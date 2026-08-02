"""Reader for the plaintext twin of a ``.scn`` scenario.

Six of the ten scenarios ship an extensionless companion file — ``Scenario/s9``,
``s14``, … — holding the same records as the binary, one per line:

    tech 0 1
    cnam 1 Austrian Empire
    zone 83 Sindel City
    labo 0 2 2 2

Almost certainly the designers' own editor input. Its value here is as an
**oracle**: it states the field count of every record independently of the
binary, so parsing it and comparing against the ``.scn`` checks
``TAG_FIELD_COUNTS`` against something other than our own assumptions.

Line endings are bare CR, and the last line may be unterminated.
"""
from __future__ import annotations

from .scn_file import NAME_TAGS, TAG_FIELD_COUNTS, Record, ScenarioFile


class ScenarioTextError(ValueError):
    """A line that does not fit the tag/fields/name shape."""


def parse_line(line: str, number: int = 0) -> Record | None:
    """Parse one line into a Record, or None if the line is blank.

    ``number`` is only used to make errors locatable.
    """
    stripped = line.strip()
    if not stripped:
        return None

    parts = stripped.split()
    tag = parts[0]
    if tag not in TAG_FIELD_COUNTS:
        raise ScenarioTextError(f"line {number}: unknown tag {tag!r}")

    count = TAG_FIELD_COUNTS[tag]
    if len(parts) - 1 < count:
        raise ScenarioTextError(
            f"line {number}: {tag} needs {count} field(s), got {len(parts) - 1}"
        )
    try:
        fields = [int(p) for p in parts[1:1 + count]]
    except ValueError as exc:
        raise ScenarioTextError(f"line {number}: {tag} has a non-integer field") from exc

    # Everything after the fields is the name. Re-joined from the original text
    # rather than from the split tokens, so runs of spaces inside a name survive.
    rest = stripped.split(maxsplit=1 + count)
    name = rest[1 + count] if len(rest) > 1 + count else None

    if tag in NAME_TAGS:
        if name is None:
            raise ScenarioTextError(f"line {number}: {tag} is missing its name")
    elif name is not None:
        raise ScenarioTextError(
            f"line {number}: {tag} takes {count} field(s) but has trailing text {name!r}"
        )
    return Record(tag=tag, fields=fields, name=name)


def parse(text: str) -> ScenarioFile:
    """Parse the whole text form into the same structure the binary yields."""
    records = []
    for number, line in enumerate(text.splitlines(), start=1):
        record = parse_line(line, number)
        if record is not None:
            records.append(record)
    return ScenarioFile(records=records)


def load(path: str, encoding: str = "cp1252") -> ScenarioFile:
    """Read a plaintext scenario. ``newline=None`` handles the bare-CR endings."""
    with open(path, "r", encoding=encoding, errors="replace", newline=None) as f:
        return parse(f.read())


def to_text(scenario: ScenarioFile, newline: str = "\r") -> str:
    """Render records back to the text form.

    Defaults to the original's bare-CR endings. No trailing newline: the shipped
    files are inconsistent about it, so the caller decides.
    """
    lines = []
    for record in scenario.records:
        parts = [record.tag, *(str(v) for v in record.fields)]
        if record.tag in NAME_TAGS and record.name:
            parts.append(record.name)
        lines.append(" ".join(parts))
    return newline.join(lines)


def differences(text_scenario: ScenarioFile, binary_scenario: ScenarioFile) -> list[str]:
    """Compare a parsed text form against a parsed binary, record by record.

    Returns human-readable descriptions of every mismatch — empty means the two
    agree on every tag, every field and every name.
    """
    problems = []
    a, b = text_scenario.records, binary_scenario.records
    if len(a) != len(b):
        problems.append(f"record count: text {len(a)}, binary {len(b)}")
    for i, (ta, tb) in enumerate(zip(a, b)):
        if ta.tag != tb.tag:
            problems.append(f"[{i}] tag: text {ta.tag!r}, binary {tb.tag!r}")
            continue
        if ta.fields != tb.fields:
            problems.append(f"[{i}] {ta.tag} fields: text {ta.fields}, binary {tb.fields}")
        if (ta.name or "") != (tb.name or ""):
            problems.append(f"[{i}] {ta.tag} name: text {ta.name!r}, binary {tb.name!r}")
    return problems
