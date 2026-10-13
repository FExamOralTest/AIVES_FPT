"""Ground feedback in the transcript and the approved rubric."""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass

from app.models.feedback import CriterionAnalysis, FeedbackAnalysisOutput, SentenceFeedback
from app.models.request import AnswerRubricCriterion

_SENTENCE_END = re.compile(r"[^.!?…。]+[.!?…。]*", re.UNICODE)
_WORD = re.compile(r"\w+", re.UNICODE)
_VIETNAMESE = re.compile(
    r"[ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]",
    re.IGNORECASE,
)
_INJECTION = re.compile(
    r"(?i)("
    r"ignore (?:the |all )?(?:previous |prior )?(?:instructions|rubric|prompt)|"
    r"disregard (?:the )?(?:rubric|instructions|prompt)|"
    r"award (?:me )?(?:full|maximum|max) (?:marks|score|points)|"
    r"give (?:me )?(?:full|maximum|max) (?:marks|score|points)|"
    r"you are now|"
    r"bỏ qua (?:tiêu chí|rubric|hướng dẫn)|"
    r"cho (?:tôi )?điểm tối đa|"
    r"đừng chấm theo"
    r")"
)
_STOPWORDS = frozenset(
    """
    a an the of to and or in on for with is are was were be this that these those it its as by
    from at into over how what why which you your please explain describe discuss they them their
    each other than then can may not but about through only just using used use between across
    there here when where who will would could should have has had been being such also more most
    some any own words answer student whats does did do so if we our
    và hoặc là các của trong cho với được một những về theo khi thì nếu có không hãy em bạn này
    đó từ đến qua như thế nào đã sẽ bị do vì nên vào để cũng hơn rất lại trình bày nêu giải thích
    """.split()
)
_MET_RATIO = 0.75


def normalize(text: str) -> str:
    return " ".join(unicodedata.normalize("NFKC", text).split())


def uses_vietnamese(*parts: str) -> bool:
    return bool(_VIETNAMESE.search(" ".join(parts)))


def is_injection(text: str) -> bool:
    return bool(_INJECTION.search(text))


def contains_quote(haystack: str, quote: str) -> bool:
    normalized_quote = normalize(quote)
    return bool(normalized_quote) and normalized_quote in normalize(haystack)


def split_sentences(text: str) -> list[str]:
    stripped = text.strip()
    if not stripped:
        return []
    sentences: list[str] = []
    for block in re.split(r"[\n\r]+", stripped):
        protected = re.sub(r"(\d)\.(\d)", r"\1§\2", block)
        for match in _SENTENCE_END.finditer(protected):
            sentence = match.group(0).replace("§", ".").strip()
            if sentence:
                sentences.append(sentence)
    return sentences or [stripped]


def significant_tokens(text: str) -> list[str]:
    tokens: list[str] = []
    for token in _WORD.findall(unicodedata.normalize("NFKC", text).casefold()):
        if len(token) > 2 and token not in _STOPWORDS and token not in tokens:
            tokens.append(token)
    return tokens


def _token_match(left: str, right: str) -> bool:
    if left == right:
        return True
    short, long = (left, right) if len(left) <= len(right) else (right, left)
    if len(short) >= 5 and long.startswith(short):
        return True
    return len(short) >= 6 and len(long) >= 6 and short[:6] == long[:6]


def _covers(text: str, required: list[str]) -> list[str]:
    source = significant_tokens(text)
    return [
        token
        for token in required
        if any(_token_match(token, candidate) for candidate in source)
    ]


def _ratio(text: str, required: list[str]) -> float:
    if not required:
        return 0.0
    return len(_covers(text, required)) / len(required)


def _shares(text: str, reference: str) -> bool:
    reference_tokens = significant_tokens(reference)
    return any(
        any(_token_match(token, other) for other in reference_tokens)
        for token in significant_tokens(text)
    )


@dataclass(frozen=True)
class SubmissionPart:
    feedback: str
    learning_outcome: str
    topic: str
    criterion_analyses: list[CriterionAnalysis]


