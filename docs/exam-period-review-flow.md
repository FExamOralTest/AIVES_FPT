# Staff exam periods and examiner results

Staff creates course-specific periods at `/staff/periods` or from the session form. Staff can create periods only for their assigned courses; admin can create for any course. Examiner reads periods and student attempts using the same global reviewer scope as the existing result review/publish service. Student cannot access these endpoints.

## Data and scheduling

The existing relationship is used: `results.session_id -> exam_sessions.period_id -> exam_periods.period_id`. No schema migration is required.

New or rescheduled normal sessions with a period must belong to that period's course and fit inside its inclusive start/end calendar dates in Vietnam (UTC+7). The period must be ACTIVE. Validation is shared in the session repository so both existing session controllers enforce it, including the final schedule after slot templates expand the end time. A started attempt's period cannot be changed. Normal status-only updates remain possible for historical periods. General sessions with no period are still supported.

Private retakes inherit the original period and course. A technical recovery retake may occur after the normal period window. Older retakes that have no period stay visible under Unassigned; this change does not backfill existing records.

## Read APIs

- `GET /api/exam-periods[?courseId=...]`: STAFF sees their assigned courses; EXAMINER and ADMIN can read all periods.
- `POST /api/exam-periods`: STAFF and ADMIN only. Existing DTO is retained. Period dates are stored as calendar dates at UTC midnight; scheduling interprets their calendar bounds in Vietnam time.
- `GET /api/results?periodId=1`: paginated attempt summaries for every session in the period.
- `GET /api/results?unassigned=true`: attempts in general/legacy sessions without a period.
- `GET /api/results?sessionId=21`: compatible with existing retake/session links.
- Optional filters: `status=PENDING_REVIEW|SUBMITTED|REVIEWED|PUBLISHED|CANCELLED`, `aiStatus=PENDING|SUCCEEDED|FAILED`, `search` (student name/email/username), `page` (default 1), `pageSize` (default 20, max 100).
- `periodId` and `unassigned=true` cannot be combined. Nonexistent periods return 404, invalid filters return 400.
- Response: `{ items, sessions, totalCount, page, pageSize }`. Session options follow the selected period; review/AI/search filters affect only attempts.
- Existing `GET /api/results/{resultId}` loads full detail only when an attempt is opened. Existing review, publish and retake APIs are reused.

The list projects summary fields only and preserves null scores. A real zero score is shown as 0.0; a missing AI score is shown as Not graded. Cancelled/voided attempts are retained as history. The UI calls rows attempts because legacy result records may already exist before final submission.

## Manual verification

1. Restart backend and frontend with the updated code.
2. Log in as Staff. Open Exam periods, select an assigned course and create a period covering today's Vietnam date.
3. Open Exam sessions, select a paper from that course and the new period; create a session inside its dates. A wrong-course/inactive/out-of-window period must be rejected by the API.
4. Have a student submit answers using the existing exam flow.
5. Log in as Examiner. Open Review & publish, select the period and confirm that attempts from all its sessions appear. Filter by session, review/AI status and student name/code/email; test pagination.
6. Open a row, review/publish using the existing modal, and verify the list refreshes according to the current filters. Test a published and a voided attempt using View.
7. Select Unassigned to see legacy general sessions. An existing `/examiner/results?sessionId=...` link should open the session's attempts without manually finding the period.
8. Examiner POST to `/api/exam-periods` must return 403. Staff GET to `/api/results` must return 403; Student cannot read either endpoint.

Automated coverage is in `ExamPeriodFlowTests`: real repositories against isolated EF InMemory data, date/assignment rules, filters/pagination/null-vs-zero, and an isolated HTTP host for role permissions. No shared database or live AI provider is used by these tests.
