#!/usr/bin/env python3
"""Generate Visual-Paradigm-importable XMI 2.1 for DIAGRAM 5A and 5B (EN + VI).

HOW TO IMPORT INTO VISUAL PARADIGM
    1. File > Import...  -> type "XMI", pick the .xmi file
    2. Model Explorer shows: AIVES > <diagram name>
    3. Double-click the Interaction -> it opens as a Sequence Diagram
    4. If the canvas is empty: drag the Interaction onto a new Sequence
       Diagram, then right-click > "Auto-layout" / "Arrange Shapes"

NOTE: the XMI carries the MODEL (lifelines + messages), not the colours.
"""
import pathlib
import xml.etree.ElementTree as ET

UML = "http://www.omg.org/spec/UML/20131001"
XMI = "http://www.omg.org/spec/XMI/20131001"
ET.register_namespace("uml", UML)
ET.register_namespace("xmi", XMI)


def u(n):
    return f"{{{UML}}}{n}"


def x(n):
    return f"{{{XMI}}}{n}"


N = 0


def nid(p):
    global N
    N += 1
    return f"{p}{N:03d}"


# ---------------------------------------------------------------- DIAGRAM 5A
A_EN_LINES = [
    ("Student", "Student"),
    ("Web App", "WebApp"),
    ("Backend API (ASP.NET Core)", "BackendAPI"),
    ("DB", "Database"),
    ("Cloudflare R2", "CloudflareR2"),
    ("AI Engine (FastAPI)", "AIEngine"),
]
A_EN_MSGS = [
    ("Student", "WebApp", "1: Open viva room", False),
    ("WebApp", "BackendAPI", "2: Start session", False),
    ("BackendAPI", "Database", "3: Load session + enrollment", False),
    ("Database", "BackendAPI", "4: Session + first question", True),
    ("BackendAPI", "WebApp", "5: First question", True),
    ("WebApp", "Student", "6: Show question", True),
    ("Student", "WebApp", "7: Record answer (audio)", False),
    ("WebApp", "BackendAPI", "8: Init chunked upload", False),
    ("BackendAPI", "WebApp", "9: uploadId", True),
    ("WebApp", "BackendAPI", "10: Upload audio chunks", False),
    ("BackendAPI", "CloudflareR2", "11: Store audio object", False),
    ("BackendAPI", "WebApp", "12: Upload completed (objectKey)", True),
    ("WebApp", "BackendAPI", "13: Submit answer (uploadId)", False),
    ("BackendAPI", "AIEngine", "14: Transcribe speech + generate follow-up", False),
    ("AIEngine", "BackendAPI", "15: Transcript + next question", True),
    ("BackendAPI", "Database", "16: Save answer (PROCESSING) + result (PENDING_REVIEW)", False),
    ("BackendAPI", "WebApp", "17: Feedback + next question", True),
    ("WebApp", "Student", "18: Show feedback + next question", True),
    ("Student", "WebApp", "19: Finish the viva", False),
    ("WebApp", "BackendAPI", "20: Finish session", False),
    ("BackendAPI", "Database", "21: Status = COMPLETED + queue grading job", False),
    ("BackendAPI", "WebApp", "22: Submitted", True),
    ("WebApp", "Student", "23: Submitted", True),
]
A_VI_LINES = [
    ("Sinh viên", "Student"),
    ("Web App", "WebApp"),
    ("Backend API (ASP.NET Core)", "BackendAPI"),
    ("DB", "Database"),
    ("Cloudflare R2", "CloudflareR2"),
    ("AI Engine (FastAPI)", "AIEngine"),
]
A_VI_MSGS = [
    ("Student", "WebApp", "1: Mở phòng thi", False),
    ("WebApp", "BackendAPI", "2: Bắt đầu session", False),
    ("BackendAPI", "Database", "3: Lấy session + enrollment", False),
    ("Database", "BackendAPI", "4: Session + câu hỏi đầu tiên", True),
    ("BackendAPI", "WebApp", "5: Câu hỏi đầu tiên", True),
    ("WebApp", "Student", "6: Hiển thị câu hỏi", True),
    ("Student", "WebApp", "7: Ghi âm câu trả lời", False),
    ("WebApp", "BackendAPI", "8: Khởi tạo upload chunked", False),
    ("BackendAPI", "WebApp", "9: uploadId", True),
    ("WebApp", "BackendAPI", "10: Upload các chunk audio", False),
    ("BackendAPI", "CloudflareR2", "11: Lưu object audio", False),
    ("BackendAPI", "WebApp", "12: Upload hoàn tất (objectKey)", True),
    ("WebApp", "BackendAPI", "13: Nộp câu trả lời (uploadId)", False),
    ("BackendAPI", "AIEngine", "14: Chuyển giọng nói + sinh câu hỏi tiếp theo", False),
    ("AIEngine", "BackendAPI", "15: Transcript + câu hỏi tiếp theo", True),
    ("BackendAPI", "Database", "16: Lưu câu trả lời (PROCESSING) + result (PENDING_REVIEW)", False),
    ("BackendAPI", "WebApp", "17: Feedback + câu hỏi tiếp theo", True),
    ("WebApp", "Student", "18: Hiển thị feedback + câu hỏi tiếp theo", True),
    ("Student", "WebApp", "19: Kết thúc bài thi", False),
    ("WebApp", "BackendAPI", "20: Finish session", False),
    ("BackendAPI", "Database", "21: Status = COMPLETED + xếp hàng job chấm điểm", False),
    ("BackendAPI", "WebApp", "22: Đã nộp bài", True),
    ("WebApp", "Student", "23: Đã nộp bài", True),
]

