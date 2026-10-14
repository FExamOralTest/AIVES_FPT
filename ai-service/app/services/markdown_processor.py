"""Prepare relevant course context while preserving code and outcome citations."""

from __future__ import annotations

import re
import unicodedata
from collections import Counter
from dataclasses import dataclass, field
from typing import Callable, List, Optional

from loguru import logger

from app.models.request import DocumentContext


# ─────────────────────────────────────────────
#  Constants
# ─────────────────────────────────────────────

REQUIRED_DOC_TYPES: set[str] = {"course_content", "learning_outcomes"}

_RE_HEADING = re.compile(r"^ {0,3}(#{1,6})[ \t]+(.+?)(?:[ \t]+#+)?[ \t]*$", re.MULTILINE)
_RE_BOLD = re.compile(r"\*{2}(.+?)\*{2}")
_RE_ITALIC = re.compile(r"\*(.+?)\*")
_RE_LINK = re.compile(r"\[([^\]]+)\]\([^)]+\)")
_RE_IMAGE = re.compile(r"!\[([^\]]*)\]\([^)]+\)")
_RE_HTML_TAG = re.compile(r"<[^>]+>")
_RE_HORIZONTAL_RULE = re.compile(r"^[-*_]{3,}\s*$", re.MULTILINE)
_RE_BLOCKQUOTE = re.compile(r"^>\s?", re.MULTILINE)
_RE_TABLE_ROW = re.compile(r"^\|.+\|$", re.MULTILINE)
_RE_TABLE_SEPARATOR = re.compile(r"^\|[-| :]+\|$", re.MULTILINE)
_RE_MULTI_BLANK = re.compile(r"\n{3,}")
_RE_TRAILING_SPACE = re.compile(r"[ \t]+$", re.MULTILINE)
_RE_FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})(.*)$")
_RE_CODE_SPAN = re.compile(r"(?<!`)(`+)(?!`)(.+?)(?<!`)\1(?!`)", re.DOTALL)
_STOP_WORDS = set(
    "a an and are as at be by can for from how in is it of on or that the this to "
    "what which with explain describe discuss use using là và của các những một "
    "trong cho về với được có hãy thế nào gì giải thích trình bày sử dụng".split()
)


def _markdown_blocks(text: str) -> list[tuple[bool, str]]:
    blocks: list[tuple[bool, str]] = []
    lines: list[str] = []
    fence = ""
    indented = False

    def flush(code: bool) -> None:
        if lines:
            blocks.append((code, "".join(lines)))
            lines.clear()

    for line in text.splitlines(keepends=True):
        if fence:
            lines.append(line)
            if re.fullmatch(rf" {{0,3}}{re.escape(fence[0])}{{{len(fence)},}}[ \t]*", line.rstrip("\n")):
                flush(True)
                fence = ""
            continue
        match = _RE_FENCE.match(line)
        if match and (match[1][0] == "~" or "`" not in match[2]):
            flush(indented)
            indented = False
            fence = match[1]
            lines.append(line)
        elif line.startswith(("    ", "\t")) or (indented and not line.strip()):
            if not indented:
                flush(False)
                indented = True
            lines.append(line)
        else:
            if indented:
                flush(True)
                indented = False
            lines.append(line)
    flush(bool(fence) or indented)
    return blocks


def _terms(text: str) -> set[str]:
    text = unicodedata.normalize("NFKC", text).casefold()
    return set(re.findall(r"\w+", text)) - _STOP_WORDS


def _protect_code_spans(text: str) -> tuple[str, Callable[[str], str]]:
    spans: list[str] = []
    marker = "\x00MDCODE\x00"
    while marker in text:
        marker += "\x01"

    def protect(match: re.Match) -> str:
        spans.append(match[0])
        return f"{marker}{len(spans) - 1}{marker}"

    def restore(value: str) -> str:
        return re.sub(
            re.escape(marker) + r"(\d+)" + re.escape(marker),
            lambda match: spans[int(match[1])], value,
        )

    return _RE_CODE_SPAN.sub(protect, text), restore


@dataclass
class ContextChunk:
    heading: str
    body: str

    @property
    def text(self) -> str:
        return f"{self.heading}\n{self.body}" if self.heading else self.body


