from pathlib import Path

import pytest

from imperialism_format import ScenarioInfo


def sample_text(newline="\r", final_newline=True):
    lines = [
        "A New World",
        "#",
        "Description line",
        "#",
        "Country one\n^^Difficulty: Easy",
        "#",
        "Country two",
        "#",
        "Country three",
        "#",
        "Country four",
        "#",
        "Country five",
        "#",
        "Country six",
        "#",
        "Country seven",
        "# 2 -1 4 3 2 4 1 0",
    ]
    text = newline.join(line.replace("\n", newline) for line in lines)
    return text + (newline if final_newline else "")


@pytest.mark.parametrize("newline", ["\r", "\n", "\r\n"])
def test_parses_all_sections_and_newline_styles(newline):
    info = ScenarioInfo.parse(sample_text(newline))

    assert info.title == "A New World"
    assert info.overview == "Description line"
    assert len(info.country_sections) == 7
    assert info.country_sections[0] == "Country one\n^^Difficulty: Easy"
    assert info.metadata == [2, -1, 4, 3, 2, 4, 1, 0]


def test_unchanged_document_reproduces_original_bytes(tmp_path):
    raw = sample_text("\r\n", final_newline=False).encode("cp1252")
    info = ScenarioInfo.from_bytes(raw)
    output = tmp_path / "copy.inf"

    info.save(str(output))

    assert info.to_bytes() == raw
    assert output.read_bytes() == raw


def test_edit_emits_canonical_cp1252_with_cr_line_endings():
    info = ScenarioInfo.parse(sample_text("\n", final_newline=False))
    info.title = "L'été nouveau"

    encoded = info.to_bytes()

    assert b"L'\xe9t\xe9 nouveau\r#\r" in encoded
    assert b"\n" not in encoded
    assert encoded.endswith(b"\r")


@pytest.mark.parametrize(
    ("text", "message"),
    [
        ("", "exactly 7 country sections"),
        (sample_text().replace("#\rCountry seven\r", ""), "exactly 7 country sections"),
        (sample_text().replace(" 2 -1 4 3 2 4 1 0", " 2 -1"), "exactly 8 integers"),
        (sample_text().replace("# 2 -1", "# x -1"), "decimal integers"),
    ],
)
def test_rejects_malformed_structure(text, message):
    with pytest.raises(ValueError, match=message):
        ScenarioInfo.parse(text)


def test_edit_validation_rejects_wrong_cardinality():
    info = ScenarioInfo.parse(sample_text())
    info.country_sections.pop()

    with pytest.raises(ValueError, match="exactly 7"):
        info.to_bytes()


def test_load_and_save_cp1252(tmp_path):
    source = tmp_path / "source.inf"
    source.write_bytes(sample_text().replace("A New World", "L'été").encode("cp1252"))

    info = ScenarioInfo.load(str(source))
    info.overview = "Révisé"
    destination = tmp_path / "destination.inf"
    info.save(str(destination))

    assert ScenarioInfo.load(str(destination)).overview == "Révisé"
