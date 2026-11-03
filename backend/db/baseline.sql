-- AIVES baseline schema (PostgreSQL 18)
--
-- WHY THIS FILE EXISTS
-- The EF migrations in Aives.Infrastructure/Migrations are incremental only: every one
-- of them uses AddColumn/CreateIndex against tables that were never created by a
-- committed migration. The first migration (20261008172917) already alters
-- "exam_sessions", so `dotnet ef database update` on an empty database fails with
--   ERROR: relation "exam_sessions" does not exist
-- This file is that missing baseline. It was dumped schema-only from the Neon
-- database that the team already runs, so it matches the real schema exactly.
--
-- HOW TO USE (first-time setup on a NEW Neon database)
--   1. Paste this file into the Neon console -> SQL Editor -> Run.
--      (or: psql "postgresql://<user>:<pass>@<host>/<db>?sslmode=require" -f db/baseline.sql)
--   2. dotnet ef database update --project Aives.Infrastructure --startup-project Aives.Api
--
-- STEP 2 IS ONLY NEEDED FOR FUTURE MIGRATIONS. Right now it fails with
--   42701: column "session_code" of relation "exam_sessions" already exists
-- because the baseline already contains every column the existing migrations add.
-- Those migrations predate this file and were never squashable into it.
-- See db/MIGRATION_NOTES.md for the one-time command that records them.
--
-- Every statement is guarded (IF NOT EXISTS / pg_constraint check), so it is safe
-- to re-run. Run it ONLY on a new/empty database: on a database that already has
-- the schema it is a no-op.
--
-- Contains schema only - no rows. Regenerate with:
--   pg_dump --schema-only --no-owner --no-privileges -n public
--   then run tools/make_baseline.ps1 to re-apply the idempotency guards.
-- PostgreSQL database dump



SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
-- Restored to 'public': pg_dump emits set_config('search_path','',false), and Neon
-- keeps that on the connection, which breaks every unqualified query in the app.
SELECT pg_catalog.set_config('search_path', 'public', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

-- Name: public; Type: SCHEMA; Schema: -; Owner: -



-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: -



-- Name: doc_type; Type: TYPE; Schema: public; Owner: -

DO $enum$ BEGIN
    CREATE TYPE public.doc_type AS ENUM (
    'CONTENT',
    'LEARNING_OUTCOMES'
    );
    EXCEPTION WHEN duplicate_object THEN NULL;
END $enum$;


-- Name: exam_status; Type: TYPE; Schema: public; Owner: -

DO $enum$ BEGIN
    CREATE TYPE public.exam_status AS ENUM (
    'DRAFT',
    'SCHEDULED',
    'OPEN',
    'CLOSED',
    'CANCELLED'
    );
    EXCEPTION WHEN duplicate_object THEN NULL;
END $enum$;


-- Name: session_status; Type: TYPE; Schema: public; Owner: -

DO $enum$ BEGIN
    CREATE TYPE public.session_status AS ENUM (
    'NOT_STARTED',
    'IN_PROGRESS',
    'COMPLETED',
    'CANCELLED'
    );
    EXCEPTION WHEN duplicate_object THEN NULL;
END $enum$;


-- Name: submission_status; Type: TYPE; Schema: public; Owner: -

DO $enum$ BEGIN
    CREATE TYPE public.submission_status AS ENUM (
    'PENDING_REVIEW',
    'SUBMITTED',
    'REVIEWED',
    'PUBLISHED',
    'CANCELLED'
    );
    EXCEPTION WHEN duplicate_object THEN NULL;
END $enum$;


-- Name: user_role; Type: TYPE; Schema: public; Owner: -

DO $enum$ BEGIN
    CREATE TYPE public.user_role AS ENUM (
    'ADMIN',
    'STAFF',
    'STUDENT',
    'EXAMINER'
    );
    EXCEPTION WHEN duplicate_object THEN NULL;
END $enum$;


SET default_tablespace = '';

SET default_table_access_method = heap;

-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


-- Name: answers; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.answers (
    answer_id integer NOT NULL,
    result_id integer NOT NULL,
    question_id integer NOT NULL,
    student_answer text,
    transcript text,
    audio_url text,
    is_follow_up boolean DEFAULT false,
    ai_score real,
    ai_feedback text,
    final_score real,
    teacher_comment text,
    answered_at timestamp with time zone,
    ai_analysis jsonb,
    status character varying(20) DEFAULT 'PROCESSING'::character varying NOT NULL,
    attempt_count integer DEFAULT 0 NOT NULL,
    detected_language character varying(20),
    duration_seconds real,
    error_message text,
    job_id character varying(100),
    object_key character varying(500)
);


-- Name: answers_answer_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'answers' AND column_name = 'answer_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.answers ALTER COLUMN answer_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.answers_answer_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: audit_logs; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.audit_logs (
    log_id bigint NOT NULL,
    entity_name character varying(100) NOT NULL,
    entity_id integer NOT NULL,
    action character varying(100) NOT NULL,
    old_value jsonb,
    new_value jsonb,
    reason text,
    performed_by integer,
    performed_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    ip_address character varying(45)
);