# ---------------------------------------------------------------- DIAGRAM 5B
B_EN_LINES = [
    ("Finish handler", "FinishHandler"),
    ("Hangfire (queue: grading)", "Hangfire"),
    ("Back-end Service (Grading)", "GradingService"),
    ("AI service", "AIService"),
    ("Result store", "ResultStore"),
    ("Examiner", "Examiner"),
    ("DB", "Database"),
    ("Student", "Student"),
]
B_EN_MSGS = [
    ("Student", "FinishHandler", "1: Finish the viva", False),
    ("FinishHandler", "ResultStore", "2: Save submission", False),
    ("ResultStore", "Database", "3: Persist (PENDING_REVIEW)", False),
    ("Examiner", "ResultStore", "4: Open waiting submissions", False),
    ("ResultStore", "Examiner", "5: List of ungraded results", True),
    ("Examiner", "ResultStore", "6: Authorize AI grading", False),
    ("ResultStore", "Hangfire", "7: Enqueue grading job", False),
    ("Hangfire", "GradingService", "8: Load grading context", False),
    ("GradingService", "AIService", "9: Evaluate answers / rubric", False),
    ("AIService", "GradingService", "10: Score + feedback", True),
    ("GradingService", "ResultStore", "11: Store AI score", False),
    ("ResultStore", "Database", "12: Persist (EVALUATED)", False),
    ("Examiner", "ResultStore", "13: Open evidence / AI result", False),
    ("Examiner", "ResultStore", "14: Save official score", False),
    ("ResultStore", "Database", "15: Status = REVIEWED", False),
    ("Examiner", "ResultStore", "16: Publish result", False),
    ("ResultStore", "Database", "17: Status = PUBLISHED", False),
    ("Student", "ResultStore", "18: View my result", False),
]
B_VI_LINES = [
    ("Finish handler", "FinishHandler"),
    ("Hangfire (queue: grading)", "Hangfire"),
    ("Back-end Service (Grading)", "GradingService"),
    ("AI service", "AIService"),
    ("Result store", "ResultStore"),
    ("Khảo thí", "Examiner"),
    ("DB", "Database"),
    ("Sinh viên", "Student"),
]
B_VI_MSGS = [
    ("Student", "FinishHandler", "1: Kết thúc bài thi", False),
    ("FinishHandler", "ResultStore", "2: Lưu bài nộp", False),
    ("ResultStore", "Database", "3: Ghi (PENDING_REVIEW)", False),
    ("Examiner", "ResultStore", "4: Mở danh sách bài chờ chấm", False),
    ("ResultStore", "Examiner", "5: Danh sách bài chưa chấm", True),
    ("Examiner", "ResultStore", "6: Cho phép chấm AI", False),
    ("ResultStore", "Hangfire", "7: Xếp hàng job chấm điểm", False),
    ("Hangfire", "GradingService", "8: Nạp dữ liệu chấm", False),
    ("GradingService", "AIService", "9: Chấm câu trả lời / rubric", False),
    ("AIService", "GradingService", "10: Điểm + feedback", True),
    ("GradingService", "ResultStore", "11: Lưu điểm AI", False),
    ("ResultStore", "Database", "12: Ghi (EVALUATED)", False),
    ("Examiner", "ResultStore", "13: Mở bằng chứng / kết quả AI", False),
    ("Examiner", "ResultStore", "14: Lưu điểm chính thức", False),
    ("ResultStore", "Database", "15: Status = REVIEWED", False),
    ("Examiner", "ResultStore", "16: Công bố kết quả", False),
    ("ResultStore", "Database", "17: Status = PUBLISHED", False),
    ("Student", "ResultStore", "18: Xem kết quả của mình", False),
]


