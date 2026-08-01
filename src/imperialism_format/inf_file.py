"""Parser for Imperialism's plain-text ``.inf`` scenario descriptions."""
from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class ScenarioInfo:
    title: str
    overview: str = ""
    country_sections: list[str] = field(default_factory=list)
    metadata: list[int] = field(default_factory=list)
    raw_text: str = ""

    @classmethod
    def load(cls, path: str, encoding: str = "cp1252") -> "ScenarioInfo":
        with open(path, "r", encoding=encoding, errors="replace", newline=None) as f:
            return cls.parse(f.read())

    @classmethod
    def parse(cls, text: str) -> "ScenarioInfo":
        blocks: list[list[str]] = [[]]
        metadata: list[int] = []

        for line in text.splitlines():
            if line.startswith("#"):
                suffix = line[1:].strip()
                if suffix:
                    try:
                        metadata = [int(value) for value in suffix.split()]
                    except ValueError:
                        blocks.append([suffix])
                else:
                    blocks.append([])
                continue
            blocks[-1].append(line)

        sections = ["\n".join(block).strip() for block in blocks]
        sections = [section for section in sections if section]
        if not sections:
            raise ValueError("scenario info file contains no text")

        first_lines = sections[0].splitlines()
        title = first_lines[0].strip()
        overview = "\n".join(first_lines[1:]).strip()
        remaining_sections = sections[1:]
        if not overview and remaining_sections:
            overview = remaining_sections[0]
            remaining_sections = remaining_sections[1:]
        return cls(
            title=title,
            overview=overview,
            country_sections=remaining_sections,
            metadata=metadata,
            raw_text=text,
        )

    def to_dict(self) -> dict:
        return {
            "title": self.title,
            "overview": self.overview,
            "country_sections": self.country_sections,
            "metadata": self.metadata,
        }