# Learning outcome patterns (e.g. "CLO1:", "LO 2.", "G3 –", "CĐR 4:")
_RE_LO_ITEM = re.compile(
    r"^(?:CLO|LO|G|CĐR|Outcome|Chuẩn đầu ra)\s*\d+\s*[:\-–.]?\s*(.+)$",
    re.IGNORECASE | re.MULTILINE,
)
# Bullet / numbered list lines
_RE_BULLET = re.compile(r"^\s*[-*+]\s+(.+)$", re.MULTILINE)
_RE_NUMBERED = re.compile(r"^\s*\d+[.)]\s+(.+)$", re.MULTILINE)


# ─────────────────────────────────────────────
#  Data classes
# ─────────────────────────────────────────────


@dataclass
class HeadingNode:
    """A heading extracted from a markdown document."""

    level: int
    text: str


@dataclass
class ExtractedStructure:
    """
    Structured representation of a single .md document after processing.
    Used internally before building the final context string.
    """

    document_id: str
    title: str
    document_type: str  # 'course_content' | 'learning_outcomes'

    headings: List[HeadingNode] = field(default_factory=list)
    learning_outcomes: List[str] = field(default_factory=list)  # only for LO doc
    key_topics: List[str] = field(default_factory=list)  # bullet/numbered items
    clean_body: str = ""

    # Metadata
    raw_char_count: int = 0
    clean_char_count: int = 0
    heading_count: int = 0
    lo_count: int = 0
    is_valid: bool = True
    validation_errors: List[str] = field(default_factory=list)


@dataclass
class ProcessingResult:
    """
    Final output of the MarkdownProcessor.
    Contains both the structured objects and the assembled context string
    ready to be inserted into any AI prompt template.
    """

    is_valid: bool
    validation_errors: List[str]
    course_content: Optional[ExtractedStructure]
    learning_outcomes: Optional[ExtractedStructure]
    context_string: str  # assembled string for AI prompt


# ─────────────────────────────────────────────
#  Core Processor
# ─────────────────────────────────────────────


