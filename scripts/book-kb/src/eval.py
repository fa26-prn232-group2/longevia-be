"""Đánh giá groundedness của RPC ``AskLongevityQuestion`` trên bộ câu hỏi vàng (T109).

Script gọi **service thật** qua gRPC (không gọi provider trực tiếp), rồi kiểm tra
bốn tiêu chí cho từng câu (xem ``eval_report.evaluate``):

* câu trong sách phải có citation;
* ``book_page`` của citation phải nằm trong ±3 trang bản in kỳ vọng (và đúng chương);
* ``quote`` phải xuất hiện nguyên văn trong chunk nguồn tương ứng của
  ``artifacts/book-kb/chunks.jsonl``;
* câu ngoài sách phải ``abstained = true`` và không có citation.

Kết quả ghi ra ``artifacts/book-kb/eval-report.json``. Thiếu
``GOOGLE_AI__API_KEY`` thì thoát với mã riêng (3) và log ``SKIPPED`` - tuyệt đối
không báo pass giả.
"""

from __future__ import annotations

import importlib
import json
import os
import sys
import tempfile
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

from .eval_report import build_report, evaluate, load_corpus, write_report

EXIT_OK = 0
EXIT_FAILED = 1
EXIT_SKIPPED = 3

DEFAULT_SERVICE_URL = "http://recommendation:50051"
DEFAULT_TIMEOUT_SECONDS = 60.0
MAX_ATTEMPTS = 3
MAX_CONSECUTIVE_QUOTA_ERRORS = 3
MAX_CONSECUTIVE_ERRORS = 5

RETRYABLE_CODES = {"RESOURCE_EXHAUSTED", "UNAVAILABLE", "DEADLINE_EXCEEDED", "INTERNAL"}


class EvalError(RuntimeError):
    """Cấu hình golden set, proto hoặc lời gọi service không hợp lệ."""


@dataclass
class Stubs:
    """Module sinh từ proto tại thời điểm chạy (không commit code sinh)."""

    pb2: Any
    pb2_grpc: Any
    grpc: Any


def _env_float(name: str, default: float) -> float:
    raw = os.getenv(name)
    if raw is None or not raw.strip():
        return default
    try:
        return float(raw)
    except ValueError as exc:
        raise EvalError(f"Biến môi trường {name} phải là số: {raw!r}.") from exc


def _resolve_golden_set(args: Any) -> Path:
    candidates: list[Path] = []
    explicit = getattr(args, "golden_set", None) or os.getenv("BOOK_KB_GOLDEN_SET")
    if explicit:
        candidates.append(Path(explicit))
    candidates.append(Path("eval/golden-set.json"))
    candidates.append(Path("/app/eval/golden-set.json"))
    candidates.append(Path(__file__).resolve().parents[1] / "eval" / "golden-set.json")

    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise EvalError("Không tìm thấy golden-set.json; đặt BOOK_KB_GOLDEN_SET.")


def _resolve_proto_path() -> Path:
    candidates = [
        os.getenv("BOOK_KB_PROTO"),
        "/contracts/recommendation.proto",
        _repository_proto_candidate(),
    ]
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return Path(candidate)
    raise EvalError("Không tìm thấy recommendation.proto; đặt BOOK_KB_PROTO.")


def _repository_proto_candidate() -> str | None:
    """Đường dẫn proto khi chạy ngoài container (repo layout); None nếu không đủ cấp."""

    parents = Path(__file__).resolve().parents
    if len(parents) <= 3:
        return None
    return str(parents[3] / "contracts" / "recommendation" / "v1" / "recommendation.proto")


