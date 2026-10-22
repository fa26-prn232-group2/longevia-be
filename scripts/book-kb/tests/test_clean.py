"""Unit tests cho bước làm sạch (T069)."""

from __future__ import annotations

from src.clean import clean_text


def test_removes_figure_placeholders_and_collapses_spaces() -> None:
    raw = "Dòng  một   có   khoảng trắng.\n[[HINH:3]]\n\n\nDòng hai.\n"

    cleaned = clean_text(raw)

    assert "[[HINH" not in cleaned
    assert "Dòng một có khoảng trắng." in cleaned
    assert cleaned.count("\n\n") == 1


def test_keeps_markdown_table_rows_intact() -> None:
    raw = (
        "Mở đầu.\n\n"
        "| Thực phẩm | Hàm lượng |\n"
        "| --- | --- |\n"
        "| Cá hồi | 10 |\n"
    )

    cleaned = clean_text(raw)

    assert "| Thực phẩm | Hàm lượng |" in cleaned
    assert "| --- | --- |" in cleaned
    assert "| Cá hồi | 10 |" in cleaned
