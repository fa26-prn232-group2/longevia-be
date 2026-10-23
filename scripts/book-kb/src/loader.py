"""Nạp corpus vào ``recommendation_db`` (schema ``kb``) idempotent (T073).

Pipeline dùng ``psycopg`` (không dùng EF Core) vì đây là tiến trình offline.
Mọi upsert dựa trên khoá tự nhiên:

- ``kb.corpus`` theo ``(corpus_key, version)``;
- ``kb.chapter`` theo ``(corpus_id, chapter_number)``;
- ``kb.chunk`` theo ``(corpus_id, chapter_id, chunk_index)``;
- ``kb.page_flag`` theo ``(corpus_id, pdf_page, kind, source_file)``.

Cột ``content_tsv`` **không** được ghi: trigger trong migration tự tính từ
``content``. Embedding được truyền dưới dạng chuỗi ``'[..]'`` và cast
``CAST(%s AS vector)``.
"""

from __future__ import annotations

import gzip
import json
import math
import os
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Mapping

from .review import SUSPICIOUS_KINDS, VERIFIED_KINDS

NAMESPACE = uuid.NAMESPACE_URL


class LoadError(RuntimeError):
    """Dữ liệu build hoặc kết nối database không hợp lệ."""


def _parse_connection_string(value: str) -> dict[str, Any]:
    parts: dict[str, str] = {}
    for item in value.split(";"):
        if "=" not in item:
            continue
        key, raw = item.split("=", 1)
        parts[key.strip().lower().replace(" ", "")] = raw.strip()

    aliases = {
        "host": ("host", "server", "datasource"),
        "port": ("port",),
        "dbname": ("database", "dbname", "initialcatalog"),
        "user": ("username", "userid", "user"),
        "password": ("password", "pwd"),
        "sslmode": ("sslmode",),
    }

    result: dict[str, Any] = {}
    for target, names in aliases.items():
        for name in names:
            if name in parts and parts[name]:
                result[target] = parts[name]
                break

    if "port" in result:
        try:
            result["port"] = int(result["port"])
        except ValueError as exc:
            raise LoadError("Port trong connection string không hợp lệ.") from exc

    missing = [key for key in ("host", "dbname", "user") if key not in result]
    if missing:
        raise LoadError(
            "Connection string thiếu trường bắt buộc: " + ", ".join(missing) + "."
        )
    return result


def resolve_connection(env: Mapping[str, str] | None = None) -> dict[str, Any] | str:
    """Đọc ``ConnectionStrings__Recommendation`` hoặc các biến ``POSTGRES_*``."""

    values = dict(os.environ if env is None else env)
    connection = values.get("ConnectionStrings__Recommendation") or values.get(
        "CONNECTIONSTRINGS__RECOMMENDATION"
    )
    if connection:
        connection = connection.strip()
        if connection.startswith(("postgres://", "postgresql://")):
            return connection
        return _parse_connection_string(connection)

    host = values.get("POSTGRES_HOST")
    user = values.get("POSTGRES_USER")
    dbname = values.get("KNOWLEDGE_DB") or values.get("POSTGRES_DB")
    if not host or not user or not dbname:
        raise LoadError(
            "Không có ConnectionStrings__Recommendation và cũng thiếu POSTGRES_* / KNOWLEDGE_DB."
        )

    result: dict[str, Any] = {
        "host": host,
        "dbname": dbname,
        "user": user,
    }
    if values.get("POSTGRES_PORT"):
        result["port"] = int(values["POSTGRES_PORT"])
    if values.get("POSTGRES_PASSWORD"):
        result["password"] = values["POSTGRES_PASSWORD"]
    return result


def connect(env: Mapping[str, str] | None = None) -> Any:
    """Mở kết nối psycopg; trả về connection chưa autocommit."""

    import psycopg

    target = resolve_connection(env)
    if isinstance(target, str):
        connection = psycopg.connect(target)
    else:
        connection = psycopg.connect(**target)
    connection.autocommit = False
    return connection


def _vector_literal(values: Iterable[float]) -> str:
    vector = [float(value) for value in values]
    if not vector or any(not math.isfinite(value) for value in vector):
        raise LoadError("Embedding rỗng hoặc chứa giá trị không hữu hạn.")
    return "[" + ",".join(repr(value) for value in vector) + "]"


