"""Lossless reader and editable writer for ``.inf`` scenario descriptions."""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path


COUNTRY_SECTION_COUNT = 7
METADATA_VALUE_COUNT = 8


def _normalize_section(lines: list[str]) -> str:
    return "\n".join(lines).strip()


@dataclass
class ScenarioInfo:
    title: str
    overview: str = ""
    country_sections: list[str] = field(default_factory=list)
    metadata: list[int] = field(default_factory=list)
    raw_text: str = field(default="", repr=False)
    _raw_bytes: bytes | None = field(default=None, repr=False)
    _original_state: tuple | None = field(default=None, repr=False)

    def __post_init__(self) -> None:
        if self._original_state is None and self._raw_bytes is not None:
            self._original_state = self._state()

    @classmethod
    def load(cls, path: str, encoding: str = "cp1252") -> "ScenarioInfo":
        if encoding.lower().replace("-", "") not in {"cp1252", "windows1252"}:
            raise ValueError(".inf files are encoded as CP1252")
        return cls.from_bytes(Path(path).read_bytes())

    @classmethod
    def from_bytes(cls, data: bytes) -> "ScenarioInfo":
        text = data.decode("cp1252")
        return cls._parse(text, raw_bytes=bytes(data))

    @classmethod
    def parse(cls, text: str) -> "ScenarioInfo":
        return cls._parse(text, raw_bytes=text.encode("cp1252"))

    @classmethod
    def _parse(cls, text: str, raw_bytes: bytes) -> "ScenarioInfo":
        normalized = text.replace("\r\n", "\n").replace("\r", "\n")
        blocks: list[list[str]] = [[]]
        metadata: list[int] | None = None

        for line_no, line in enumerate(normalized.split("\n"), start=1):
            if line.startswith("#"):
                suffix = line[1:].strip()
                if suffix:
                    if metadata is not None:
                        raise ValueError(f"line {line_no}: duplicate metadata record")
                    try:
                        metadata = [int(value, 10) for value in suffix.split()]
                    except ValueError as exc:
                        raise ValueError(
                            f"line {line_no}: metadata must contain decimal integers"
                        ) from exc
                else:
                    if metadata is not None:
                        raise ValueError(f"line {line_no}: section follows metadata")
                    blocks.append([])
                continue

            if metadata is not None:
                if line.strip():
                    raise ValueError(f"line {line_no}: text follows metadata")
                continue
            blocks[-1].append(line)

        if len(blocks) != 2 + COUNTRY_SECTION_COUNT:
            raise ValueError(
                "scenario info must contain a title, overview, and exactly "
                f"{COUNTRY_SECTION_COUNT} country sections"
            )
        if metadata is None or len(metadata) != METADATA_VALUE_COUNT:
            actual = 0 if metadata is None else len(metadata)
            raise ValueError(
                f"scenario info metadata must contain exactly {METADATA_VALUE_COUNT} "
                f"integers, got {actual}"
            )

        title_block = _normalize_section(blocks[0])
        if not title_block:
            raise ValueError("scenario info file contains no title")
        if "\n" in title_block:
            raise ValueError("scenario info title must be one line")

        document = cls(
            title=title_block,
            overview=_normalize_section(blocks[1]),
            country_sections=[_normalize_section(block) for block in blocks[2:]],
            metadata=metadata,
            raw_text=text,
            _raw_bytes=raw_bytes,
        )
        document._original_state = document._state()
        return document

    def _state(self) -> tuple:
        return (
            self.title,
            self.overview,
            tuple(self.country_sections),
            tuple(self.metadata),
        )

    def _validate(self) -> None:
        if not self.title.strip() or "\r" in self.title or "\n" in self.title:
            raise ValueError("scenario info title must be one non-empty line")
        if len(self.country_sections) != COUNTRY_SECTION_COUNT:
            raise ValueError(
                f"scenario info requires exactly {COUNTRY_SECTION_COUNT} country sections"
            )
        if len(self.metadata) != METADATA_VALUE_COUNT:
            raise ValueError(
                f"scenario info requires exactly {METADATA_VALUE_COUNT} metadata integers"
            )
        for value in self.metadata:
            if isinstance(value, bool) or not isinstance(value, int):
                raise ValueError("scenario info metadata values must be integers")
        for section in [self.overview, *self.country_sections]:
            if not isinstance(section, str):
                raise ValueError("scenario info sections must be strings")
            for line in section.replace("\r\n", "\n").replace("\r", "\n").split("\n"):
                if line.startswith("#"):
                    raise ValueError("scenario info section lines cannot start with '#'")

    def canonical_text(self) -> str:
        self._validate()

        def canonical_section(value: str) -> str:
            return value.replace("\r\n", "\n").replace("\r", "\n").strip().replace("\n", "\r")

        parts = [self.title.strip(), "#", canonical_section(self.overview)]
        for section in self.country_sections:
            parts.extend(("#", canonical_section(section)))
        parts.append("# " + " ".join(str(value) for value in self.metadata))
        return "\r".join(parts) + "\r"

    def to_bytes(self) -> bytes:
        self._validate()
        if self._raw_bytes is not None and self._original_state == self._state():
            return self._raw_bytes
        return self.canonical_text().encode("cp1252")

    def to_text(self) -> str:
        return self.to_bytes().decode("cp1252")

    def save(self, path: str) -> None:
        Path(path).write_bytes(self.to_bytes())

    def to_dict(self) -> dict:
        return {
            "title": self.title,
            "overview": self.overview,
            "country_sections": self.country_sections,
            "metadata": self.metadata,
        }