-- Name: audit_logs_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'audit_logs' AND column_name = 'log_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.audit_logs ALTER COLUMN log_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.audit_logs_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: courses; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.courses (
    course_id integer NOT NULL,
    course_code character varying(20) NOT NULL,
    course_name character varying(150) NOT NULL,
    teacher_id integer,
    created_by integer,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
);


-- Name: courses_course_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'courses' AND column_name = 'course_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.courses ALTER COLUMN course_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.courses_course_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: enrollments; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.enrollments (
    enrollment_id integer NOT NULL,
    student_id integer NOT NULL,
    course_id integer NOT NULL,
    enrolled_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
);


-- Name: enrollments_enrollment_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'enrollments' AND column_name = 'enrollment_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.enrollments ALTER COLUMN enrollment_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.enrollments_enrollment_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: exam_questions; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.exam_questions (
    exam_id integer NOT NULL,
    question_id integer NOT NULL,
    question_score real NOT NULL
);


-- Name: exam_sessions; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.exam_sessions (
    session_id integer NOT NULL,
    exam_id integer NOT NULL,
    scheduled_by integer NOT NULL,
    session_name character varying(150) NOT NULL,
    room character varying(150),
    start_time timestamp with time zone NOT NULL,
    end_time timestamp with time zone NOT NULL,
    status public.session_status DEFAULT 'NOT_STARTED'::public.session_status,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    session_code character varying(50) DEFAULT ''::character varying NOT NULL,
    max_capacity integer DEFAULT 50 NOT NULL,
    current_enrollment integer DEFAULT 0 NOT NULL
);


-- Name: exam_sessions_session_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'exam_sessions' AND column_name = 'session_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.exam_sessions ALTER COLUMN session_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.exam_sessions_session_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: exams; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.exams (
    exam_id integer NOT NULL,
    course_id integer NOT NULL,
    created_by integer NOT NULL,
    title character varying(255) NOT NULL,
    exam_code character varying(50) NOT NULL,
    duration_minutes integer NOT NULL,
    max_questions integer DEFAULT 10 NOT NULL,
    exam_status public.exam_status DEFAULT 'DRAFT'::public.exam_status,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    is_active boolean DEFAULT true NOT NULL
);


-- Name: exams_exam_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'exams' AND column_name = 'exam_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.exams ALTER COLUMN exam_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.exams_exam_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: markdown_documents; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.markdown_documents (
    document_id integer NOT NULL,
    course_id integer NOT NULL,
    uploaded_by integer NOT NULL,
    file_name character varying(255) NOT NULL,
    doc_type public.doc_type DEFAULT 'CONTENT'::public.doc_type NOT NULL,
    content text NOT NULL,
    uploaded_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
);


