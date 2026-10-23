"""Tiêu chí groundedness và tổng hợp báo cáo eval (T109).

Tách khỏi ``eval.py`` để giữ mỗi file trong ngưỡng cấu trúc của
``docs/development/code-hygiene.md``: module này chỉ chứa phép so khớp/đánh giá
thuần tuý và phần dựng báo cáo, không phụ thuộc gRPC.
"""

from __future__ import annotations

import json
import os
import unicodedata
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

PAGE_TOLERANCE = 3


def normalize(text: str) -> str:
    """Bỏ dấu + hạ chữ + gộp whitespace; khớp cách chuẩn hoá của CitationResolver."""

    decomposed = unicodedata.normalize("NFD", text)
    builder: list[str] = []
    for character in decomposed:
        if unicodedata.category(character) == "Mn":
            continue
        if character == "\u0111":
            builder.append("d")
        elif character == "\u0110":
            builder.append("D")
        else:
            builder.append(character)
    return " ".join("".join(builder).lower().split())


def load_corpus(path: Path) -> list[dict[str, Any]]:
    """Đọc ``chunks.jsonl`` và chuẩn hoá nội dung để đối chiếu quote."""

    if not path.is_file():
        raise ValueError(f"Không tìm thấy chunks.jsonl: {path}.")

    records: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            if not line.strip():
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError as exc:
                raise ValueError(f"chunks.jsonl dòng {line_number} không phải JSON.") from exc
            records.append({
                "chapter": int(row["chapter_number"]),
                "page_from": int(row["book_page_from"]),
                "page_to": int(row["book_page_to"]),
                "content": normalize(str(row["content"])),
            })

    if not records:
        raise ValueError("chunks.jsonl rỗng; chạy `make kb-build` trước.")
    return records


def _quote_matches(quote: str, citation: Any, corpus: Sequence[dict[str, Any]]) -> tuple[bool, str]:
    normalized_quote = normalize(quote)
    if not normalized_quote:
        return False, "empty_quote"

    candidates = [
        chunk for chunk in corpus
        if chunk["chapter"] == citation.chapter_number
        and chunk["page_from"] <= citation.book_page <= chunk["page_to"]
    ]
    if not candidates:
        return False, "no_chunk_for_citation_page"

    return any(normalized_quote in chunk["content"] for chunk in candidates), ""


def _citation_summary(citations: Sequence[Any]) -> list[dict[str, Any]]:
    return [
        {
            "chapter": int(citation.chapter_number),
            "book_page": int(citation.book_page),
            "pdf_page": int(citation.pdf_page),
            "verified": bool(citation.verified),
            "quote_chars": len(citation.quote),
        }
        for citation in citations
    ]


def evaluate(item: dict[str, Any], response: Any, corpus: Sequence[dict[str, Any]]) -> dict[str, Any]:
    """Chấm một câu trả lời theo 4 tiêu chí của T109."""

    citations = list(response.citations)
    answer = str(response.answer or "")
    terms = [str(term) for term in item.get("expected_answer_terms") or []]
    normalized_answer = normalize(answer)

    if bool(item["out_of_book"]):
        reasons: list[str] = []
        if not bool(response.abstained):
            reasons.append("out_of_book_not_abstained")
        if citations:
            reasons.append("out_of_book_has_citation")
        return {
            "id": str(item["id"]),
            "out_of_book": True,
            "passed": not reasons,
            "reasons": reasons,
            "abstained": bool(response.abstained),
            "citations": _citation_summary(citations),
            "answer_terms_hit": None,
        }

    reasons = []
    if not citations:
        reasons.append("missing_citation")

    expected_chapter = item.get("expected_chapter")
    expected_page = item.get("expected_book_page")
    page_ok = bool(citations) and expected_page is not None and any(
        (expected_chapter is None or int(citation.chapter_number) == int(expected_chapter))
        and abs(int(citation.book_page) - int(expected_page)) <= PAGE_TOLERANCE
        for citation in citations
    )
    if not page_ok:
        reasons.append(
            "page_out_of_tolerance"
            f"(expected={expected_chapter}/{expected_page},"
            f" got={[f'{c.chapter_number}/{c.book_page}' for c in citations]})"
        )

    quote_ok = bool(citations)
    quote_reason = ""
    for citation in citations:
        matched, reason = _quote_matches(citation.quote, citation, corpus)
        if not matched:
            quote_ok = False
            quote_reason = reason
            break
    if not quote_ok:
        reasons.append("quote_not_in_source_chunk" + (f"({quote_reason})" if quote_reason else ""))

    terms_hit = any(normalize(term) in normalized_answer for term in terms) if terms else None

    return {
        "id": str(item["id"]),
        "out_of_book": False,
        "passed": not reasons,
        "reasons": reasons,
        "abstained": bool(response.abstained),
        "citations": _citation_summary(citations),
        "answer_terms_hit": terms_hit,
    }


