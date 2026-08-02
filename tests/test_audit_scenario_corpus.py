from imperialism_format import ScenarioFile
from tools.audit_scenario_corpus import audit_corpus, compare_records, format_report


def scenario(text: str) -> ScenarioFile:
    return ScenarioFile.from_text(text)


def test_pair_relationships_cover_all_classifications():
    exact = compare_records(
        scenario("year 5\ncash 0 100\n"),
        scenario("year 5\ncash 0 100\n"),
        binary_name="s1",
    )
    subset = compare_records(
        scenario("year 5\ncash 0 100\n"),
        scenario("flag 0\nyear 5\ncash 0 100\n"),
        binary_name="s1",
    )
    near = compare_records(
        scenario("year 5\ncash 0 100\n"),
        scenario("year 5\ncash 0 200\n"),
        binary_name="s1",
    )
    unrelated = compare_records(
        scenario("year 5\n"),
        scenario("cash 0 100\n"),
        binary_name="s1",
    )

    assert exact.relationship == "exact"
    assert subset.relationship == "ordered subset"
    assert near.relationship == "near match"
    assert unrelated.relationship == "unrelated"


def test_audit_reports_same_name_and_alternate_best_match(tmp_path):
    (tmp_path / "s1").write_bytes(b"year 5\rcash 0 100\r")
    scenario("year 5\ncash 0 200\n").save(str(tmp_path / "s1.scn"))
    scenario("year 5\ncash 0 100\n").save(str(tmp_path / "s2.scn"))

    (tmp_path / "s3").write_bytes(b"year 5\rcash 0 100\r")
    scenario("flag 0\nyear 5\ncash 0 100\n").save(str(tmp_path / "s3.scn"))

    audit = audit_corpus(tmp_path)
    by_name = {entry["text"]: entry for entry in audit["scenarios"]}

    assert audit["text_files"] == 2
    assert audit["binary_files"] == 3
    assert by_name["s1"]["same_name"]["relationship"] == "near match"
    assert by_name["s1"]["best_match"]["binary"] == "s2"
    assert by_name["s1"]["best_match"]["relationship"] == "exact"
    assert by_name["s3"]["same_name"]["relationship"] == "ordered subset"

    report = format_report(audit)
    assert "Scenario corpus: 2 text files, 3 binary files" in report
    assert "s2" in report
    assert "exact" in report


def test_audit_rejects_missing_inputs(tmp_path):
    try:
        audit_corpus(tmp_path)
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "no extensionless scenario files" in str(exc)
