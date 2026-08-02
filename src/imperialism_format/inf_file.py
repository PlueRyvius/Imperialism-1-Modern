"""Reader/writer for Imperialism's plain-text ``.inf`` scenario descriptions.

The file is the text the player reads when picking a scenario: a title, an
overview, seven per-country briefings, and a trailing line of eight integers
(seven playability codes where ``-1`` means unplayable, then the default player
index). Sections are separated by lines beginning with ``#``, and the shipped
files use bare **CR** line endings.

Writing follows the same rule as the ``.map`` and ``.scn`` writers: **preserve
what you did not interpret.** The original text is kept verbatim and only the
regions you actually edited are spliced back into it, so an untouched file
re-emits byte-for-byte — line endings, blank lines and delimiter spacing
included — without this module having to understand any of them.

Section text is exposed with ``\\n`` endings regardless of what the file uses,
because that is what an editor wants to work in; the file's own endings are
restored on the way out.
"""
from __future__ import annotations

from dataclasses import dataclass, field


def _split_lines(text: str) -> list[tuple[str, str]]:
    """Split into (content, ending) pairs, preserving CR / LF / CRLF exactly.

    ``str.splitlines`` is not usable here: it also breaks on form feed and
    several Unicode separators, which would silently corrupt a cp1252 file that
    happened to contain one.
    """
    lines = []
    index, length = 0, len(text)
    while index < length:
        stop = index
        while stop < length and text[stop] not in "\r\n":
            stop += 1
        content = text[index:stop]
        ending = ""
        if stop < length:
            if text.startswith("\r\n", stop):
                ending, stop = "\r\n", stop + 2
            else:
                ending, stop = text[stop], stop + 1
        lines.append((content, ending))
        index = stop
    return lines


@dataclass
class _Region:
    """A slice of the original text that one editable value came from."""

    start: int
    end: int
    original: str


