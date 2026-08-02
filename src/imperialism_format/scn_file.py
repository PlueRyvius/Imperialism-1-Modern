"""Reader/writer for Imperialism's .scn scenario format.

The file is a sequence of variable-length tagged records: a 4-byte ASCII
tag, followed by N big-endian 4-byte integer fields, followed (for the
three "name" tags) by a fixed 64-byte null-padded name string. The file
ends with a bare "TERM" tag (no fields).
"""
from __future__ import annotations

from dataclasses import dataclass, field

NAME_FIELD_SIZE = 64

# tag -> number of 4-byte big-endian integer fields that follow
TAG_FIELD_COUNTS = {
    "cnam": 1, "pnam": 1, "zone": 1, "tech": 2, "year": 1, "tyer": 2,
    "cash": 2, "tran": 2, "capa": 3, "army": 3, "ware": 3, "emba": 3,
    "rela": 3, "trea": 3, "port": 1, "rail": 1, "deve": 2, "civi": 2,
    "labo": 4, "tclr": 1, "tbar": 3, "ship": 4, "coun": 2, "flag": 1,
}

# Tags that carry a trailing 64-byte name string after their int fields.
NAME_TAGS = {"cnam", "pnam", "zone"}


@dataclass
class Record:
    tag: str
    fields: list  # list[int]
    name: str = None  # only set for cnam/pnam/zone
    raw_name_field: bytes = field(default=None, repr=False)

    def field_count(self) -> int:
        return TAG_FIELD_COUNTS.get(self.tag, 0)

    def encoded_name_field(self) -> bytes:
        """Return a lossless name field unless the decoded name was edited."""
        if self.raw_name_field is not None:
            if len(self.raw_name_field) != NAME_FIELD_SIZE:
                raise ValueError("raw name field must be exactly 64 bytes")
            original_name = self.raw_name_field.split(b"\x00", 1)[0].decode(
                "ascii", errors="replace"
            )
            if original_name == (self.name or ""):
                return self.raw_name_field
        try:
            name_bytes = (self.name or "").encode("ascii")
        except UnicodeEncodeError as exc:
            raise ValueError(
                f"name for tag {self.tag!r} must be ASCII: {self.name!r}"
            ) from exc
        return name_bytes[:NAME_FIELD_SIZE].ljust(NAME_FIELD_SIZE, b"\x00")