-- Name: markdown_documents_document_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'markdown_documents' AND column_name = 'document_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.markdown_documents ALTER COLUMN document_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.markdown_documents_document_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: question_bank; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.question_bank (
    question_id integer NOT NULL,
    course_id integer NOT NULL,
    document_id integer,
    question_text text NOT NULL,
    max_score real DEFAULT 1.0,
    sample_answer text,
    is_follow_up_allowed boolean DEFAULT true,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    topic text,
    learning_outcome character varying(2000),
    level integer DEFAULT 1 NOT NULL,
    approval_status text DEFAULT 'PENDING_REVIEW'::text NOT NULL,
    expected_keywords text[] NOT NULL,
    rejection_reason character varying(2000),
    reviewed_at timestamp with time zone,
    reviewed_by integer
);


-- Name: question_bank_question_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'question_bank' AND column_name = 'question_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.question_bank ALTER COLUMN question_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.question_bank_question_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: question_rubric_criteria; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.question_rubric_criteria (
    criterion_id integer NOT NULL,
    question_id integer NOT NULL,
    "position" integer NOT NULL,
    description character varying(2000) NOT NULL,
    max_score numeric(8,4) NOT NULL,
    CONSTRAINT ck_question_rubric_position CHECK ((("position" >= 0) AND ("position" < 20))),
    CONSTRAINT ck_question_rubric_score CHECK (((max_score > (0)::numeric) AND (max_score <= (100)::numeric)))
);


-- Name: question_rubric_criteria_criterion_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'question_rubric_criteria' AND column_name = 'criterion_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.question_rubric_criteria ALTER COLUMN criterion_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.question_rubric_criteria_criterion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: results; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.results (
    result_id integer NOT NULL,
    session_id integer NOT NULL,
    student_id integer NOT NULL,
    reviewed_by integer,
    ai_score real,
    ai_feedback text,
    final_score real,
    final_feedback text,
    status public.submission_status DEFAULT 'PENDING_REVIEW'::public.submission_status,
    submitted_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    reviewed_at timestamp with time zone,
    published_at timestamp with time zone,
    ai_analysis jsonb,
    ai_evaluation_error text,
    ai_evaluation_status character varying(20),
    published_by integer
);


-- Name: results_result_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'results' AND column_name = 'result_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.results ALTER COLUMN result_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.results_result_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: session_enrollments; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.session_enrollments (
    session_enrollment_id integer NOT NULL,
    session_id integer NOT NULL,
    student_id integer NOT NULL,
    enrolled_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    status public.session_status DEFAULT 'NOT_STARTED'::public.session_status,
    started_at timestamp with time zone,
    last_activity_at timestamp with time zone,
    current_question_index integer
);


-- Name: session_enrollments_session_enrollment_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'session_enrollments' AND column_name = 'session_enrollment_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.session_enrollments ALTER COLUMN session_enrollment_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.session_enrollments_session_enrollment_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: users; Type: TABLE; Schema: public; Owner: -

CREATE TABLE IF NOT EXISTS public.users (
    user_id integer NOT NULL,
    role public.user_role NOT NULL,
    username character varying(50) NOT NULL,
    full_name character varying(100) NOT NULL,
    email character varying(100),
    password_hash character varying(255) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
);


-- Name: users_user_id_seq; Type: SEQUENCE; Schema: public; Owner: -

DO $id$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'user_id' AND is_identity = 'YES') THEN
        ALTER TABLE public.users ALTER COLUMN user_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.users_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
        );
    END IF;
END $id$;


-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'PK___EFMigrationsHistory') THEN
        ALTER TABLE ONLY public."__EFMigrationsHistory"
            ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");
    END IF;
END $c$;


-- Name: answers answers_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'answers_pkey') THEN
        ALTER TABLE ONLY public.answers
            ADD CONSTRAINT answers_pkey PRIMARY KEY (answer_id);
    END IF;
END $c$;


-- Name: audit_logs audit_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'audit_logs_pkey') THEN
        ALTER TABLE ONLY public.audit_logs
            ADD CONSTRAINT audit_logs_pkey PRIMARY KEY (log_id);
    END IF;
END $c$;


-- Name: courses courses_course_code_key; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'courses_course_code_key') THEN
        ALTER TABLE ONLY public.courses
            ADD CONSTRAINT courses_course_code_key UNIQUE (course_code);
    END IF;
END $c$;


-- Name: courses courses_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'courses_pkey') THEN
        ALTER TABLE ONLY public.courses
            ADD CONSTRAINT courses_pkey PRIMARY KEY (course_id);
    END IF;
