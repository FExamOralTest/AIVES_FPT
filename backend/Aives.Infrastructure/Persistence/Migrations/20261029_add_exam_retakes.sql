-- Additive migration for an EXISTING AIVES database. Do not run against a shared database without approval.
-- The repository currently has no EF migration history/snapshot. This script does not recreate existing tables.
-- Apply once on an isolated development branch first, using a direct connection.
BEGIN;
CREATE TABLE exam_retakes (
    retake_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    original_result_id integer NOT NULL REFERENCES results(result_id) ON DELETE RESTRICT,
    retake_session_id integer NOT NULL REFERENCES exam_sessions(session_id) ON DELETE RESTRICT,
    student_id integer NOT NULL REFERENCES users(user_id) ON DELETE RESTRICT,
    granted_by integer NOT NULL REFERENCES users(user_id) ON DELETE RESTRICT,
    reason varchar(1000) NOT NULL CHECK (length(btrim(reason)) BETWEEN 5 AND 1000),
    granted_at timestamptz NOT NULL,
    duration_minutes integer NOT NULL CONSTRAINT ck_retake_duration CHECK (duration_minutes BETWEEN 1 AND 240),
    consumed_at timestamptz,
    revision integer NOT NULL DEFAULT 1,
    CONSTRAINT uq_retake_original_result UNIQUE (original_result_id),
    CONSTRAINT uq_retake_private_session UNIQUE (retake_session_id)
);
CREATE INDEX idx_retake_student ON exam_retakes(student_id);
COMMIT;
-- Rollback requires a separate approved data-preservation plan after retakes have been granted.