@dataclass
class ScenarioInfo:
    title: str
    overview: str = ""
    country_sections: list = field(default_factory=list)  # list[str]
    metadata: list = field(default_factory=list)  # list[int]
    raw_text: str = ""

    # Where each value lives in raw_text. Absent when built by hand rather than
    # parsed, in which case to_text() renders from scratch.
    regions: dict = field(default_factory=dict, repr=False)
    line_ending: str = "\r"

    @classmethod
    def load(cls, path: str, encoding: str = "cp1252") -> "ScenarioInfo":
        """Read a scenario description, keeping its line endings intact.

        Deliberately *not* opened in universal-newline mode: translating CR to
        LF on the way in would make a byte-exact round trip impossible.
        """
        with open(path, "rb") as f:
            raw = f.read()
        return cls.parse(raw.decode(encoding, errors="replace"))

    @classmethod
    def parse(cls, text: str) -> "ScenarioInfo":
        lines = _split_lines(text)

        endings = [end for _, end in lines if end]
        line_ending = max(set(endings), key=endings.count) if endings else "\r"

        # Group into blocks of consecutive non-delimiter lines, remembering the
        # span each block occupies so it can be replaced in place later.
        blocks: list[list[int]] = [[]]
        metadata: list[int] = []
        metadata_region = None
        offset = 0
        starts: list[int] = []
        for index, (content, ending) in enumerate(lines):
            if content.startswith("#"):
                suffix = content[1:].strip()
                if suffix:
                    try:
                        metadata = [int(value) for value in suffix.split()]
                        metadata_region = _Region(
                            offset + 1, offset + 1 + len(content) - 1, content[1:])
                    except ValueError:
                        blocks.append([index])
                        starts.append(offset)
                        offset += len(content) + len(ending)
                        continue
                else:
                    blocks.append([])
                    starts.append(offset + len(content) + len(ending))
            else:
                if not blocks[-1] and len(starts) < len(blocks):
                    starts.append(offset)
                blocks[-1].append(index)
            offset += len(content) + len(ending)

        while len(starts) < len(blocks):
            starts.append(len(text))

        def span(block_index):
            block = blocks[block_index]
            if not block:
                start = starts[block_index]
                return start, start
            start = sum(len(c) + len(e) for c, e in lines[:block[0]])
            end = sum(len(c) + len(e) for c, e in lines[:block[-1] + 1])
            return start, end

        # Keep only blocks with real content, exactly as before, but carry each
        # one's index so its span stays reachable.
        kept = []
        for block_index, block in enumerate(blocks):
            body = "\n".join(lines[i][0] for i in block).strip()
            if body:
                kept.append((block_index, body))
        if not kept:
            raise ValueError("scenario info file contains no text")

        regions = {}
        first_index, first_body = kept[0]
        first_lines = first_body.splitlines()
        title = first_lines[0].strip()
        overview = "\n".join(first_lines[1:]).strip()

        # The title is the first line of the first block; give it a region of
        # its own so editing it does not disturb the rest of that block.
        title_line = blocks[first_index][0]
        title_start = sum(len(c) + len(e) for c, e in lines[:title_line])
        regions["title"] = _Region(
            title_start, title_start + len(lines[title_line][0]), lines[title_line][0])

        remaining = kept[1:]
        if overview:
            block_start, block_end = span(first_index)
            after_title = title_start + len(lines[title_line][0]) + len(lines[title_line][1])
            regions["overview"] = _Region(after_title, block_end, overview)
        elif remaining:
            overview_index, overview = remaining[0][0], remaining[0][1]
            regions["overview"] = _Region(*span(overview_index), overview)
            remaining = remaining[1:]

        country_sections = []
        for position, (block_index, body) in enumerate(remaining):
            country_sections.append(body)
            regions[f"country:{position}"] = _Region(*span(block_index), body)

        if metadata_region is not None:
            regions["metadata"] = metadata_region

        return cls(
            title=title,
            overview=overview,
            country_sections=country_sections,
            metadata=metadata,
            raw_text=text,
            regions=regions,
            line_ending=line_ending,
        )

    # --- writing ----------------------------------------------------------

    def _render_block(self, text: str) -> str:
        """Lay a section back out with the file's own line endings."""
        if not text:
            return ""
        return "".join(line + self.line_ending for line in text.split("\n"))

    def to_text(self) -> str:
        """Re-emit the file, splicing in only what changed.

        With no edits this returns the original text unchanged, which is what
        makes a byte-exact round trip possible.
        """
        if not self.regions:
            return self._render_from_scratch()

        edits = []
        title_region = self.regions.get("title")
        if title_region and self.title != title_region.original:
            edits.append((title_region, self.title))

        overview_region = self.regions.get("overview")
        if overview_region and self.overview != overview_region.original:
            edits.append((overview_region, self._render_block(self.overview)))

        for position, body in enumerate(self.country_sections):
            region = self.regions.get(f"country:{position}")
            if region and body != region.original:
                edits.append((region, self._render_block(body)))

        metadata_region = self.regions.get("metadata")
        if metadata_region:
            rendered = " " + " ".join(str(value) for value in self.metadata)
            if rendered.split() != metadata_region.original.split():
                edits.append((metadata_region, rendered))

        out = self.raw_text
        for region, replacement in sorted(edits, key=lambda e: e[0].start, reverse=True):
            out = out[:region.start] + replacement + out[region.end:]
        return out

    def _render_from_scratch(self) -> str:
        """Build a whole file. Only for instances that were never parsed."""
        end = self.line_ending
        parts = [self.title + end, "#" + end]
        if self.overview:
            parts.append(self._render_block(self.overview))
            parts.append("#" + end)
        for body in self.country_sections:
            parts.append(self._render_block(body))
            parts.append("#" + end)
        parts[-1] = "# " + " ".join(str(value) for value in self.metadata) + end
        return "".join(parts)

    def save(self, path: str, encoding: str = "cp1252") -> None:
        with open(path, "wb") as f:
            f.write(self.to_text().encode(encoding, errors="replace"))

    def to_dict(self) -> dict:
        return {
            "title": self.title,
            "overview": self.overview,
            "country_sections": self.country_sections,
            "metadata": self.metadata,
        }