class MarkdownProcessor:
    """
    Reads, processes and normalises content from 2 .md documents
    (course_content + learning_outcomes) and builds a rich context string
    for the AI question/scoring/followup prompts.

    Usage:
        processor = MarkdownProcessor()
        result = processor.process(documents)
        context_for_prompt = result.context_string
    """

    # ── Public API ────────────────────────────────────────────────────────────

    def process(
        self, documents: List[DocumentContext], focus_terms: list[str] | None = None
    ) -> ProcessingResult:
        """
        Main entry point. Validates, parses, and assembles context.

        Args:
            documents: List of DocumentContext objects from the API request.
                       Expected: one 'course_content' + one 'learning_outcomes'.

        Returns:
            ProcessingResult with context_string ready for AI prompts.
        """
        logger.info(
            f"[MarkdownProcessor] Processing {len(documents)} document(s): "
            f"{[d.document_type for d in documents]}"
        )

        # Step 1: Validate input
        validation_errors = self._validate_documents(documents)
        if validation_errors:
            logger.warning(
                f"[MarkdownProcessor] Validation failed: {validation_errors}"
            )
            return ProcessingResult(
                is_valid=False,
                validation_errors=validation_errors,
                course_content=None,
                learning_outcomes=None,
                context_string="",
            )

        # Step 2: Process each document
        doc_map = {d.document_type: d for d in documents}
        cc_struct = self._process_document(doc_map["course_content"])
        lo_struct = self._process_document(doc_map["learning_outcomes"])

        # Step 3: Collect per-document validation errors
        all_errors = cc_struct.validation_errors + lo_struct.validation_errors
        is_valid = cc_struct.is_valid and lo_struct.is_valid

        # Step 4: Build context string
        context_string = self._build_context_string(cc_struct, lo_struct, focus_terms)

        logger.info(
            f"[MarkdownProcessor] Done. valid={is_valid}, "
            f"cc_headings={cc_struct.heading_count}, "
            f"lo_count={lo_struct.lo_count}, "
            f"context_len={len(context_string)} chars"
        )

        return ProcessingResult(
            is_valid=is_valid,
            validation_errors=all_errors,
            course_content=cc_struct,
            learning_outcomes=lo_struct,
            context_string=context_string,
        )

    def prepare_documents(
        self, documents: List[DocumentContext], focus_terms: list[str] | None = None
    ) -> List[DocumentContext]:
        return [
            doc.model_copy(update={"content": self.select_context(
                self._document_body(doc), focus_terms,
                max_chars=2000 if doc.document_type == "learning_outcomes" else 4000,
                preserve_source=doc.document_type == "learning_outcomes",
            )})
            for doc in documents
        ]

    def select_context(
        self, text: str, focus_terms: list[str] | None = None, max_chars: int = 4000,
        preserve_source: bool = False,
    ) -> str:
        """Select complete, relevant chunks within a soft character budget."""
        if len(text) <= max_chars:
            return text
        chunks: list[ContextChunk] = []
        headings: list[tuple[int, str]] = []

        def add(body: str) -> None:
            if body.strip():
                chunks.append(ContextChunk(" > ".join(h[1] for h in headings), body.rstrip("\n")))

        for code, block in _markdown_blocks(text):
            if code:
                heading = " > ".join(h[1] for h in headings)
                if chunks and chunks[-1].heading == heading:
                    block = chunks.pop().body + "\n\n" + block
                add(block)
                continue
            block, restore = _protect_code_spans(block)
            paragraph: list[str] = []

            def flush() -> None:
                body = restore("\n".join(paragraph).strip())
                paragraph.clear()
                if not body:
                    return
                if _RE_CODE_SPAN.search(body):
                    add(body)
                    return
                parts = re.split(r"(?<=[.!?])\s+|\n", body)
                current = ""
                for part in parts:
                    # Split long prose at whitespace; code blocks remain atomic.
                    pieces = re.findall(r".{1,800}(?:\s+|$)|\S+", part)
                    for piece in pieces:
                        piece = piece.strip()
                        if current and len(current) + len(piece) + 1 > 800:
                            add(current)
                            current = ""
                        current = f"{current}\n{piece}".strip()
                add(current)

            for line in block.splitlines():
                heading = _RE_HEADING.fullmatch(line)
                if heading:
                    flush()
                    level = len(heading[1])
                    while headings and headings[-1][0] >= level:
                        headings.pop()
                    headings.append((level, restore(heading[2].strip())))
                elif not line.strip():
                    flush()
                else:
                    paragraph.append(line)
            flush()

        if not chunks:
            chunks = [ContextChunk("", line) for line in text.splitlines() if line.strip()]
        if not chunks:
            return ""
        queries = [_terms(term) for term in focus_terms or [] if term.strip()]
        queries = [query for query in queries if query]
        chunk_terms = [_terms(chunk.text) for chunk in chunks]
        frequencies = Counter(term for terms in chunk_terms for term in terms)
        weights = {term: 1 + len(chunks) / frequency for term, frequency in frequencies.items()}
        scores = [
            [sum(weights.get(term, 1) for term in query & terms)
             / sum(weights.get(term, 1) for term in query)
             + 0.5 * len(query & _terms(chunk.heading)) / len(query)
             for query in queries]
            for chunk, terms in zip(chunks, chunk_terms)
        ]
        ranked = sorted(range(len(chunks)), key=lambda i: (-sum(scores[i]), i))
        if queries and any(sum(score) for score in scores):
            best_per_query = [max(range(len(chunks)), key=lambda i: scores[i][q]) for q in range(len(queries))]
            ranked = list(dict.fromkeys(sorted(best_per_query, key=lambda i: -sum(scores[i])) + ranked))
            ranked = [i for i in ranked if sum(scores[i]) > 0]
        else:
            # Interleave sections so an unfocused request covers the whole document.
            sections: dict[str, list[int]] = {}
            for i, chunk in enumerate(chunks):
                sections.setdefault(chunk.heading, []).append(i)
            groups = list(sections.values())

            def coverage_order(count: int) -> list[int]:
                return list(dict.fromkeys([
                    0, count - 1, count // 2, count // 4, 3 * count // 4, *range(count)
                ]))

            if len(groups) == 1:
                ranked = coverage_order(len(chunks))
            else:
                groups = [groups[i] for i in coverage_order(len(groups))]
                ranked = [group[row] for row in range(max(map(len, groups))) for group in groups if row < len(group)]
        selected: list[int] = []
        size = 0
        for i in ranked:
            content = chunks[i].body if preserve_source else chunks[i].text
            cost = len(content) + (2 if selected else 0)
            # Keep an oversized best chunk intact rather than cutting code or a statement.
            if not selected or size + cost <= max_chars:
                selected.append(i)
                size += cost
        return "\n\n".join(
            chunks[i].body if preserve_source else chunks[i].text for i in sorted(selected)
        )

    def _document_body(self, doc: DocumentContext) -> str:
        # Outcomes must remain verbatim for the backend's source validation.
        if doc.document_type == "learning_outcomes":
            return doc.content.replace("\r\n", "\n").replace("\r", "\n")
        return self._clean_markdown(doc.content)

    # ── Step 1: Validate ─────────────────────────────────────────────────────

    def _validate_documents(self, documents: List[DocumentContext]) -> List[str]:
        """Check that required document types are present and non-empty."""
        errors: List[str] = []

        if not documents:
            errors.append("No documents provided.")
            return errors

        found_types = {d.document_type for d in documents}
        missing = REQUIRED_DOC_TYPES - found_types
        if missing:
            errors.append(
                f"Missing required document type(s): {', '.join(sorted(missing))}. "
                f"Expected: {', '.join(sorted(REQUIRED_DOC_TYPES))}."
            )

        for doc in documents:
            if not doc.content or not doc.content.strip():
                errors.append(
                    f"Document '{doc.document_type}' (id={doc.document_id}) "
                    f"has empty content."
                )
            if not doc.title or not doc.title.strip():
                errors.append(
                    f"Document '{doc.document_type}' (id={doc.document_id}) "
                    f"has no title."
                )

        return errors

    # ── Step 2-4: Per-document pipeline ──────────────────────────────────────

    def _process_document(self, doc: DocumentContext) -> ExtractedStructure:
        """Full pipeline for a single document."""
        struct = ExtractedStructure(
            document_id=doc.document_id,
            title=doc.title.strip(),
            document_type=doc.document_type,
            raw_char_count=len(doc.content),
        )

        raw = doc.content

        normalised = self._clean_markdown(raw)
        prose = "\n".join(block for code, block in _markdown_blocks(normalised) if not code)
        prose = _RE_CODE_SPAN.sub(lambda match: "" if "\n" in match[0] else match[0], prose)

        struct.headings = self._extract_headings(prose)
        struct.heading_count = len(struct.headings)

        if doc.document_type == "learning_outcomes":
            struct.learning_outcomes = self._extract_learning_outcomes(prose)
            struct.lo_count = len(struct.learning_outcomes)

        struct.key_topics = self._extract_key_topics(prose)

        clean = self._document_body(doc)
        struct.clean_body = clean
        struct.clean_char_count = len(clean)

        # 2d. Per-document validation
        struct.validation_errors, struct.is_valid = self._validate_structure(struct)

        return struct

    # ── Step 2a: Heading extraction ───────────────────────────────────────────

    def _extract_headings(self, text: str) -> List[HeadingNode]:
        """Extract all ATX-style headings (#, ##, ...) preserving hierarchy."""
        headings: List[HeadingNode] = []
        for match in _RE_HEADING.finditer(text):
            level = len(match.group(1))
            heading_text = match.group(2).strip()
            heading_text = _RE_CODE_SPAN.sub(lambda match: match[2], heading_text)
            headings.append(HeadingNode(level=level, text=heading_text))
        return headings

    # ── Step 2b: Learning outcome extraction ─────────────────────────────────

    def _extract_learning_outcomes(self, text: str) -> List[str]:
        """
        Extract learning outcome items from the LO document.
        Handles common Vietnamese/English LO label formats:
        CLO1, LO 2, G3, CĐR 4, etc.
        Falls back to bullet/numbered items if no labelled LOs found.
        """
        outcomes: List[str] = []

        # Try labelled LO patterns first
        for match in _RE_LO_ITEM.finditer(text):
            item = match.group(1).strip()
            if item:
                outcomes.append(item)

        # Fallback: use all bullet/numbered items as outcomes
        if not outcomes:
            outcomes = self._extract_key_topics(text)

        # Deduplicate while preserving order
        seen: set[str] = set()
        unique: List[str] = []
        for o in outcomes:
            key = o.lower().strip()
            if key not in seen:
                seen.add(key)
                unique.append(o)

        return unique

    # ── Step 2c: Key topic extraction ─────────────────────────────────────────

    def _extract_key_topics(self, text: str) -> List[str]:
        """Extract bullet and numbered list items as key topics."""
        topics: List[str] = []
        for match in _RE_BULLET.finditer(text):
            item = match.group(1).strip()
            # Skip table-of-content style items or very short fragments
            if item and len(item) > 3:
                topics.append(item)
        for match in _RE_NUMBERED.finditer(text):
            item = match.group(1).strip()
            if item and len(item) > 3:
                topics.append(item)
        return topics

    # ── Step 2d: Markdown cleaning ────────────────────────────────────────────

    def _clean_markdown(self, text: str) -> str:
        text = text.replace("\r\n", "\n").replace("\r", "\n")
        cleaned = []
        for code, block in _markdown_blocks(text):
            if code:
                cleaned.append(block)
                continue
            block, restore = _protect_code_spans(block)
            block = unicodedata.normalize("NFC", block)
            block = _RE_IMAGE.sub(r"[image: \1]", block)
            block = _RE_LINK.sub(r"\1", block)
            block = _RE_HTML_TAG.sub("", block)
            block = _RE_HORIZONTAL_RULE.sub("", block)
            block = _RE_BLOCKQUOTE.sub("", block)
            block = _RE_TABLE_SEPARATOR.sub("", block)
            block = _RE_TABLE_ROW.sub(
                lambda match: " | ".join(cell.strip() for cell in match[0].split("|") if cell.strip()), block
            )
            block = _RE_BOLD.sub(r"\1", block)
            block = _RE_ITALIC.sub(r"\1", block)
            block = re.sub(r"(?<!\w)__(.+?)__(?!\w)", r"\1", block)
            block = re.sub(r"(?<!\w)_(.+?)_(?!\w)", r"\1", block)
            block = _RE_MULTI_BLANK.sub("\n\n", _RE_TRAILING_SPACE.sub("", block))
            block = restore(block)
            cleaned.append(block)
        return "".join(cleaned).strip("\n")

    # ── Step 2e: Per-document structural validation ───────────────────────────

    def _validate_structure(
        self, struct: ExtractedStructure
    ) -> tuple[List[str], bool]:
        """Warn if a document is suspiciously thin on content."""
        errors: List[str] = []

        MIN_CHARS = 50
        if struct.clean_char_count < MIN_CHARS:
            errors.append(
                f"Document '{struct.document_type}' seems too short "
                f"({struct.clean_char_count} chars after cleaning). "
                f"Minimum recommended: {MIN_CHARS} chars."
            )

        if struct.document_type == "learning_outcomes" and struct.lo_count == 0:
            # Not an error – fall back to bullet items
            logger.debug(
                "[MarkdownProcessor] No labelled LO items found in "
                f"'{struct.title}'; using bullet items as outcomes."
            )

        is_valid = len(errors) == 0
        return errors, is_valid

    # ── Step 5: Build context string ──────────────────────────────────────────

    def _build_context_string(
        self,
        cc: ExtractedStructure,
        lo: ExtractedStructure,
        focus_terms: list[str] | None = None,
    ) -> str:
        return (
            f"═══ NỘI DUNG MÔN HỌC: {cc.title} ═══\n"
            f"{self.select_context(cc.clean_body, focus_terms, 4000)}\n\n"
            f"═══ CHUẨN ĐẦU RA (LEARNING OUTCOMES): {lo.title} ═══\n"
            f"{self.select_context(lo.clean_body, focus_terms, 2000, preserve_source=True)}\n"
        )