END $c$;


-- Name: enrollments enrollments_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'enrollments_pkey') THEN
        ALTER TABLE ONLY public.enrollments
            ADD CONSTRAINT enrollments_pkey PRIMARY KEY (enrollment_id);
    END IF;
END $c$;


-- Name: exam_questions exam_questions_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exam_questions_pkey') THEN
        ALTER TABLE ONLY public.exam_questions
            ADD CONSTRAINT exam_questions_pkey PRIMARY KEY (exam_id, question_id);
    END IF;
END $c$;


-- Name: exam_sessions exam_sessions_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exam_sessions_pkey') THEN
        ALTER TABLE ONLY public.exam_sessions
            ADD CONSTRAINT exam_sessions_pkey PRIMARY KEY (session_id);
    END IF;
END $c$;


-- Name: exams exams_exam_code_key; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exams_exam_code_key') THEN
        ALTER TABLE ONLY public.exams
            ADD CONSTRAINT exams_exam_code_key UNIQUE (exam_code);
    END IF;
END $c$;


-- Name: exams exams_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exams_pkey') THEN
        ALTER TABLE ONLY public.exams
            ADD CONSTRAINT exams_pkey PRIMARY KEY (exam_id);
    END IF;
END $c$;


-- Name: markdown_documents markdown_documents_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'markdown_documents_pkey') THEN
        ALTER TABLE ONLY public.markdown_documents
            ADD CONSTRAINT markdown_documents_pkey PRIMARY KEY (document_id);
    END IF;
END $c$;


-- Name: question_bank question_bank_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'question_bank_pkey') THEN
        ALTER TABLE ONLY public.question_bank
            ADD CONSTRAINT question_bank_pkey PRIMARY KEY (question_id);
    END IF;
END $c$;


-- Name: question_rubric_criteria question_rubric_criteria_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'question_rubric_criteria_pkey') THEN
        ALTER TABLE ONLY public.question_rubric_criteria
            ADD CONSTRAINT question_rubric_criteria_pkey PRIMARY KEY (criterion_id);
    END IF;
END $c$;


-- Name: results results_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'results_pkey') THEN
        ALTER TABLE ONLY public.results
            ADD CONSTRAINT results_pkey PRIMARY KEY (result_id);
    END IF;
END $c$;


-- Name: session_enrollments session_enrollments_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'session_enrollments_pkey') THEN
        ALTER TABLE ONLY public.session_enrollments
            ADD CONSTRAINT session_enrollments_pkey PRIMARY KEY (session_enrollment_id);
    END IF;
END $c$;


-- Name: answers unique_result_question; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'unique_result_question') THEN
        ALTER TABLE ONLY public.answers
            ADD CONSTRAINT unique_result_question UNIQUE (result_id, question_id);
    END IF;
END $c$;


-- Name: results unique_result_student_session; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'unique_result_student_session') THEN
        ALTER TABLE ONLY public.results
            ADD CONSTRAINT unique_result_student_session UNIQUE (student_id, session_id);
    END IF;
END $c$;


-- Name: enrollments unique_student_course; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'unique_student_course') THEN
        ALTER TABLE ONLY public.enrollments
            ADD CONSTRAINT unique_student_course UNIQUE (student_id, course_id);
    END IF;
END $c$;


-- Name: session_enrollments unique_student_session; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'unique_student_session') THEN
        ALTER TABLE ONLY public.session_enrollments
            ADD CONSTRAINT unique_student_session UNIQUE (student_id, session_id);
    END IF;
END $c$;


-- Name: users users_email_key; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'users_email_key') THEN
        ALTER TABLE ONLY public.users
            ADD CONSTRAINT users_email_key UNIQUE (email);
    END IF;
END $c$;


-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'users_pkey') THEN
        ALTER TABLE ONLY public.users
            ADD CONSTRAINT users_pkey PRIMARY KEY (user_id);
    END IF;
END $c$;


-- Name: users users_username_key; Type: CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'users_username_key') THEN
        ALTER TABLE ONLY public.users
            ADD CONSTRAINT users_username_key UNIQUE (username);
    END IF;