def write_report(work_dir: Path, payload: dict[str, Any]) -> Path:
    work_dir.mkdir(parents=True, exist_ok=True)
    path = work_dir / "eval-report.json"
    temporary = path.with_suffix(".json.tmp")
    temporary.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(temporary, path)
    return path


def build_report(
    items: Sequence[dict[str, Any]],
    evaluations: Sequence[dict[str, Any]],
    errors: Sequence[dict[str, Any]],
    corpus_size: int,
) -> dict[str, Any]:
    """Tổng hợp pass/fail; câu chưa chạy được tính là chưa hoàn thành, không phải pass."""

    in_book_total = sum(1 for item in items if not item["out_of_book"])
    out_book_total = sum(1 for item in items if item["out_of_book"])

    in_book = [evaluation for evaluation in evaluations if not evaluation["out_of_book"]]
    out_book = [evaluation for evaluation in evaluations if evaluation["out_of_book"]]

    passed = sum(1 for evaluation in evaluations if evaluation["passed"])
    completed = len(evaluations)
    attempted = completed + len(errors)
    total = len(items)

    terms = [evaluation["answer_terms_hit"] for evaluation in evaluations
             if evaluation["answer_terms_hit"] is not None]
    report = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "corpus_chunks": corpus_size,
        "total": total,
        "attempted": attempted,
        "completed": completed,
        "remaining": total - attempted,
        "criteria": {
            "in_book": {
                "total": in_book_total,
                "evaluated": len(in_book),
                "all_passed": sum(1 for evaluation in in_book if evaluation["passed"]),
                "missing_citation": sum(1 for evaluation in in_book if "missing_citation" in evaluation["reasons"]),
                "page_out_of_tolerance": sum(
                    1 for evaluation in in_book
                    if any(reason.startswith("page_out_of_tolerance") for reason in evaluation["reasons"])
                ),
                "quote_not_in_source_chunk": sum(
                    1 for evaluation in in_book
                    if any(reason.startswith("quote_not_in_source_chunk") for reason in evaluation["reasons"])
                ),
            },
            "out_of_book": {
                "total": out_book_total,
                "evaluated": len(out_book),
                "all_passed": sum(1 for evaluation in out_book if evaluation["passed"]),
                "abstained": sum(1 for evaluation in out_book if evaluation["abstained"]),
                "no_citation": sum(1 for evaluation in out_book if not evaluation["citations"]),
            },
        },
        "passed": passed,
        "groundedness_rate": round(passed / total, 4) if total else 0.0,
        "groundedness_rate_completed": round(passed / completed, 4) if completed else 0.0,
        "answer_terms": {
            "evaluated": len(terms),
            "hit": sum(1 for hit in terms if hit),
        },
        "errors": list(errors),
        "failures": [evaluation for evaluation in evaluations if not evaluation["passed"]],
    }

    if completed < total:
        report["status"] = "partial"
    elif passed == total:
        report["status"] = "ok"
    else:
        report["status"] = "failed"
    return report
