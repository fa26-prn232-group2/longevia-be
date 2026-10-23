"""Nhúng chunk bằng Gemini, L2-normalize và cache theo SHA-256 (T072).

``gemini-embedding-001`` **không** tự chuẩn hoá lại khi cắt về 768 chiều, nên
vector được L2-normalize thủ công trước khi ghi DB (research D-04, D-13).

Cache nằm dưới ``artifacts/book-kb/cache/`` và khoá theo SHA-256 của nội dung.
Nhờ vậy khi bị giới hạn quota, chạy lại ``build`` chỉ nhúng phần còn thiếu.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import time
from pathlib import Path
from typing import Any, Callable, Iterable, Sequence

TASK_TYPE = "RETRIEVAL_DOCUMENT"
DEFAULT_MODEL = "gemini-embedding-001"
DEFAULT_DIMENSIONS = 768
DEFAULT_BATCH_SIZE = 16
DEFAULT_MAX_RETRIES = 6
DEFAULT_BASE_DELAY = 5.0


class EmbeddingError(RuntimeError):
    """Provider nhúng lỗi hoặc trả về dữ liệu không khớp."""


def content_hash(text: str) -> str:
    """SHA-256 của nội dung chunk (UTF-8)."""

    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def l2_normalize(values: Sequence[float]) -> list[float]:
    """Chuẩn hoá vector về norm 1; từ chối vector rỗng/không hữu hạn."""

    vector = [float(value) for value in values]
    if not vector:
        raise ValueError("Vector rỗng hoặc có norm 0, không thể L2-normalize.")

    norm = math.sqrt(sum(value * value for value in vector))
    if norm == 0.0 or not math.isfinite(norm):
        raise ValueError("Vector rỗng hoặc có norm 0, không thể L2-normalize.")

    return [value / norm for value in vector]


class EmbeddingCache:
    """Cache vector theo hash nội dung, có kiểm tra model/số chiều."""

    def __init__(self, root: Path) -> None:
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)

    def _path(self, digest: str) -> Path:
        # Chia thư mục theo 2 ký tự đầu để tránh một thư mục quá lớn.
        return self.root / digest[:2] / f"{digest}.json"

    def get(self, digest: str, *, model: str, dimensions: int) -> list[float] | None:
        path = self._path(digest)
        if not path.is_file():
            return None

        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return None

        if payload.get("model") != model or payload.get("dimensions") != dimensions:
            return None

        vector = payload.get("vector")
        if not isinstance(vector, list) or len(vector) != dimensions:
            return None

        try:
            return [float(value) for value in vector]
        except (TypeError, ValueError):
            return None

    def put(
        self,
        digest: str,
        vector: Sequence[float],
        *,
        model: str,
        dimensions: int,
    ) -> None:
        path = self._path(digest)
        path.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "model": model,
            "dimensions": dimensions,
            "task_type": TASK_TYPE,
            "vector": [float(value) for value in vector],
        }
        temporary = path.with_suffix(".tmp")
        temporary.write_text(
            json.dumps(payload, ensure_ascii=False),
            encoding="utf-8",
        )
        os.replace(temporary, path)


def _is_retryable(exc: BaseException) -> bool:
    code = getattr(exc, "code", None) or getattr(exc, "status_code", None)
    if isinstance(code, int) and code in (429, 500, 502, 503, 504):
        return True

    message = str(exc).lower()
    return any(
        token in message
        for token in (
            "429",
            "resource_exhausted",
            "rate limit",
            "quota",
            "unavailable",
            "503",
            "deadline exceeded",
        )
    )


class Embedder:
    """Bọc ``google-genai`` và chuẩn hoá vector trả về."""

    def __init__(
        self,
        *,
        client: Any,
        model: str = DEFAULT_MODEL,
        dimensions: int = DEFAULT_DIMENSIONS,
        task_type: str = TASK_TYPE,
        max_retries: int = DEFAULT_MAX_RETRIES,
        base_delay: float = DEFAULT_BASE_DELAY,
        sleeper: Callable[[float], None] = time.sleep,
    ) -> None:
        self.client = client
        self.model = model
        self.dimensions = dimensions
        self.task_type = task_type
        self.max_retries = max_retries
        self.base_delay = base_delay
        self._sleeper = sleeper
        self._config = self._build_config(task_type, dimensions)

    @staticmethod
    def _build_config(task_type: str, dimensions: int) -> object:
        try:
            from google.genai import types
        except ImportError:  # test double không cần SDK
            return {
                "task_type": task_type,
                "output_dimensionality": dimensions,
            }

        return types.EmbedContentConfig(
            task_type=task_type,
            output_dimensionality=dimensions,
        )

    def embed_batch(self, texts: Sequence[str]) -> list[list[float]]:
        if not texts:
            return []

        response: object | None = None
        for attempt in range(self.max_retries + 1):
            try:
                response = self.client.models.embed_content(
                    model=self.model,
                    contents=list(texts),
                    config=self._config,
                )
                break
            except Exception as exc:  # noqa: BLE001 - provider có nhiều kiểu lỗi
                if attempt >= self.max_retries or not _is_retryable(exc):
                    raise EmbeddingError(
                        f"Gọi Gemini embedding thất bại sau {attempt + 1} lần thử: {exc}"
                    ) from exc

                delay = min(self.base_delay * (2**attempt), 120.0)
                print(
                    f"[embed] Lỗi tạm thời ({exc.__class__.__name__}); "
                    f"thử lại sau {delay:.0f}s (lần {attempt + 1}/{self.max_retries}).",
                )
                self._sleeper(delay)

        embeddings = getattr(response, "embeddings", None)
        if not embeddings:
            raise EmbeddingError("Gemini không trả về embedding nào.")
        if len(embeddings) != len(texts):
            raise EmbeddingError(
                f"Gemini trả về {len(embeddings)} vector cho {len(texts)} chunk."
            )

        vectors: list[list[float]] = []
        for embedding in embeddings:
            values = getattr(embedding, "values", None)
            if values is None:
                raise EmbeddingError("Gemini trả về embedding thiếu trường 'values'.")
            vectors.append(l2_normalize(values))
        return vectors


def create_client(api_key: str) -> Any:
    """Tạo client ``google-genai``; không log API key."""

    if not api_key:
        raise EmbeddingError("Thiếu GOOGLE_AI__API_KEY để nhúng corpus.")
    from google import genai

    return genai.Client(api_key=api_key)


def embed_texts(
    texts: Iterable[str],
    embedder: Embedder,
    cache: EmbeddingCache,
    *,
    batch_size: int = DEFAULT_BATCH_SIZE,
    progress: Callable[[int, int], None] | None = None,
) -> list[list[float]]:
    """Nhúng danh sách text, dùng cache và chỉ gửi phần còn thiếu lên provider."""

    items = list(texts)
    if batch_size <= 0:
        raise ValueError("batch_size phải dương.")

    digests = [content_hash(text) for text in items]
    resolved: dict[str, list[float]] = {}
    missing: list[tuple[str, str]] = []
    requested: set[str] = set()

    for digest, text in zip(digests, items):
        if digest in resolved or digest in requested:
            continue

        cached = cache.get(digest, model=embedder.model, dimensions=embedder.dimensions)
        if cached is not None:
            resolved[digest] = cached
            continue

        requested.add(digest)
        missing.append((digest, text))

    total = len(missing)
    for offset in range(0, total, batch_size):
        batch = missing[offset : offset + batch_size]
        vectors = embedder.embed_batch([text for _, text in batch])
        if len(vectors) != len(batch):
            raise EmbeddingError("Số vector trả về không khớp số chunk gửi đi.")

        for (digest, _), vector in zip(batch, vectors):
            cache.put(
                digest,
                vector,
                model=embedder.model,
                dimensions=embedder.dimensions,
            )
            resolved[digest] = vector

        if progress is not None:
            progress(min(offset + len(batch), total), total)

    return [resolved[digest] for digest in digests]