def _resolve_artifact(path: Path) -> Path:
    """Trả file tồn tại; chấp nhận bản nén ``<name>.gz`` của seed."""

    if path.is_file():
        return path
    compressed = path.with_name(path.name + ".gz")
    if compressed.is_file():
        return compressed
    raise LoadError(f"Không tìm thấy file build: {path}.")


def _read_jsonl(path: Path) -> list[dict[str, Any]]:
    resolved = _resolve_artifact(path)
    rows: list[dict[str, Any]] = []
    opener = gzip.open if resolved.suffix == ".gz" else open
    with opener(resolved, "rt", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            if not line.strip():
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise LoadError(f"{resolved.name} dòng {line_number} không phải JSON.") from exc
    return rows


def _read_build(work_dir: Path) -> dict[str, Any]:
    build_path = work_dir / "build.json"
    if not build_path.is_file():
        raise LoadError(f"Không tìm thấy build.json trong {work_dir}.")
    try:
        build = json.loads(build_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise LoadError("build.json không phải JSON hợp lệ.") from exc
    if not isinstance(build, dict):
        raise LoadError("build.json phải là một JSON object.")
    return build


def is_loaded(connection: Any, corpus_key: str, version: str) -> bool:
    """True khi đúng ``corpus_key`` + ``version`` đang là corpus active."""

    cursor = connection.cursor()
    try:
        cursor.execute(
            "SELECT 1 FROM kb.corpus WHERE corpus_key = %s AND version = %s AND is_active",
            (corpus_key, version),
        )
        return cursor.fetchone() is not None
    finally:
        cursor.close()


def _load_payload(
    work_dir: Path,
    build: dict[str, Any] | None = None,
) -> tuple[dict[str, Any], list[dict[str, Any]], list[dict[str, Any]]]:
    build = build if build is not None else _read_build(work_dir)
    chunks = _read_jsonl(work_dir / "chunks.jsonl")
    flags = _read_jsonl(work_dir / "page_flags.jsonl")

    dimensions = int(build.get("embedding_dim") or 0)
    declared = int(build.get("chunk_count") or 0)
    if len(chunks) != declared:
        raise LoadError(
            f"build.json ghi {declared} chunk nhưng chunks.jsonl có {len(chunks)}."
        )

    for chunk in chunks:
        embedding = chunk.get("embedding")
        if not isinstance(embedding, list) or len(embedding) != dimensions:
            raise LoadError(
                f"Chunk {chunk.get('chapter_number')}/{chunk.get('chunk_index')} "
                f"không có embedding đủ {dimensions} chiều."
            )

    if not build.get("chapters"):
        raise LoadError("build.json thiếu danh sách chương.")
    return build, chunks, flags


def _chapter_id(corpus_key: str, version: str, chapter_number: int) -> uuid.UUID:
    return uuid.uuid5(NAMESPACE, f"{corpus_key}:{version}:chapter:{chapter_number}")


def _corpus_id(corpus_key: str, version: str) -> uuid.UUID:
    return uuid.uuid5(NAMESPACE, f"{corpus_key}:{version}")


def load_build(
    work_dir: Path,
    connection: Any,
    *,
    skip_if_loaded: bool = False,
    verify_after_load: bool = False,
) -> dict[str, Any]:
    """Nạp một thư mục artifact vào database trong một transaction.

    ``skip_if_loaded`` dùng cho bước deploy (service ``kb-init``): nếu đúng
    ``corpus_key`` + ``version`` đang active thì trả về ngay, không ghi lại.
    ``verify_after_load`` chạy ``verify_database`` sau khi commit để seed lệch
    metadata bị phát hiện thay vì âm thầm nằm trong database.
    """

    work_dir = Path(work_dir)
    build = _read_build(work_dir)
    corpus_key = str(build["corpus_key"])
    version = str(build["version"])

    if skip_if_loaded and is_loaded(connection, corpus_key, version):
        return {
            "skipped": True,
            "reason": f"corpus '{corpus_key}' version {version[:12]}… đã active.",
            "corpus_key": corpus_key,
            "version": version,
        }

    build, chunks, flags = _load_payload(work_dir, build)
    embedded_at = datetime.fromisoformat(str(build["embedded_at"]))
    if embedded_at.tzinfo is None:
        embedded_at = embedded_at.replace(tzinfo=timezone.utc)

    from psycopg.types.json import Jsonb

    cursor = connection.cursor()
    try:
        cursor.execute(
            "UPDATE kb.corpus SET is_active = false "
            "WHERE corpus_key = %s AND is_active AND version <> %s",
            (corpus_key, version),
        )
        cursor.execute(
            """
            INSERT INTO kb.corpus (
                id, corpus_key, version, title, source_path,
                chapter_count, chunk_count, embedding_model, embedding_dim,
                chunker_params, is_active, embedded_at, created_at
            )
            VALUES (
                %s, %s, %s, %s, %s,
                %s, %s, %s, %s,
                %s, true, %s, now()
            )
            ON CONFLICT (corpus_key, version) DO UPDATE SET
                title = EXCLUDED.title,
                source_path = EXCLUDED.source_path,
                chapter_count = EXCLUDED.chapter_count,
                chunk_count = EXCLUDED.chunk_count,
                embedding_model = EXCLUDED.embedding_model,
                embedding_dim = EXCLUDED.embedding_dim,
                chunker_params = EXCLUDED.chunker_params,
                is_active = true,
                embedded_at = EXCLUDED.embedded_at
            RETURNING id
            """,
            (
                _corpus_id(corpus_key, version),
                corpus_key,
                version,
                str(build.get("title") or ""),
                str(build.get("source_path") or ""),
                int(build.get("chapter_count") or 0),
                int(build.get("chunk_count") or 0),
                str(build.get("embedding_model") or ""),
                int(build.get("embedding_dim") or 0),
                Jsonb(build.get("chunker_params") or {}),
                embedded_at,
            ),
        )
        corpus_id = cursor.fetchone()[0]

        cursor.execute(
            """
            DELETE FROM kb.corpus
            WHERE corpus_key = %s
              AND id NOT IN (
                  SELECT id FROM kb.corpus
                  WHERE corpus_key = %s
                  ORDER BY is_active DESC, created_at DESC
                  LIMIT 2
              )
            """,
            (corpus_key, corpus_key),
        )

        chapter_ids: dict[int, uuid.UUID] = {}
        for chapter in build["chapters"]:
            number = int(chapter["number"])
            cursor.execute(
                """
                INSERT INTO kb.chapter (
                    id, corpus_id, chapter_number, title, source_file,
                    book_page_from, book_page_to, pdf_page_from, pdf_page_to
                )
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (corpus_id, chapter_number) DO UPDATE SET
                    title = EXCLUDED.title,
                    source_file = EXCLUDED.source_file,
                    book_page_from = EXCLUDED.book_page_from,
                    book_page_to = EXCLUDED.book_page_to,
                    pdf_page_from = EXCLUDED.pdf_page_from,
                    pdf_page_to = EXCLUDED.pdf_page_to
                RETURNING id
                """,
                (
                    _chapter_id(corpus_key, version, number),
                    corpus_id,
                    number,
                    str(chapter.get("title") or ""),
                    str(chapter.get("source_file") or ""),
                    int(chapter["book_page_from"]),
                    int(chapter["book_page_to"]),
                    int(chapter["pdf_page_from"]),
                    int(chapter["pdf_page_to"]),
                ),
            )
            chapter_ids[number] = cursor.fetchone()[0]

        chunk_sql = """
            INSERT INTO kb.chunk (
                corpus_id, chapter_id, chapter_number, chunk_index,
                content, token_count, content_hash,
                book_page_from, book_page_to, pdf_page_from, pdf_page_to,
                verified, needs_review, embedding
            )
            VALUES (
                %s, %s, %s, %s,
                %s, %s, %s,
                %s, %s, %s, %s,
                %s, %s, CAST(%s AS vector)
            )
            ON CONFLICT (corpus_id, chapter_id, chunk_index) DO UPDATE SET
                chapter_number = EXCLUDED.chapter_number,
                content = EXCLUDED.content,
                token_count = EXCLUDED.token_count,
                content_hash = EXCLUDED.content_hash,
                book_page_from = EXCLUDED.book_page_from,
                book_page_to = EXCLUDED.book_page_to,
                pdf_page_from = EXCLUDED.pdf_page_from,
                pdf_page_to = EXCLUDED.pdf_page_to,
                verified = EXCLUDED.verified,
                needs_review = EXCLUDED.needs_review,
                embedding = EXCLUDED.embedding
        """

        grouped: dict[int, list[dict[str, Any]]] = {}
        for chunk in chunks:
            number = int(chunk["chapter_number"])
            if number not in chapter_ids:
                raise LoadError(f"Chunk thuộc chương không có trong build.json: {number}.")
            grouped.setdefault(number, []).append(chunk)

        for number, rows in sorted(grouped.items()):
            rows.sort(key=lambda item: int(item["chunk_index"]))
            parameters = [
                (
                    corpus_id,
                    chapter_ids[number],
                    number,
                    int(chunk["chunk_index"]),
                    str(chunk["content"]),
                    int(chunk["token_count"]),
                    str(chunk["content_hash"]),
                    int(chunk["book_page_from"]),
                    int(chunk["book_page_to"]),
                    int(chunk["pdf_page_from"]),
                    int(chunk["pdf_page_to"]),
                    bool(chunk["verified"]),
                    bool(chunk["needs_review"]),
                    _vector_literal(chunk["embedding"]),
                )
                for chunk in rows
            ]
            cursor.executemany(chunk_sql, parameters)

            # Dọn chunk dư của cùng version nếu chunker thay đổi số đoạn.
            cursor.execute(
                "DELETE FROM kb.chunk "
                "WHERE corpus_id = %s AND chapter_id = %s AND chunk_index >= %s",
                (corpus_id, chapter_ids[number], len(rows)),
            )

        flag_sql = """
            INSERT INTO kb.page_flag (
                corpus_id, pdf_page, book_page, kind, source_file, note
            )
            VALUES (%s, %s, %s, %s, %s, %s)
            ON CONFLICT (corpus_id, pdf_page, kind, source_file) DO UPDATE SET
                book_page = EXCLUDED.book_page,
                note = EXCLUDED.note
        """
        cursor.executemany(
            flag_sql,
            [
                (
                    corpus_id,
                    int(flag["pdf_page"]),
                    int(flag["book_page"]),
                    str(flag["kind"]),
                    str(flag["source_file"]),
                    flag.get("note"),
                )
                for flag in flags
            ],
        )

        cursor.execute(
            """
            UPDATE kb.corpus SET
                chapter_count = (SELECT count(*) FROM kb.chapter WHERE corpus_id = %s),
                chunk_count = (SELECT count(*) FROM kb.chunk WHERE corpus_id = %s)
            WHERE id = %s
            """,
            (corpus_id, corpus_id, corpus_id),
        )
        connection.commit()
    except Exception:
        connection.rollback()
        raise
    finally:
        cursor.close()

    result = {
        "corpus_id": str(corpus_id),
        "corpus_key": corpus_key,
        "version": version,
        "chapters": len(chapter_ids),
        "chunks": len(chunks),
        "page_flags": len(flags),
        "is_active": True,
    }

    if verify_after_load:
        report = verify_database(
            connection,
            corpus_key,
            {"chapter_count": len(chapter_ids), "chunk_count": len(chunks)},
        )
        result["verify"] = report
        if not report.get("ok"):
            raise LoadError(
                "Nạp xong nhưng verify không khớp: " + json.dumps(report, ensure_ascii=False)
            )

    return result


def _scalar(cursor: Any, sql: str, parameters: tuple[Any, ...]) -> Any:
    cursor.execute(sql, parameters)
    row = cursor.fetchone()
    return row[0] if row else None


def verify_database(
    connection: Any,
    corpus_key: str,
    expected: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Đối chiếu số chương, số chunk, embedding và khoảng trang."""

    expected = dict(expected or {})
    cursor = connection.cursor()
    try:
        cursor.execute(
            """
            SELECT id, version, chapter_count, chunk_count,
                   embedding_model, embedding_dim, is_active
            FROM kb.corpus
            WHERE corpus_key = %s AND is_active
            """,
            (corpus_key,),
        )
        corpus = cursor.fetchone()
        if corpus is None:
            return {
                "ok": False,
                "error": f"Không có corpus active cho '{corpus_key}'.",
            }

        corpus_id = corpus[0]
        actual_chapters = _scalar(
            cursor,
            "SELECT count(*) FROM kb.chapter WHERE corpus_id = %s",
            (corpus_id,),
        )
        actual_chunks = _scalar(
            cursor,
            "SELECT count(*) FROM kb.chunk WHERE corpus_id = %s",
            (corpus_id,),
        )
        needs_review = _scalar(
            cursor,
            "SELECT count(*) FROM kb.chunk WHERE corpus_id = %s AND needs_review",
            (corpus_id,),
        )
        verified = _scalar(
            cursor,
            "SELECT count(*) FROM kb.chunk WHERE corpus_id = %s AND verified",
            (corpus_id,),
        )
        missing_embedding = _scalar(
            cursor,
            "SELECT count(*) FROM kb.chunk "
            "WHERE corpus_id = %s AND (embedding IS NULL OR vector_dims(embedding) <> %s)",
            (corpus_id, int(corpus[5])),
        )
        bad_hash = _scalar(
            cursor,
            "SELECT count(*) FROM kb.chunk "
            "WHERE corpus_id = %s AND length(content_hash) <> 64",
            (corpus_id,),
        )
        bad_ranges = _scalar(
            cursor,
            """
            SELECT count(*)
            FROM kb.chunk AS c
            JOIN kb.chapter AS ch ON ch.id = c.chapter_id
            WHERE c.corpus_id = %s
              AND (
                  c.book_page_from > c.book_page_to
                  OR c.pdf_page_from > c.pdf_page_to
                  OR c.book_page_from < ch.book_page_from
                  OR c.book_page_to > ch.book_page_to
                  OR c.pdf_page_from < ch.pdf_page_from
                  OR c.pdf_page_to > ch.pdf_page_to
              )
            """,
            (corpus_id,),
        )
        flag_count = _scalar(
            cursor,
            "SELECT count(*) FROM kb.page_flag WHERE corpus_id = %s",
            (corpus_id,),
        )
        cursor.execute(
            "SELECT kind, count(*) FROM kb.page_flag WHERE corpus_id = %s GROUP BY kind ORDER BY kind",
            (corpus_id,),
        )
        by_kind = dict(cursor.fetchall())
        suspicious_pages = _scalar(
            cursor,
            """
            SELECT count(DISTINCT f.pdf_page)
            FROM kb.page_flag AS f
            WHERE f.corpus_id = %s
              AND f.kind = ANY(%s)
              AND NOT EXISTS (
                  SELECT 1 FROM kb.page_flag AS v
                  WHERE v.corpus_id = f.corpus_id
                    AND v.pdf_page = f.pdf_page
                    AND v.kind = ANY(%s)
              )
            """,
            (corpus_id, sorted(SUSPICIOUS_KINDS), sorted(VERIFIED_KINDS)),
        )
        overridden_pages = _scalar(
            cursor,
            """
            SELECT count(DISTINCT f.pdf_page)
            FROM kb.page_flag AS f
            WHERE f.corpus_id = %s
              AND f.kind = ANY(%s)
              AND EXISTS (
                  SELECT 1 FROM kb.page_flag AS v
                  WHERE v.corpus_id = f.corpus_id
                    AND v.pdf_page = f.pdf_page
                    AND v.kind = ANY(%s)
              )
            """,
            (corpus_id, sorted(SUSPICIOUS_KINDS), sorted(VERIFIED_KINDS)),
        )
    finally:
        cursor.close()

    expected_chapters = expected.get("chapter_count")
    expected_chunks = expected.get("chunk_count")
    checks = {
        "one_active_corpus": bool(corpus[6]),
        "chapters_match_expected": expected_chapters is None
        or int(actual_chapters) == int(expected_chapters),
        "chunk_count_match_expected": expected_chunks is None
        or int(actual_chunks) == int(expected_chunks),
        "corpus_chunk_count_matches": int(actual_chunks) == int(corpus[3]),
        "embeddings_match_dimension": int(missing_embedding) == 0,
        "content_hash_is_sha256": int(bad_hash) == 0,
        "page_ranges_valid": int(bad_ranges) == 0,
        "page_flags_present": int(flag_count) > 0,
    }

    return {
        "ok": all(checks.values()),
        "corpus_id": str(corpus_id),
        "version": corpus[1],
        "embedding_model": corpus[4],
        "embedding_dim": int(corpus[5]),
        "chapters": int(actual_chapters),
        "chunks": int(actual_chunks),
        "needs_review": int(needs_review),
        "verified": int(verified),
        "needs_review_ratio": round(int(needs_review) / int(actual_chunks), 4)
        if int(actual_chunks)
        else 0.0,
        "page_flags": int(flag_count),
        "page_flags_by_kind": {str(key): int(value) for key, value in by_kind.items()},
        "suspicious_pages": int(suspicious_pages),
        "overridden_pages": int(overridden_pages),
        "expected_chapters": expected_chapters,
        "expected_chunks": expected_chunks,
        "checks": checks,
    }
