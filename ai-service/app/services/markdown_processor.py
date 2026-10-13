"""
AIVES AI Service - Markdown Processor
Reads, cleans, and normalises content from 2 .md files (course_content + learning_outcomes)
to create a rich, structured context string for downstream AI prompts.

Processing pipeline:
  1. Validate   – ensure required document types are present and non-empty
  2. Clean      – strip markdown syntax that adds noise to LLM input
  3. Extract    – pull headings, bullet lists, learning-outcome items into typed structures
  4. Normalise  – deduplicate, trim, collapse excessive whitespace
  5. Build      – assemble a single, well-formatted context string for the AI prompt
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass, field
from typing import List, Optional

from loguru import logger

from app.models.request import DocumentContext


# ─────────────────────────────────────────────
#  Constants
# ─────────────────────────────────────────────

REQUIRED_DOC_TYPES: set[str] = {"course_content", "learning_outcomes"}

# Markdown elements that add noise without semantic value for LLMs
_RE_HEADING = re.compile(r"^(#{1,6})\s+(.+)$", re.MULTILINE)
_RE_BOLD = re.compile(r"\*{2}(.+?)\*{2}")
_RE_ITALIC = re.compile(r"\*(.+?)\*")
_RE_INLINE_CODE = re.compile(r"`([^`]+)`")
_RE_CODE_BLOCK = re.compile(r"```[\s\S]*?```", re.MULTILINE)
_RE_LINK = re.compile(r"\[([^\]]+)\]\([^)]+\)")
_RE_IMAGE = re.compile(r"!\[([^\]]*)\]\([^)]+\)")
_RE_HTML_TAG = re.compile(r"<[^>]+>")
_RE_HORIZONTAL_RULE = re.compile(r"^[-*_]{3,}\s*$", re.MULTILINE)
_RE_BLOCKQUOTE = re.compile(r"^>\s?", re.MULTILINE)
_RE_TABLE_ROW = re.compile(r"^\|.+\|$", re.MULTILINE)
_RE_TABLE_SEPARATOR = re.compile(r"^\|[-| :]+\|$", re.MULTILINE)
_RE_MULTI_BLANK = re.compile(r"\n{3,}")
_RE_TRAILING_SPACE = re.compile(r"[ \t]+$", re.MULTILINE)

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
    clean_body: str = ""  # full cleaned plain-text body

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

    def process(self, documents: List[DocumentContext]) -> ProcessingResult:
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
        context_string = self._build_context_string(cc_struct, lo_struct)

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

        # 2a. Normalise unicode (e.g. composed/decomposed Vietnamese characters)
        normalised = unicodedata.normalize("NFC", raw)

        # 2b. Extract structure BEFORE stripping markdown syntax
        struct.headings = self._extract_headings(normalised)
        struct.heading_count = len(struct.headings)

        if doc.document_type == "learning_outcomes":
            struct.learning_outcomes = self._extract_learning_outcomes(normalised)
            struct.lo_count = len(struct.learning_outcomes)

        struct.key_topics = self._extract_key_topics(normalised)

        # 2c. Clean markdown syntax → plain text
        clean = self._clean_markdown(normalised)
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
            # Strip inline formatting from heading text
            heading_text = _RE_BOLD.sub(r"\1", heading_text)
            heading_text = _RE_ITALIC.sub(r"\1", heading_text)
            heading_text = _RE_INLINE_CODE.sub(r"\1", heading_text)
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
        """
        Strip markdown formatting and return clean plain text suitable
        for insertion into LLM prompts.
        """
        # Remove fenced code blocks (preserve a placeholder)
        text = _RE_CODE_BLOCK.sub("[code block]", text)

        # Remove images (keep alt text)
        text = _RE_IMAGE.sub(r"[image: \1]", text)

        # Convert links to plain text
        text = _RE_LINK.sub(r"\1", text)

        # Remove HTML tags
        text = _RE_HTML_TAG.sub("", text)

        # Convert headings to plain text (remove # markers but keep text)
        text = _RE_HEADING.sub(lambda m: m.group(2).strip(), text)

        # Remove horizontal rules
        text = _RE_HORIZONTAL_RULE.sub("", text)

        # Remove blockquote markers
        text = _RE_BLOCKQUOTE.sub("", text)

        # Remove table separator rows
        text = _RE_TABLE_SEPARATOR.sub("", text)

        # Clean table rows: keep cell text
        def _table_row_to_text(m: re.Match) -> str:
            cells = [c.strip() for c in m.group(0).split("|") if c.strip()]
            return " | ".join(cells)

        text = _RE_TABLE_ROW.sub(_table_row_to_text, text)

        # Strip bold/italic/inline-code markers (keep text)
        text = _RE_BOLD.sub(r"\1", text)
        text = _RE_ITALIC.sub(r"\1", text)
        text = _RE_INLINE_CODE.sub(r"\1", text)

        # Remove trailing spaces per line
        text = _RE_TRAILING_SPACE.sub("", text)

        # Collapse 3+ consecutive blank lines → 2
        text = _RE_MULTI_BLANK.sub("\n\n", text)

        return text.strip()

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
    ) -> str:
        """
        Assemble a structured, readable context string from the two processed
        documents, ready for insertion into AI prompt templates as
        {document_context}.

        Format:
            ══ COURSE CONTENT ══
            [heading outline]
            [key topics]
            [clean body excerpt]

            ══ LEARNING OUTCOMES ══
            [numbered LO list]
        """
        sections: List[str] = []

        # ── Course Content ─────────────────────────────────────────────────
        cc_parts: List[str] = []
        cc_parts.append(f"═══ NỘI DUNG MÔN HỌC: {cc.title} ═══")

        if cc.headings:
            outline_lines = []
            for h in cc.headings:
                indent = "  " * (h.level - 1)
                outline_lines.append(f"{indent}• {h.text}")
            cc_parts.append("Cấu trúc chương trình:\n" + "\n".join(outline_lines))

        if cc.key_topics:
            # Deduplicate and limit to top-30 most relevant topics
            unique_topics = list(dict.fromkeys(cc.key_topics))[:30]
            topics_text = "\n".join(f"  - {t}" for t in unique_topics)
            cc_parts.append(f"Chủ đề và nội dung chính:\n{topics_text}")

        # Include a trimmed clean body (limit to 4000 chars to avoid token bloat)
        body_excerpt = cc.clean_body[:4000]
        if len(cc.clean_body) > 4000:
            body_excerpt += "\n... [nội dung đã được rút gọn]"
        cc_parts.append(f"Nội dung chi tiết:\n{body_excerpt}")

        sections.append("\n\n".join(cc_parts))

        # ── Learning Outcomes ──────────────────────────────────────────────
        lo_parts: List[str] = []
        lo_parts.append(f"═══ CHUẨN ĐẦU RA (LEARNING OUTCOMES): {lo.title} ═══")

        if lo.learning_outcomes:
            lo_lines = []
            for idx, outcome in enumerate(lo.learning_outcomes, start=1):
                lo_lines.append(f"  {idx}. {outcome}")
            lo_parts.append("Sau khi hoàn thành môn học, sinh viên có thể:\n" + "\n".join(lo_lines))
        else:
            # No structured LOs found – include clean body
            lo_excerpt = lo.clean_body[:2000]
            if len(lo.clean_body) > 2000:
                lo_excerpt += "\n... [nội dung đã được rút gọn]"
            lo_parts.append(f"Nội dung chuẩn đầu ra:\n{lo_excerpt}")

        sections.append("\n\n".join(lo_parts))

        return "\n\n" + ("\n\n" + "─" * 60 + "\n\n").join(sections) + "\n"