def blank_analysis(rubric: list[AnswerRubricCriterion], vietnamese: bool) -> FeedbackAnalysisOutput:
    if vietnamese:
        feedback = "Bài làm không có transcript để đối chiếu với rubric."
        weakness = "Không có nội dung câu trả lời trong transcript."
        explanation = "Tiêu chí {criterion_id} chưa đạt vì transcript trống."
        improvement = "Bổ sung câu trả lời cho tiêu chí {criterion_id}: {description}"
    else:
        feedback = "The submission has no transcript to compare with the rubric."
        weakness = "The transcript does not contain an answer."
        explanation = "Criterion {criterion_id} is not met because the transcript is empty."
        improvement = "Add an answer for criterion {criterion_id}: {description}"

    analyses: list[CriterionAnalysis] = []
    improvements: list[str] = []
    missing: list[str] = []
    for criterion in rubric:
        analyses.append(
            CriterionAnalysis(
                criterion_id=criterion.criterion_id,
                judgment="not_met",
                explanation=explanation.replace("{criterion_id}", criterion.criterion_id),
                errors=[],
                omissions=[criterion.description],
                evidence_quotes=[],
            )
        )
        improvements.append(
            improvement.replace("{criterion_id}", criterion.criterion_id).replace(
                "{description}", criterion.description
            )
        )
        missing.append(criterion.description)
    return FeedbackAnalysisOutput(
        sentence_feedback=[],
        criterion_analyses=analyses,
        strengths=[],
        weaknesses=[weakness],
        missing_content=missing,
        improvements=improvements,
        feedback=feedback,
    )


def build_mock_analysis(payload: dict) -> dict:
    rubric = [
        AnswerRubricCriterion.model_validate(criterion) for criterion in payload.get("rubric", [])
    ]
    transcript = str(payload.get("transcript") or "")
    sample = str(payload.get("sample_answer") or "")
    question = str(payload.get("question_text") or "")
    learning_outcome = str(payload.get("learning_outcome") or "")
    vietnamese = uses_vietnamese(transcript, sample, question, learning_outcome, *(c.description for c in rubric))
    if not transcript.strip():
        return blank_analysis(rubric, vietnamese).model_dump()

    sentences = split_sentences(transcript)
    required = {criterion.criterion_id: significant_tokens(criterion.description) for criterion in rubric}
    sentence_feedback: list[SentenceFeedback] = []
    for sentence in sentences:
        ratios = [_ratio(sentence, tokens) for tokens in required.values()]
        best = max(ratios, default=0.0)
        if is_injection(sentence):
            judgment = "irrelevant"
            comment = (
                "Câu này tìm cách đổi yêu cầu chấm và không được dùng làm căn cứ."
                if vietnamese
                else "This sentence tries to change the grading task and is not evidence."
            )
        elif best >= _MET_RATIO:
            judgment = "correct"
            comment = (
                "Câu này nêu được ý trong rubric."
                if vietnamese
                else "This sentence states an idea required by the rubric."
            )
        elif best > 0:
            judgment = "omission"
            comment = (
                "Câu này mới đề cập một phần tiêu chí."
                if vietnamese
                else "This sentence addresses only part of a rubric criterion."
            )
        elif _shares(sentence, f"{sample} {question}"):
            judgment = "error"
            comment = (
                "Câu này nói về câu hỏi nhưng mâu thuẫn với rubric."
                if vietnamese
                else "This sentence is about the question but conflicts with the rubric."
            )
        else:
            judgment = "irrelevant"
            comment = (
                "Câu này không dùng làm căn cứ cho rubric."
                if vietnamese
                else "This sentence is not evidence for the rubric."
            )
        sentence_feedback.append(
            SentenceFeedback(sentence=sentence, judgment=judgment, comment=comment)
        )

    judgments = {item.sentence: item.judgment for item in sentence_feedback}
    analyses: list[CriterionAnalysis] = []
    for criterion in rubric:
        tokens = required[criterion.criterion_id]
        evidence = [
            sentence
            for sentence in sentences
            if judgments[sentence] in {"correct", "omission"}
            and not is_injection(sentence)
            and _ratio(sentence, tokens) > 0
        ]
        ratio = _ratio(" ".join(evidence), tokens) if evidence else 0.0
        if tokens and ratio >= _MET_RATIO and all(judgments[sentence] == "correct" for sentence in evidence):
            judgment = "met"
            evidence_quotes = evidence
            errors: list[str] = []
            omissions: list[str] = []
            explanation = (
                f"Tiêu chí {criterion.criterion_id} được transcript nêu đủ."
                if vietnamese
                else f"Criterion {criterion.criterion_id} is covered by the transcript."
            )
        elif tokens and ratio > 0 and evidence:
            judgment = "partial"
            evidence_quotes = evidence
            errors = []
            omissions = [criterion.description]
            explanation = (
                f"Tiêu chí {criterion.criterion_id} mới được đáp ứng một phần."
                if vietnamese
                else f"Criterion {criterion.criterion_id} is only partly covered."
            )
        else:
            judgment = "not_met"
            evidence_quotes = [
                sentence
                for sentence in sentences
                if judgments[sentence] == "error" and not is_injection(sentence)
            ]
            errors = list(evidence_quotes)
            omissions = [criterion.description]
            explanation = (
                f"Tiêu chí {criterion.criterion_id} chưa được transcript đáp ứng."
                if vietnamese
                else f"Criterion {criterion.criterion_id} is not covered by the transcript."
            )
        analyses.append(
            CriterionAnalysis(
                criterion_id=criterion.criterion_id,
                judgment=judgment,
                explanation=explanation,
                errors=errors,
                omissions=omissions,
                evidence_quotes=evidence_quotes,
            )
        )

    strengths = [
        f"{item.criterion_id}: {next(c.description for c in rubric if c.criterion_id == item.criterion_id)}"
        for item in analyses
        if item.judgment == "met"
    ]
    weaknesses = [item.explanation for item in analyses if item.judgment != "met"]
    missing = [omission for item in analyses for omission in item.omissions]
    improvements = [
        (
            f"Bổ sung tiêu chí {item.criterion_id}: {next(c.description for c in rubric if c.criterion_id == item.criterion_id)}"
            if vietnamese
            else f"Address criterion {item.criterion_id}: {next(c.description for c in rubric if c.criterion_id == item.criterion_id)}"
        )
        for item in analyses
        if item.judgment != "met"
    ]
    feedback = " ".join([*(item.comment for item in sentence_feedback), *(item.explanation for item in analyses)])
    return FeedbackAnalysisOutput(
        sentence_feedback=sentence_feedback,
        criterion_analyses=analyses,
        strengths=strengths,
        weaknesses=weaknesses,
        missing_content=missing,
        improvements=improvements,
        feedback=feedback,
    ).model_dump()


