#!/usr/bin/env python3
"""Generate the English ERD sources from the Vietnamese ones.

The entity / table / column identifiers are physical names and stay identical;
only the human-readable labels, package titles, relation captions and notes are
translated.

Usage:  python docs/architecture/erd/_make_english.py
"""
import pathlib

OUT = pathlib.Path(__file__).parent

# Ordered longest-first so that longer phrases win over shorter substrings.
REPLACEMENTS = {
    # --- titles / headers -----------------------------------------------------
    "ERD 1 - CONCEPTUAL ERD (AIVES) - Muc khai niem":
        "ERD 1 - CONCEPTUAL ERD (AIVES) - Conceptual level",
    "ERD 2 - LOGICAL ERD (AIVES) - Muc logic":
        "ERD 2 - LOGICAL ERD (AIVES) - Logical level",
    "ERD 3 - PHYSICAL ERD (AIVES) - PostgreSQL / Neon":
        "ERD 3 - PHYSICAL ERD (AIVES) - PostgreSQL / Neon",
    "AIVES - CONCEPTUAL ERD (Muc Conceptual - Pho bien nghiep ngh)":
        "AIVES - CONCEPTUAL ERD (Conceptual level - business view)",
    "Muc dich: mo ta nganh nghiep ngh - thuc the va moi quan he":
        "Purpose: describe business entities and their relationships",
    "nghiep ngh, KHONG dua vao khoa/duyet lieu/khoa chinh":
        "without keys, attributes or data types",
    "AIVES - LOGICAL ERD (Muc Logical - Thiet ke luu du lieu)":
        "AIVES - LOGICAL ERD (Logical level - logical data design)",
    "Muc dich: mo ta relation da chuan hoa 3NF, thuoc tinh, khoa chinh":
        "Purpose: describe 3NF-normalised relations, attributes, primary keys",
    "(PK) va khoa ngoai (FK), quy tac nghiep ngh":
        "(PK) and foreign keys (FK), business rules",
    "KHONG dua vao kieu du lieu cu the cu DBMS":
        "independent of any DBMS-specific data type",
    "AIVES - PHYSICAL ERD (Muc Physical - Cai dat Postgres/Neon that)":
        "AIVES - PHYSICAL ERD (Physical level - real PostgreSQL/Neon implementation)",
    "Muc dich: kieu du lieu Postgres cu the, PK/FK, UNIQUE, index,":
        "Purpose: concrete PostgreSQL data types, PK/FK, UNIQUE, indexes,",
    "           CHECK constraint, quy tac ON DELETE":
        "           CHECK constraints, ON DELETE rules",
    "Nguon: Neon DB (ep-holy-unit-aofgdav8) - dump information_schema":
        "Source: live Neon DB (ep-holy-unit-aofgdav8) - information_schema dump",
    "        + backend/Aives.Infrastructure/Persistence/AivesDbContext.cs":
        "        + backend/Aives.Infrastructure/Persistence/AivesDbContext.cs",
    "Nguon: SRS 07/11/2026 + backend/Aives.Domain/Entities":
        "Source: SRS 07/11/2026 + backend/Aives.Domain/Entities",
    "Nguon: backend/Aives.Domain/Entities + AivesDbContext.OnModelCreating":
        "Source: backend/Aives.Domain/Entities + AivesDbContext.OnModelCreating",

    # --- packages -------------------------------------------------------------
    'package "Danh muc va noi dung" {': 'package "Catalog & Content" {',
    'package "To chuc thi" {': 'package "Exam Organisation" {',
    'package "Thi va cham diem" {': 'package "Taking & Grading" {',
    'package "Kiem toan" {': 'package "Audit" {',

    # --- conceptual entity names ---------------------------------------------
    'entity "Nguoi dung\\n(User)" as User': 'entity "User" as User',
    'entity "Mon hoc\\n(Course)" as Course': 'entity "Course" as Course',
    'entity "Tai lieu hoc\\n(Learning Document)" as Doc':
        'entity "Learning Document" as Doc',
    'entity "Cau hoi\\n(Question)" as Question': 'entity "Question" as Question',
    'entity "Tieu chi cham\\n(Rubric Criterion)" as Rubric':
        'entity "Rubric Criterion" as Rubric',
    'entity "De thi\\n(Exam Paper)" as ExamPaper': 'entity "Exam Paper" as ExamPaper',
    'entity "Ky thi\\n(Exam Period)" as Period': 'entity "Exam Period" as Period',
    'entity "Ca thi\\n(Exam Session)" as Session': 'entity "Exam Session" as Session',
    'entity "Kip thi\\n(Time Slot)" as Slot': 'entity "Time Slot" as Slot',
    'entity "Ghi danh mon\\n(Course Enrollment)" as Enrollment':
        'entity "Course Enrollment" as Enrollment',
    'entity "Ghi danh ca thi\\n(Session Registration)" as SessionReg':
        'entity "Session Registration" as SessionReg',
    'entity "Bai lam\\n(Submission / Result)" as Result':
        'entity "Submission / Result" as Result',
    'entity "Cau tra loi\\n(Answer)" as Answer': 'entity "Answer" as Answer',
    'entity "Ket qua cham AI\\n(AI Evaluation)" as AiEval':
        'entity "AI Evaluation" as AiEval',
    'entity "Tep am thanh\\n(Audio Recording)" as Audio':
        'entity "Audio Recording" as Audio',
    'entity "Yeu cau thi lai\\n(Retake)" as Retake': 'entity "Retake" as Retake',
    'entity "Nhat ky kiem toan\\n(Audit Log)" as Audit':
        'entity "Audit Log" as Audit',

    # --- conceptual relation labels ------------------------------------------
    '"tao / phuong day"': '"creates / teaches"',
    '"ghi danh hoc"': '"enrols in"',
    '"co ghi danh"': '"has enrolments"',
    '"chua tai lieu"': '"holds documents"',
    '"so huu ngan hang cau hoi"': '"owns the question bank"',
    '"sinh cau hoi tu"': '"generates questions from"',
    '"danh gia theo"': '"graded by"',
    '"so huu de thi"': '"owns exam papers"',
    '"chon cau hoi"': '"selects questions"',
    '"to chuc ky thi"': '"organises exam periods"',
    '"chua ca thi"': '"contains sessions"',
    '"chia kip thi"': '"splits into time slots"',
    '"co nguoi ghi danh"': '"has registrations"',
    '"la hoc sinh"': '"is the student"',
    '"phan cong kip"': '"is assigned a slot"',
    '"su dung trong ca"': '"is used in the session"',
    '"sinh ket qua"': '"produces results"',
    '"la nguoi thi"': '"is the candidate"',
    '"gom cau tra loi"': '"contains answers"',
    '"duoc tra loi"': '"is answered"',
    '"duoc cham boi AI"': '"is graded by AI"',
    '"kem ban ghi"': '"carries the recording"',
    '"duoc cap phep thi lai"': '"may be granted a retake"',
    '"ca thi lai"': '"retake session"',
    '"cap quyen"': '"grants"',
    '"thuc hien"': '"performs"',
    '"ghi nhan thao tac"': '"records the operation"',

    # --- conceptual notes -----------------------------------------------------
    "Vai tro: ADMIN / STAFF /": "Roles: ADMIN / STAFF /",
    "EXAMINER / STUDENT": "EXAMINER / STUDENT",
    "Trang thai duyet:": "Approval status:",
    "AI cham goi y,": "AI only suggests a score,",
    "KHAO THI quyet dinh": "the EXAMINER decides",
    "diem chinh thuc": "the official score",

    # --- logical packages / notes --------------------------------------------
    "package \"Danh muc va noi dung\" {": "package \"Catalog & Content\" {",
    "package \"To chuc thi\" {": "package \"Exam Organisation\" {",
    "package \"Thi va cham diem\" {": "package \"Taking & Grading\" {",
    "package \"Kiem toan\" {": "package \"Audit\" {",
    "* (student_id, session_id) la UNIQUE:": "* (student_id, session_id) is UNIQUE:",
    "    1 ket qua / hoc sinh / ca thi": "    one result per student per session",
    "  * status la concurrency token": "  * status is a concurrency token",
    "    (chong publish trung)": "    (prevents double publish)",
    "  * AI chi goi y - diem chinh thuc": "  * AI only suggests - the official score",
    "    do KHAO THI quyet dinh": "    is decided by the EXAMINER",
    "  * 0 < max_score <= 100": "  * 0 < max_score <= 100",
    "  * 0 <= position < 20": "  * 0 <= position < 20",
    "  * 1 <= duration_minutes <= 240": "  * 1 <= duration_minutes <= 240",
    "  * (original_result_id) UNIQUE": "  * (original_result_id) UNIQUE",
    "  * (retake_session_id) UNIQUE": "  * (retake_session_id) UNIQUE",

    # --- logical relation labels ---------------------------------------------
    '"tai len"': '"uploads"',
    '"sinh cau hoi tu"': '"generates questions from"',
    '"co tieu chi cham"': '"has rubric criteria"',
    '"tao de thi"': '"creates the paper"',
    '"duoc chon vao"': '"is selected in"',
    '"to chuc ky thi"': '"organises exam periods"',
    '"tao ky thi"': '"creates the period"',
    '"len lich"': '"schedules"',
    '"thuoc khoa hoc"': '"belongs to the course"',
    '"gan de thi"': '"assigns papers"',
    '"dung trong ca"': '"is used in the session"',
    '"dinh nghia kip"': '"defines the slot"',
    '"sinh tu mau"': '"is instantiated from"',
    '"dung trong kip"': '"is used in the slot"',
    '"la hoc sinh"': '"is the student"',
    '"ghi danh vao ca"': '"registers for the session"',
    '"duoc phan cong"': '"is assigned"',
    '"duoc phan cong kip"': '"is assigned a slot"',
    '"sinh ket qua"': '"produces results"',
    '"la nguoi thi"': '"is the candidate"',
    '"keo qua"': '"refers to"',
    '"gom cau tra loi"': '"contains answers"',
    '"duoc tra loi"': '"is answered"',
    '"ban ghi am"': '"has the recording"',
    '"tai len trong ca"': '"uploaded in the session"',
    '"duoc cap phep thi lai"': '"may be granted a retake"',
    '"ca thi lai"': '"retake session"',
    '"cap quyen"': '"grants"',
    '"thuc hien thao tac"': '"performs the operation"',

    # --- physical notes -------------------------------------------------------
    "CHECK ck_question_rubric_score:": "CHECK ck_question_rubric_score:",
    "CHECK ck_question_rubric_position:": "CHECK ck_question_rubric_position:",
    "CHECK ck_retake_duration:": "CHECK ck_retake_duration:",
    "duration_minutes BETWEEN 1 AND 240": "duration_minutes BETWEEN 1 AND 240",
    "CHECK reason: 5 <= length <= 1000": "CHECK reason: 5 <= length <= 1000",
    "UNIQUE uq_retake_original_result": "UNIQUE uq_retake_original_result",
    "UNIQUE uq_retake_private_session": "UNIQUE uq_retake_private_session",
    "concurrency: revision, consumed_at": "concurrency: revision, consumed_at",
    "UNIQUE unique_result_student_session": "UNIQUE unique_result_student_session",
    "UNIQUE unique_result_question trong answers": "UNIQUE unique_result_question in answers",
    "concurrency token: status": "concurrency token: status",
    "Upload chunked: received_chunk_indexes": "Chunked upload: received_chunk_indexes",
    "    (jsonb) luu index chunk da nhan": "    (jsonb) stores the received chunk indexes",
    "  Local-first: ghi local_file_path roi": "  Local-first: writes local_file_path, then",
    "    PutObject 1 lan len Cloudflare R2": "    performs a single PutObject to Cloudflare R2",
}

FILES = [
    ("erd_01_conceptual.puml", "erd_01_conceptual_en.puml"),
    ("erd_02_logical.puml", "erd_02_logical_en.puml"),
    ("erd_03_physical.puml", "erd_03_physical_en.puml"),
]


def main():
    for src_name, dst_name in FILES:
        text = (OUT / src_name).read_text(encoding="utf-8")
        for vi, en in REPLACEMENTS.items():
            text = text.replace(vi, en)
        (OUT / dst_name).write_text(text, encoding="utf-8", newline="\n")
        print("wrote", dst_name)


if __name__ == "__main__":
    main()