END $c$;


-- Name: IX_results_published_by; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS "IX_results_published_by" ON public.results USING btree (published_by);


-- Name: exam_sessions_session_code_key; Type: INDEX; Schema: public; Owner: -

CREATE UNIQUE INDEX IF NOT EXISTS exam_sessions_session_code_key ON public.exam_sessions USING btree (session_code);


-- Name: idx_answers_result; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_answers_result ON public.answers USING btree (result_id);


-- Name: idx_audit_logs_entity; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_audit_logs_entity ON public.audit_logs USING btree (entity_name, entity_id);


-- Name: idx_audit_logs_performed_by; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_audit_logs_performed_by ON public.audit_logs USING btree (performed_by);


-- Name: idx_enrollments_course; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_enrollments_course ON public.enrollments USING btree (course_id);


-- Name: idx_enrollments_student; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_enrollments_student ON public.enrollments USING btree (student_id);


-- Name: idx_markdown_documents_course; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_markdown_documents_course ON public.markdown_documents USING btree (course_id);


-- Name: idx_question_bank_course; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_question_bank_course ON public.question_bank USING btree (course_id);


-- Name: idx_question_rubric_question; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_question_rubric_question ON public.question_rubric_criteria USING btree (question_id);


-- Name: idx_results_session; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_results_session ON public.results USING btree (session_id);


-- Name: idx_results_student; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_results_student ON public.results USING btree (student_id);


-- Name: idx_session_enrollments_session; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_session_enrollments_session ON public.session_enrollments USING btree (session_id);


-- Name: idx_session_enrollments_student; Type: INDEX; Schema: public; Owner: -

CREATE INDEX IF NOT EXISTS idx_session_enrollments_student ON public.session_enrollments USING btree (student_id);


-- Name: answers answers_question_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'answers_question_id_fkey') THEN
        ALTER TABLE ONLY public.answers
            ADD CONSTRAINT answers_question_id_fkey FOREIGN KEY (question_id) REFERENCES public.question_bank(question_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: answers answers_result_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'answers_result_id_fkey') THEN
        ALTER TABLE ONLY public.answers
            ADD CONSTRAINT answers_result_id_fkey FOREIGN KEY (result_id) REFERENCES public.results(result_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: audit_logs audit_logs_performed_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'audit_logs_performed_by_fkey') THEN
        ALTER TABLE ONLY public.audit_logs
            ADD CONSTRAINT audit_logs_performed_by_fkey FOREIGN KEY (performed_by) REFERENCES public.users(user_id) ON DELETE SET NULL;
    END IF;
END $c$;


-- Name: courses courses_created_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'courses_created_by_fkey') THEN
        ALTER TABLE ONLY public.courses
            ADD CONSTRAINT courses_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.users(user_id) ON DELETE SET NULL;
    END IF;
END $c$;


-- Name: courses courses_teacher_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'courses_teacher_id_fkey') THEN
        ALTER TABLE ONLY public.courses
            ADD CONSTRAINT courses_teacher_id_fkey FOREIGN KEY (teacher_id) REFERENCES public.users(user_id) ON DELETE SET NULL;
    END IF;
END $c$;