def build(lifelines, messages, interaction_name, note_body, out_path):
    global N
    N = 0
    model = ET.Element(x("XMI"))
    model.set(x("version"), "2.1")
    doc = ET.SubElement(model, x("Documentation"))
    doc.set("exporter", "AIVES")
    doc.set("exporterVersion", "1.0")

    uml_model = ET.SubElement(model, u("Model"))
    uml_model.set(x("type"), "uml:Model")
    uml_model.set(x("id"), nid("model"))
    uml_model.set("name", "AIVES")

    collab = ET.SubElement(uml_model, u("packagedElement"))
    collab.set(x("type"), "uml:Collaboration")
    collab.set(x("id"), nid("collab"))
    collab.set("name", "VivaCollaboration")

    role_id = {}
    for label, short in lifelines:
        a = ET.SubElement(collab, u("ownedAttribute"))
        a.set(x("type"), "uml:Property")
        a.set(x("id"), nid("role"))
        a.set("name", label)
        role_id[short] = a.get(x("id"))

    interaction = ET.SubElement(uml_model, u("ownedBehavior"))
    interaction.set(x("type"), "uml:Interaction")
    interaction.set(x("id"), nid("interaction"))
    interaction.set("name", interaction_name)

    life_id = {}
    for label, short in lifelines:
        ll = ET.SubElement(interaction, u("lifeline"))
        ll.set(x("type"), "uml:Lifeline")
        ll.set(x("id"), nid("lifeline"))
        ll.set("name", label)
        ll.set("represents", role_id[short])
        life_id[short] = ll.get(x("id"))

    for i, (s, r, label, reply) in enumerate(messages, start=1):
        send = ET.SubElement(interaction, u("fragment"))
        send.set(x("type"), "uml:MessageOccurrenceSpecification")
        send.set(x("id"), nid("send"))
        send.set("name", f"send {label}")
        send.set("covered", life_id[s])
        recv = ET.SubElement(interaction, u("fragment"))
        recv.set(x("type"), "uml:MessageOccurrenceSpecification")
        recv.set(x("id"), nid("recv"))
        recv.set("name", f"recv {label}")
        recv.set("covered", life_id[r])
        m = ET.SubElement(interaction, u("message"))
        m.set(x("type"), "uml:Message")
        m.set(x("id"), nid("msg"))
        m.set("name", f"{i}. {label}")
        m.set("messageSort", "reply" if reply else "synchCall")
        m.set("sendEvent", send.get(x("id")))
        m.set("receiveEvent", recv.get(x("id")))

    note = ET.SubElement(interaction, u("ownedComment"))
    note.set(x("type"), "uml:Comment")
    note.set(x("id"), nid("note"))
    note.set("body", note_body)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    tree = ET.ElementTree(model)
    ET.indent(tree, space="  ")
    tree.write(out_path, encoding="utf-8", xml_declaration=True)
    print("wrote", out_path, f"({len(messages)} messages, {len(lifelines)} lifelines)")


base = pathlib.Path("docs/architecture/simplified")

build(
    A_EN_LINES, A_EN_MSGS,
    "DIAGRAM 5A - Live Viva Exam (AIVES)",
    "The AI interviewer may ask a follow-up after every answer, depending on the rubric. "
    "Internal Backend-AI protocol: POST /ai/speech/transcribe-object and "
    "POST /ai/interview/follow-up with the header X-Internal-Token "
    "(= AI_SERVICE_SECRET_KEY), base URL http://ai-service:8000, timeout 300s.",
    base / "DIAGRAM_05A_VIVA_EXAM_EN.xmi",
)
build(
    A_VI_LINES, A_VI_MSGS,
    "DIAGRAM 5A - Live Viva Exam (AIVES)",
    "AI co the hoi follow-up sau moi cau tra loi, tuy theo rubric cua ca thi. "
    "Giao thuc noi bo Backend - AI: POST /ai/speech/transcribe-object va "
    "POST /ai/interview/follow-up voi header X-Internal-Token "
    "(= AI_SERVICE_SECRET_KEY), base URL http://ai-service:8000, timeout 300s.",
    base / "DIAGRAM_05A_VIVA_EXAM.xmi",
)
build(
    B_EN_LINES, B_EN_MSGS,
    "DIAGRAM 5B - AI Scoring & Examiner Review (AIVES)",
    "The AI never grades on its own - the examiner must authorize the grading first. "
    "Internal Backend-AI protocol: POST /ai/evaluate/session with the header "
    "X-Internal-Token (= AI_SERVICE_SECRET_KEY), base URL http://ai-service:8000, timeout 300s. "
    "Hangfire queue: storage in the PostgreSQL schema hangfire; server aives-grading-* runs 1 worker "
    "on the grading queue (aives-api-* handles default / answers); retry 30s -> 120s -> 300s, "
    "max 3 attempts (BR-17).",
    base / "DIAGRAM_05B_GRADING_REVIEW_EN.xmi",
)
build(
    B_VI_LINES, B_VI_MSGS,
    "DIAGRAM 5B - AI Scoring & Examiner Review (AIVES)",
    "AI khong tu cham. Chi khi khai thi cho phep cham AI thi job moi duoc xep hang. "
    "Giao thuc noi bo Backend - AI: POST /ai/evaluate/session voi header "
    "X-Internal-Token (= AI_SERVICE_SECRET_KEY), base URL http://ai-service:8000, timeout 300s. "
    "Hangfire queue: luu job trong schema hangfire cua PostgreSQL; server aives-grading-* chay 1 worker "
    " tren queue grading (aives-api-* xu ly queue default / answers); retry 30s -> 120s -> 300s, "
    "toi da 3 lan (BR-17).",
    base / "DIAGRAM_05B_GRADING_REVIEW.xmi",
)