@dataclass
class ScenarioFile:
    records: list = field(default_factory=list)  # list[Record], excludes trailing TERM
    trailing_bytes: bytes = b""

    @classmethod
    def load(cls, path: str) -> "ScenarioFile":
        with open(path, "rb") as f:
            data = f.read()
        return cls.from_bytes(data)

    @classmethod
    def load_text(cls, path: str) -> "ScenarioFile":
        """Load the editor's CR-delimited plaintext scenario form.

        The shipped files are ASCII.  Reading strictly is intentional: a
        non-ASCII byte is evidence that the format needs further research,
        rather than something to replace silently.
        """
        with open(path, "r", encoding="ascii", newline=None) as f:
            return cls.from_text(f.read())

    @classmethod
    def from_text(cls, text: str) -> "ScenarioFile":
        """Parse one whitespace-delimited scenario record per line.

        CR, LF, and CRLF line endings are accepted.  Name records consume the
        remainder of their line as the name, so names may contain spaces.
        Formatting is not retained; :meth:`to_text` emits a canonical form.
        """
        records = []
        for line_no, raw_line in enumerate(text.splitlines(), start=1):
            line = raw_line.strip()
            if not line:
                continue

            head = line.split(maxsplit=1)
            tag = head[0]
            remainder = head[1] if len(head) == 2 else ""
            count = TAG_FIELD_COUNTS.get(tag)
            if count is None:
                raise ValueError(f"line {line_no}: unknown tag {tag!r}")

            if tag in NAME_TAGS:
                parts = remainder.split(maxsplit=count)
                expected_parts = count + 1
                if len(parts) != expected_parts:
                    raise ValueError(
                        f"line {line_no}: tag {tag!r} expects {count} integer "
                        "fields followed by a name"
                    )
                raw_fields = parts[:count]
                name = parts[count].rstrip()
                if not name:
                    raise ValueError(f"line {line_no}: tag {tag!r} requires a name")
            else:
                raw_fields = remainder.split()
                if len(raw_fields) != count:
                    raise ValueError(
                        f"line {line_no}: tag {tag!r} expects {count} integer "
                        f"fields, got {len(raw_fields)}"
                    )
                name = None

            fields = []
            for field_no, raw_value in enumerate(raw_fields, start=1):
                try:
                    value = int(raw_value, 10)
                except ValueError as exc:
                    raise ValueError(
                        f"line {line_no}: field {field_no} for tag {tag!r} "
                        f"is not a decimal integer: {raw_value!r}"
                    ) from exc
                if not 0 <= value <= 0xFFFFFFFF:
                    raise ValueError(
                        f"line {line_no}: field {field_no} for tag {tag!r} "
                        f"is outside uint32 range: {value}"
                    )
                fields.append(value)

            records.append(Record(tag=tag, fields=fields, name=name))
        return cls(records=records)

    @classmethod
    def from_bytes(cls, data: bytes) -> "ScenarioFile":
        records = []
        offset = 0
        while offset < len(data):
            if len(data) - offset < 4:
                raise ValueError(f"truncated tag at offset {offset}")
            raw_tag = data[offset:offset + 4]
            try:
                tag = raw_tag.decode("ascii")
            except UnicodeDecodeError as exc:
                raise ValueError(f"invalid tag at offset {offset}") from exc
            offset += 4
            if tag == "TERM":
                return cls(records=records, trailing_bytes=data[offset:])
            count = TAG_FIELD_COUNTS.get(tag)
            if count is None:
                raise ValueError(f"unknown tag {tag!r} at offset {offset - 4}")
            field_bytes = count * 4
            if len(data) - offset < field_bytes:
                raise ValueError(f"truncated fields for tag {tag!r} at offset {offset}")
            fields = []
            for _ in range(count):
                fields.append(int.from_bytes(data[offset:offset + 4], "big"))
                offset += 4
            name = None
            raw_name_field = None
            if tag in NAME_TAGS:
                if len(data) - offset < NAME_FIELD_SIZE:
                    raise ValueError(f"truncated name for tag {tag!r} at offset {offset}")
                raw_name = data[offset:offset + NAME_FIELD_SIZE]
                offset += NAME_FIELD_SIZE
                name = raw_name.split(b"\x00", 1)[0].decode("ascii", errors="replace")
                raw_name_field = raw_name
            records.append(
                Record(
                    tag=tag,
                    fields=fields,
                    name=name,
                    raw_name_field=raw_name_field,
                )
            )
        raise ValueError("scenario is missing terminating TERM tag")

    def to_bytes(self) -> bytes:
        chunks = []
        for rec in self.records:
            expected = TAG_FIELD_COUNTS.get(rec.tag)
            if expected is None:
                raise ValueError(f"unknown tag {rec.tag!r}")
            if len(rec.fields) != expected:
                raise ValueError(
                    f"tag {rec.tag!r} expects {expected} fields, "
                    f"got {len(rec.fields)}"
                )
            chunks.append(rec.tag.encode("ascii"))
            for value in rec.fields:
                if not 0 <= int(value) <= 0xFFFFFFFF:
                    raise ValueError(f"field value {value!r} is outside uint32 range")
                chunks.append(int(value).to_bytes(4, "big"))
            if rec.tag in NAME_TAGS:
                chunks.append(rec.encoded_name_field())
        chunks.extend((b"TERM", self.trailing_bytes))
        return b"".join(chunks)

    def to_text(self) -> str:
        """Return canonical ASCII text using CR line endings.

        Binary-only details such as name-field padding, trailing bytes, and
        the ``TERM`` sentinel have no representation in the editor format.
        """
        lines = []
        for rec in self.records:
            expected = TAG_FIELD_COUNTS.get(rec.tag)
            if expected is None:
                raise ValueError(f"unknown tag {rec.tag!r}")
            if len(rec.fields) != expected:
                raise ValueError(
                    f"tag {rec.tag!r} expects {expected} fields, "
                    f"got {len(rec.fields)}"
                )

            parts = [rec.tag]
            for value in rec.fields:
                if not 0 <= int(value) <= 0xFFFFFFFF:
                    raise ValueError(f"field value {value!r} is outside uint32 range")
                parts.append(str(int(value)))

            if rec.tag in NAME_TAGS:
                if rec.name is None or not rec.name.strip():
                    raise ValueError(f"tag {rec.tag!r} requires a name")
                if "\r" in rec.name or "\n" in rec.name:
                    raise ValueError(f"name for tag {rec.tag!r} cannot contain a newline")
                rec.name.encode("ascii")
                parts.append(rec.name.strip())

            lines.append(" ".join(parts))
        return "\r".join(lines) + ("\r" if lines else "")

    def save(self, path: str) -> None:
        with open(path, "wb") as f:
            f.write(self.to_bytes())

    def save_text(self, path: str) -> None:
        with open(path, "wb") as f:
            f.write(self.to_text().encode("ascii"))

    def add(self, tag: str, *fields: int, name: str = None) -> Record:
        rec = Record(tag=tag, fields=list(fields), name=name)
        expected = TAG_FIELD_COUNTS.get(tag)
        if expected is None:
            raise ValueError(f"unknown tag {tag!r}")
        if len(rec.fields) != expected:
            raise ValueError(f"tag {tag!r} expects {expected} fields, got {len(rec.fields)}")
        if tag in NAME_TAGS and name is None:
            raise ValueError(f"tag {tag!r} requires a name")
        self.records.append(rec)
        return rec

    def find(self, tag: str) -> list:
        return [r for r in self.records if r.tag == tag]
