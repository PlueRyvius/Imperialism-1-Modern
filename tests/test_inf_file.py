from imperialism_format import ScenarioInfo


def test_parses_scenario_info_sections_and_metadata():
    raw = (
        "A New World\r#\rDescription line\r#\rCountry one\r^^Difficulty: Easy\r"
        "#\rCountry two\r# 2 -1 4\r"
    )
    info = ScenarioInfo.parse(raw)
    assert info.title == "A New World"
    assert info.overview == "Description line"
    assert info.country_sections == [
        "Country one\n^^Difficulty: Easy",
        "Country two",
    ]
    assert info.metadata == [2, -1, 4]


def test_rejects_empty_info_file():
    try:
        ScenarioInfo.parse("\r\n")
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "no text" in str(exc)
