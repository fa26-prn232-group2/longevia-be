"""Unit tests cho embedding: L2-normalize và cache theo SHA-256 (T066)."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

import pytest

from src.embed import Embedder, EmbeddingCache, embed_texts, l2_normalize


class _FakeModels:
    def __init__(self) -> None:
        self.calls: list[list[str]] = []

    def embed_content(
        self,
        *,
        model: str,
        contents: list[str],
        config: object,
    ) -> object:
        # Không cần config trong test nhưng phải nhận đúng tham số như SDK.
        assert model == "gemini-embedding-001"
        assert isinstance(contents, list)
        self.calls.append(list(contents))
        return SimpleNamespace(
            embeddings=[SimpleNamespace(values=[3.0, 4.0]) for _ in contents]
        )


class _FakeClient:
    def __init__(self) -> None:
        self.models = _FakeModels()


def _embedder(client: _FakeClient) -> Embedder:
    return Embedder(
        client=client,
        model="gemini-embedding-001",
        dimensions=2,
        task_type="RETRIEVAL_DOCUMENT",
    )


def test_l2_normalize_produces_unit_norm() -> None:
    vector = l2_normalize([3.0, 4.0])
    assert vector == pytest.approx([0.6, 0.8])
    assert sum(value * value for value in vector) ** 0.5 == pytest.approx(1.0)


def test_l2_normalize_rejects_zero_vector() -> None:
    with pytest.raises(ValueError, match="rỗng"):
        l2_normalize([0.0, 0.0])


def test_embed_texts_normalizes_vectors(tmp_path: Path) -> None:
    client = _FakeClient()
    cache = EmbeddingCache(tmp_path)

    vectors = embed_texts(["một", "hai"], _embedder(client), cache)

    assert len(vectors) == 2
    for vector in vectors:
        assert vector == pytest.approx([0.6, 0.8])


def test_cache_prevents_second_paid_call(tmp_path: Path) -> None:
    client = _FakeClient()
    cache = EmbeddingCache(tmp_path)
    embedder = _embedder(client)

    embed_texts(["một", "hai"], embedder, cache)
    assert len(client.models.calls) == 1

    embed_texts(["một", "hai"], embedder, cache)
    assert len(client.models.calls) == 1, "lần gọi lại phải trúng cache"


def test_only_missing_texts_are_sent_to_provider(tmp_path: Path) -> None:
    client = _FakeClient()
    cache = EmbeddingCache(tmp_path)
    embedder = _embedder(client)

    embed_texts(["một"], embedder, cache)
    embed_texts(["một", "ba"], embedder, cache)

    assert client.models.calls == [["một"], ["ba"]]


def test_cache_metadata_mismatch_is_a_miss(tmp_path: Path) -> None:
    cache = EmbeddingCache(tmp_path)
    cache.put("abc", [0.6, 0.8], model="other-model", dimensions=2)

    assert cache.get("abc", model="gemini-embedding-001", dimensions=2) is None
