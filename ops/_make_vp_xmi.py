#!/usr/bin/env python3
"""Generate Visual-Paradigm-importable XMI 2.1 files for DIAGRAM 5 (EN + VI).

HOW TO IMPORT INTO VISUAL PARADIGM
    1. File > Import...  (or right-click the project > Import...)
    2. Choose "XMI" as the file type, pick the .xmi file
    3. The imported model appears in Model Explorer:
           AIVES
             +- VivaCollaboration      (the 6 roles)
             +- DIAGRAM 5 - ...        (the Interaction = the sequence)
    4. Open the Interaction -> it renders as a Sequence Diagram. If the
       canvas is empty, drag the Interaction onto a new Sequence Diagram,
       or right-click the diagram > "Auto-layout" / "Arrange Shapes".

NOTE: the XMI carries the MODEL (lifelines + messages). The colour code
      (blue=request / green dashed=response / purple=database /
      orange=internal) is applied inside VP on the diagram, not in the XMI.
"""
import pathlib
import xml.etree.ElementTree as ET

UML = "http://www.omg.org/spec/UML/20131001"
XMI = "http://www.omg.org/spec/XMI/20131001"
ET.register_namespace("uml", UML)
ET.register_namespace("xmi", XMI)


def u(name):
    return f"{{{UML}}}{name}"


def x(name):
    return f"{{{XMI}}}{name}"


N = 0


def nid(prefix):
    global N
    N += 1
    return f"{prefix}{N:03d}"


EN_LIFELINES = [
    ("Student", "Student"),
    ("Backend API (ASP.NET Core)", "BackendAPI"),
    ("Hangfire Worker (queue: grading)", "GradingWorker"),
    ("AI Service (FastAPI)", "AIService"),
    ("PostgreSQL", "Database"),
    ("Examiner", "Examiner"),
]

EN_MESSAGES = [
    ("Student", "BackendAPI", "Submit answer (audio)", False),
    ("BackendAPI", "AIService", "Transcribe speech + generate follow-up", False),
    ("AIService", "BackendAPI", "Transcript + next question", True),
    ("BackendAPI", "Database", "Save answer (PROCESSING)", False),
    ("BackendAPI", "Student", "Feedback + next question", True),
    ("Student", "BackendAPI", "Finish the viva", False),
    ("BackendAPI", "Database", "Result = PENDING_REVIEW + queue grading job", False),
    ("BackendAPI", "Student", "Submitted", True),
    ("GradingWorker", "Database", "Load result + answers + rubric", False),
    ("GradingWorker", "AIService", "Evaluate answers against rubric", False),
    ("AIService", "GradingWorker", "Score + feedback per answer", True),
    ("GradingWorker", "Database", "Save AI score + feedback (EVALUATED)", False),
    ("Examiner", "BackendAPI", "Open result (audio + AI score)", False),
    ("BackendAPI", "Examiner", "Answers + AI score + rubric", True),
    ("Examiner", "BackendAPI", "Assign official score", False),
    ("BackendAPI", "Database", "Status = REVIEWED (AI score kept)", False),
    ("Examiner", "BackendAPI", "Publish result", False),
    ("BackendAPI", "Database", "Status = PUBLISHED", False),
    ("BackendAPI", "Examiner", "Published", True),
    ("Student", "BackendAPI", "View my result", False),
    ("BackendAPI", "Student", "Score + examiner comment", True),
]

VI_LIFELINES = [
    ("Sinh viên", "Student"),
    ("Backend API (ASP.NET Core)", "BackendAPI"),
    ("Hangfire Worker (queue: grading)", "GradingWorker"),
    ("AI Service (FastAPI)", "AIService"),
    ("PostgreSQL", "Database"),
    ("Khảo thí", "Examiner"),
]

VI_MESSAGES = [
    ("Student", "BackendAPI", "Nộp câu trả lời (audio)", False),
    ("BackendAPI", "AIService", "Chuyển giọng nói + sinh câu hỏi tiếp theo", False),
    ("AIService", "BackendAPI", "Transcript + câu hỏi tiếp theo", True),
    ("BackendAPI", "Database", "Lưu câu trả lời (PROCESSING)", False),
    ("BackendAPI", "Student", "Feedback + câu hỏi tiếp theo", True),
    ("Student", "BackendAPI", "Kết thúc bài thi", False),
    ("BackendAPI", "Database", "Result = PENDING_REVIEW + xếp hàng job chấm điểm", False),
    ("BackendAPI", "Student", "Đã nộp bài", True),
    ("GradingWorker", "Database", "Lấy result + câu trả lời + rubric", False),
    ("GradingWorker", "AIService", "Chấm điểm theo rubric", False),
    ("AIService", "GradingWorker", "Điểm + feedback từng câu", True),
    ("GradingWorker", "Database", "Lưu điểm AI + feedback (EVALUATED)", False),
    ("Examiner", "BackendAPI", "Mở kết quả (audio + điểm AI)", False),
    ("BackendAPI", "Examiner", "Câu trả lời + điểm AI + rubric", True),
    ("Examiner", "BackendAPI", "Chấm điểm chính thức", False),
    ("BackendAPI", "Database", "Status = REVIEWED (giữ điểm AI)", False),
    ("Examiner", "BackendAPI", "Công bố kết quả", False),
    ("BackendAPI", "Database", "Status = PUBLISHED", False),
    ("BackendAPI", "Examiner", "Đã công bố", True),
    ("Student", "BackendAPI", "Xem kết quả của mình", False),
    ("BackendAPI", "Student", "Điểm + nhận xét khảo thí", True),
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

    # --- the 6 roles the lifelines represent -------------------------
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

    # --- the interaction = the sequence diagram ------------------------
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
    EN_LIFELINES, EN_MESSAGES,
    "DIAGRAM 5 - Simplified Sequence Diagram (AIVES)",
    "Retryable AI error: retry 20s / 60s / 300s (max 4 attempts). "
    "Otherwise AI evaluation = FAILED, answer = ERROR.",
    base / "DIAGRAM_05_SEQUENCE_EN.xmi",
)

build(
    VI_LIFELINES, VI_MESSAGES,
    "DIAGRAM 5 - Sequence Diagram rut gon (AIVES)",
    "Loi AI retryable: thu lai 20s / 60s / 300s (toi da 4 lan). "
    "Con lai: AI evaluation = FAILED, cau tra loi = ERROR.",
    base / "DIAGRAM_05_SEQUENCE.xmi",
)