def build_submission_feedback(parts: list[SubmissionPart]) -> tuple[str, list[str]]:
    vietnamese = uses_vietnamese(
        " ".join(f"{part.feedback} {part.learning_outcome} {part.topic}" for part in parts)
    )
    met = sum(item.judgment == "met" for part in parts for item in part.criterion_analyses)
    partial = sum(item.judgment == "partial" for part in parts for item in part.criterion_analyses)
    missed = sum(item.judgment == "not_met" for part in parts for item in part.criterion_analyses)
    if vietnamese:
        header = (
            "Nhận xét toàn bài dựa trên rubric và transcript: "
            f"{met} tiêu chí đạt, {partial} tiêu chí đạt một phần, {missed} tiêu chí chưa đạt."
        )
        evidence_label = "Căn cứ từ transcript"
        no_gap = "Không có tiêu chí rubric nào còn thiếu."
    else:
        header = (
            "Submission feedback based on the rubric and transcript: "
            f"{met} criteria met, {partial} partial, and {missed} not met."
        )
        evidence_label = "Transcript evidence"
        no_gap = "No rubric criterion is still missing."

    sections = [header]
    suggestions: list[str] = []
    for part in parts:
        sections.append(part.feedback)
        quotes: list[str] = []
        for criterion in part.criterion_analyses:
            for quote in criterion.evidence_quotes:
                if quote not in quotes:
                    quotes.append(quote)
            if criterion.judgment == "met":
                continue
            subject = part.learning_outcome or part.topic or "the question"
            suggestion = f"{subject} — {criterion.explanation}"
            if suggestion not in suggestions:
                suggestions.append(suggestion)
        if quotes:
            sections.append(f"{evidence_label}: " + " | ".join(quotes))
    sections.append(" ".join(suggestions) if suggestions else no_gap)
    return "\n\n".join(section for section in sections if section), suggestions