def _load_golden_set(path: Path) -> list[dict[str, Any]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    questions = payload.get("questions") if isinstance(payload, dict) else payload
    if not isinstance(questions, list) or not questions:
        raise EvalError(f"{path} không chứa danh sách câu hỏi.")

    seen: set[str] = set()
    required = {"id", "question", "expected_chapter", "expected_book_page",
                "expected_answer_terms", "out_of_book"}
    for item in questions:
        if not isinstance(item, dict) or not required.issubset(item):
            raise EvalError(f"Câu hỏi thiếu trường bắt buộc: {item!r}.")
        identifier = str(item["id"])
        if identifier in seen:
            raise EvalError(f"Id câu hỏi bị trùng: {identifier}.")
        seen.add(identifier)

    return questions


def _load_stubs(proto_path: Path) -> Stubs:
    try:
        from grpc_tools import protoc
        import grpc
    except ImportError as exc:
        raise EvalError(
            "Thiếu grpcio/grpcio-tools; cài lại image bằng `make kb-build`."
        ) from exc

    output = Path(tempfile.mkdtemp(prefix="book-kb-eval-"))
    result = protoc.main([
        "grpc_tools.protoc",
        f"-I{proto_path.parent}",
        f"--python_out={output}",
        f"--grpc_python_out={output}",
        str(proto_path),
    ])
    if result != 0:
        raise EvalError(f"Sinh stub gRPC thất bại (protoc trả về {result}).")

    sys.path.insert(0, str(output))
    pb2 = importlib.import_module(f"{proto_path.stem}_pb2")
    pb2_grpc = importlib.import_module(f"{proto_path.stem}_pb2_grpc")
    return Stubs(pb2=pb2, pb2_grpc=pb2_grpc, grpc=grpc)


def _channel_target(url: str) -> str:
    parsed = urlparse(url if "://" in url else f"http://{url}")
    host = parsed.hostname or "recommendation"
    port = parsed.port or 50051
    return f"{host}:{port}"


def _error_code(exc: Exception) -> str:
    code = getattr(exc, "code", None)
    if callable(code):
        try:
            value = code()
        except Exception:  # noqa: BLE001 - lỗi code() của gRPC không quan trọng
            return ""
        return getattr(value, "name", str(value))
    return ""


def _is_quota_error(exc: Exception) -> bool:
    return _error_code(exc) == "RESOURCE_EXHAUSTED" or "429" in str(exc)


def _ask(stub: Any, stubs: Stubs, question: str, timeout: float) -> Any:
    request = stubs.pb2.AskLongevityQuestionRequest(question=question)

    for attempt in range(MAX_ATTEMPTS):
        try:
            return stub.AskLongevityQuestion(request, timeout=timeout)
        except Exception as exc:  # noqa: BLE001 - gRPC có nhiều kiểu lỗi transport
            retryable = _error_code(exc) in RETRYABLE_CODES
            if attempt + 1 >= MAX_ATTEMPTS or not retryable:
                raise
            delay = 5.0 * (2 ** attempt)
            print(f"[eval] Lỗi tạm thời ({_error_code(exc) or exc.__class__.__name__}); "
                  f"thử lại sau {delay:.0f}s.", file=sys.stderr)
            time.sleep(delay)

    raise EvalError("Không gọi được AskLongevityQuestion.")


def run_eval(args: Any) -> int:
    """Entry point cho lệnh ``eval`` của CLI; trả mã thoát của tiến trình."""

    work_dir = Path(getattr(args, "work", None) or os.getenv("BOOK_KB_WORK") or "artifacts/book-kb")
    api_key = os.getenv("GOOGLE_AI__API_KEY", "").strip()
    if not api_key:
        print("SKIPPED: missing key (GOOGLE_AI__API_KEY not set) - không chạy eval, không báo pass.",
              file=sys.stderr)
        write_report(work_dir, {
            "status": "skipped",
            "reason": "missing_key",
            "generated_at": datetime.now(timezone.utc).isoformat(),
        })
        return EXIT_SKIPPED

    golden_set = _resolve_golden_set(args)
    items = _load_golden_set(golden_set)
    corpus = load_corpus(work_dir / "chunks.jsonl")
    stubs = _load_stubs(_resolve_proto_path())

    service_url = os.getenv("RECOMMENDATION_GRPC_URL", DEFAULT_SERVICE_URL)
    timeout = _env_float("BOOK_KB_EVAL_TIMEOUT", DEFAULT_TIMEOUT_SECONDS)
    target = _channel_target(service_url)
    print(f"[eval] {len(items)} câu | corpus {len(corpus)} chunk | gRPC {target}")

    evaluations: list[dict[str, Any]] = []
    errors: list[dict[str, Any]] = []
    consecutive_errors = 0
    consecutive_quota = 0

    with stubs.grpc.insecure_channel(target) as channel:
        stub = stubs.pb2_grpc.RecommendationServiceStub(channel)
        for index, item in enumerate(items):
            try:
                response = _ask(stub, stubs, str(item["question"]), timeout)
            except Exception as exc:  # noqa: BLE001 - ghi lại và tiếp tục câu kế tiếp
                code = _error_code(exc) or exc.__class__.__name__
                errors.append({"id": str(item["id"]), "code": code, "message": str(exc)[:500]})
                print(f"[eval] {item['id']} ERROR {code}", file=sys.stderr)
                consecutive_errors += 1
                consecutive_quota = consecutive_quota + 1 if _is_quota_error(exc) else 0
                if consecutive_errors >= MAX_CONSECUTIVE_ERRORS or consecutive_quota >= MAX_CONSECUTIVE_QUOTA_ERRORS:
                    remaining = len(items) - len(evaluations) - len(errors)
                    print(f"[eval] Dừng sớm sau {consecutive_errors} lỗi liên tiếp; "
                          f"còn {remaining} câu chưa chạy.", file=sys.stderr)
                    break
                continue

            consecutive_errors = 0
            consecutive_quota = 0
            evaluation = evaluate(item, response, corpus)
            evaluations.append(evaluation)
            print(f"[eval] {item['id']} {'PASS' if evaluation['passed'] else 'FAIL'}"
                  f" [{index + 1}/{len(items)}]")

    report = build_report(items, evaluations, errors, len(corpus))
    path = write_report(work_dir, report)
    print(f"[eval] {report['passed']}/{report['total']} pass "
          f"({report['groundedness_rate'] * 100:.1f}%) -> {path}")

    if report["status"] != "ok":
        return EXIT_FAILED
    return EXIT_OK