-- Name: enrollments enrollments_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'enrollments_course_id_fkey') THEN
        ALTER TABLE ONLY public.enrollments
            ADD CONSTRAINT enrollments_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(course_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: enrollments enrollments_student_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'enrollments_student_id_fkey') THEN
        ALTER TABLE ONLY public.enrollments
            ADD CONSTRAINT enrollments_student_id_fkey FOREIGN KEY (student_id) REFERENCES public.users(user_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: exam_questions exam_questions_exam_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exam_questions_exam_id_fkey') THEN
        ALTER TABLE ONLY public.exam_questions
            ADD CONSTRAINT exam_questions_exam_id_fkey FOREIGN KEY (exam_id) REFERENCES public.exams(exam_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: exam_questions exam_questions_question_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exam_questions_question_id_fkey') THEN
        ALTER TABLE ONLY public.exam_questions
            ADD CONSTRAINT exam_questions_question_id_fkey FOREIGN KEY (question_id) REFERENCES public.question_bank(question_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: exam_sessions exam_sessions_exam_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exam_sessions_exam_id_fkey') THEN
        ALTER TABLE ONLY public.exam_sessions
            ADD CONSTRAINT exam_sessions_exam_id_fkey FOREIGN KEY (exam_id) REFERENCES public.exams(exam_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: exam_sessions exam_sessions_scheduled_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exam_sessions_scheduled_by_fkey') THEN
        ALTER TABLE ONLY public.exam_sessions
            ADD CONSTRAINT exam_sessions_scheduled_by_fkey FOREIGN KEY (scheduled_by) REFERENCES public.users(user_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: exams exams_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exams_course_id_fkey') THEN
        ALTER TABLE ONLY public.exams
            ADD CONSTRAINT exams_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(course_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: exams exams_created_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'exams_created_by_fkey') THEN
        ALTER TABLE ONLY public.exams
            ADD CONSTRAINT exams_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.users(user_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: markdown_documents markdown_documents_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'markdown_documents_course_id_fkey') THEN
        ALTER TABLE ONLY public.markdown_documents
            ADD CONSTRAINT markdown_documents_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(course_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: markdown_documents markdown_documents_uploaded_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'markdown_documents_uploaded_by_fkey') THEN
        ALTER TABLE ONLY public.markdown_documents
            ADD CONSTRAINT markdown_documents_uploaded_by_fkey FOREIGN KEY (uploaded_by) REFERENCES public.users(user_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: question_bank question_bank_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'question_bank_course_id_fkey') THEN
        ALTER TABLE ONLY public.question_bank
            ADD CONSTRAINT question_bank_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(course_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: question_bank question_bank_document_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'question_bank_document_id_fkey') THEN
        ALTER TABLE ONLY public.question_bank
            ADD CONSTRAINT question_bank_document_id_fkey FOREIGN KEY (document_id) REFERENCES public.markdown_documents(document_id) ON DELETE SET NULL;
    END IF;
END $c$;


-- Name: question_rubric_criteria question_rubric_question_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'question_rubric_question_id_fkey') THEN
        ALTER TABLE ONLY public.question_rubric_criteria
            ADD CONSTRAINT question_rubric_question_id_fkey FOREIGN KEY (question_id) REFERENCES public.question_bank(question_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: results results_published_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'results_published_by_fkey') THEN
        ALTER TABLE ONLY public.results
            ADD CONSTRAINT results_published_by_fkey FOREIGN KEY (published_by) REFERENCES public.users(user_id) ON DELETE SET NULL;
    END IF;
END $c$;


-- Name: results results_reviewed_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'results_reviewed_by_fkey') THEN
        ALTER TABLE ONLY public.results
            ADD CONSTRAINT results_reviewed_by_fkey FOREIGN KEY (reviewed_by) REFERENCES public.users(user_id) ON DELETE SET NULL;
    END IF;
END $c$;


-- Name: results results_session_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'results_session_id_fkey') THEN
        ALTER TABLE ONLY public.results
            ADD CONSTRAINT results_session_id_fkey FOREIGN KEY (session_id) REFERENCES public.exam_sessions(session_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: results results_student_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'results_student_id_fkey') THEN
        ALTER TABLE ONLY public.results
            ADD CONSTRAINT results_student_id_fkey FOREIGN KEY (student_id) REFERENCES public.users(user_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: session_enrollments session_enrollments_session_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'session_enrollments_session_id_fkey') THEN
        ALTER TABLE ONLY public.session_enrollments
            ADD CONSTRAINT session_enrollments_session_id_fkey FOREIGN KEY (session_id) REFERENCES public.exam_sessions(session_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- Name: session_enrollments session_enrollments_student_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -

DO $c$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'session_enrollments_student_id_fkey') THEN
        ALTER TABLE ONLY public.session_enrollments
            ADD CONSTRAINT session_enrollments_student_id_fkey FOREIGN KEY (student_id) REFERENCES public.users(user_id) ON DELETE CASCADE;
    END IF;
END $c$;


-- PostgreSQL database dump complete