def validate_analysis(
    analysis: FeedbackAnalysisOutput,
    transcript: str,
    rubric: list[AnswerRubricCriterion],
) -> FeedbackAnalysisOutput:
    # Quotes must occur in the transcript. Instructions inside the transcript are not evidence.
    sentences = split_sentences(transcript)
    sentence_feedback = _align_sentences(analysis.sentence_feedback, sentences)
    for item in sentence_feedback:
        if is_injection(item.sentence) and item.judgment == "correct":
            raise ValueError("Transcript instructions cannot be graded as correct content.")

    expected = [criterion.criterion_id for criterion in rubric]
    received = [item.criterion_id for item in analysis.criterion_analyses]
    if len(received) != len(set(received)) or sorted(received) != sorted(expected):
        raise ValueError("Feedback must analyze every rubric criterion exactly once.")

    judgments = {item.sentence: item.judgment for item in sentence_feedback}
    by_id = {item.criterion_id: item for item in analysis.criterion_analyses}
    ordered = [_validate_criterion(by_id[criterion.criterion_id], transcript, sentences, judgments) for criterion in rubric]

    if any(item.judgment != "met" for item in ordered) and not analysis.improvements:
        raise ValueError("Feedback must suggest an improvement for unmet rubric criteria.")
    if any(item.judgment == "met" for item in ordered) and not analysis.strengths:
        raise ValueError("Feedback must record a strength for covered rubric criteria.")
    if any(item.judgment != "met" for item in ordered) and not (analysis.weaknesses or analysis.missing_content):
        raise ValueError("Feedback must record the gap for unmet rubric criteria.")

    return analysis.model_copy(update={"sentence_feedback": sentence_feedback, "criterion_analyses": ordered})


def _align_sentences(items: list[SentenceFeedback], sentences: list[str]) -> list[SentenceFeedback]:
    if len(items) != len(sentences):
        raise ValueError("Sentence feedback must match the transcript sentences.")
    remaining = list(items)
    ordered: list[SentenceFeedback] = []
    for sentence in sentences:
        index = next(
            (i for i, item in enumerate(remaining) if normalize(item.sentence) == normalize(sentence)),
            None,
        )
        if index is None:
            raise ValueError("Sentence feedback must match the transcript sentences.")
        ordered.append(remaining.pop(index).model_copy(update={"sentence": sentence}))
    return ordered


def _validate_criterion(
    item: CriterionAnalysis,
    transcript: str,
    sentences: list[str],
    judgments: dict[str, str],
) -> CriterionAnalysis:
    if item.judgment == "met" and (item.errors or item.omissions):
        raise ValueError("A covered criterion cannot also list errors or omissions.")
    if item.judgment == "partial" and not item.omissions:
        raise ValueError("A partly covered criterion must list what is missing.")
    if item.judgment == "not_met" and not (item.errors or item.omissions):
        raise ValueError("An unmet criterion must list an error or an omission.")
    if item.judgment in {"met", "partial"} and not item.evidence_quotes:
        raise ValueError("A met criterion needs a transcript excerpt.")

    allowed = {"met": {"correct"}, "partial": {"correct", "omission"}, "not_met": {"error"}}[item.judgment]
    for quote in item.evidence_quotes:
        if len(_WORD.findall(quote)) < 2 or not contains_quote(transcript, quote):
            raise ValueError("Feedback cites text that is not in the transcript.")
        hosts = [sentence for sentence in sentences if contains_quote(sentence, quote) or contains_quote(quote, sentence)]
        if not hosts:
            raise ValueError("Feedback cites text that is not in the transcript.")
        usable = [sentence for sentence in hosts if not is_injection(sentence)]
        if not usable:
            raise ValueError("Transcript instructions cannot be used as evidence.")
        if not any(judgments[sentence] in allowed for sentence in usable):
            raise ValueError("Feedback evidence does not match the sentence judgment.")
    return item
