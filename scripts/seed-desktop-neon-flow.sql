-- Additive QA fixture for Aives / neondb on the shared Neon test database.
-- Run against the explicitly selected project and branch, never a new local DB.
-- Dedicated QA identities must first be registered through the backend API.
-- Does not reset existing sessions, results, passwords or EF migration history.
DO $fixture$
DECLARE
  staff_id integer;
  examiner_id integer;
  student_id_qa integer;
  probe_id integer;
  qa_course integer;
  qa_exam integer;
  qa_period integer;
  content_doc integer;
  q record;
  question_id_qa integer;
BEGIN
  IF current_database() <> 'neondb' THEN
    RAISE EXCEPTION 'Select the shared Aives Neon test database neondb explicitly.';
  END IF;
  PERFORM set_config('statement_timeout','20s',true);
  PERFORM set_config('lock_timeout','5s',true);
  PERFORM pg_advisory_xact_lock(39220261027);
  SELECT user_id INTO STRICT student_id_qa FROM users
    WHERE username='test_sv_001' AND email='test_sv_001@example.invalid' AND role='STUDENT';
  SELECT user_id INTO STRICT staff_id FROM users
    WHERE username='desktop_staff_qa' AND email='desktop_staff_qa@example.invalid'
      AND full_name='Desktop QA Staff' AND role IN ('STUDENT','STAFF');
  SELECT user_id INTO STRICT examiner_id FROM users
    WHERE username='desktop_examiner_qa' AND email='desktop_examiner_qa@example.invalid'
      AND full_name='Desktop QA Examiner' AND role IN ('STUDENT','EXAMINER');
  SELECT user_id INTO STRICT probe_id FROM users
    WHERE username='desktop_probe_qa' AND email='desktop_probe_qa@example.invalid' AND role='STUDENT';
  UPDATE users SET role='STAFF' WHERE user_id=staff_id AND role='STUDENT';
  UPDATE users SET role='EXAMINER' WHERE user_id=examiner_id AND role='STUDENT';

  IF EXISTS(SELECT 1 FROM courses WHERE course_code='DESKTOP-QA-SWD392'
      AND teacher_id IS DISTINCT FROM staff_id) THEN
    RAISE EXCEPTION 'Reserved QA course belongs to a different teacher.';
  END IF;
  INSERT INTO courses(course_code,course_name,teacher_id,created_by)
    VALUES('DESKTOP-QA-SWD392','[DESKTOP QA] Software Architecture',staff_id,staff_id)
    ON CONFLICT(course_code) DO NOTHING;
  SELECT course_id INTO STRICT qa_course FROM courses WHERE course_code='DESKTOP-QA-SWD392';
  INSERT INTO enrollments(student_id,course_id) VALUES(student_id_qa,qa_course),(probe_id,qa_course)
    ON CONFLICT(student_id,course_id) DO NOTHING;
  INSERT INTO markdown_documents(course_id,uploaded_by,file_name,doc_type,content)
    SELECT qa_course,staff_id,'desktop-qa-content.md','CONTENT',
      '# Software Architecture
Functional requirements describe system capabilities. Non-functional requirements describe qualities such as performance, reliability and security.
MVC separates Model (data and business rules), View (presentation), and Controller (input and coordination).
In AIVES, desktop/web clients call the backend. The backend validates access, manages exam sessions, stores recordings and invokes the AI service. Clients do not call AI directly.'
    WHERE NOT EXISTS(SELECT 1 FROM markdown_documents WHERE course_id=qa_course AND doc_type='CONTENT');
  SELECT document_id INTO STRICT content_doc FROM markdown_documents
    WHERE course_id=qa_course AND doc_type='CONTENT';
  INSERT INTO markdown_documents(course_id,uploaded_by,file_name,doc_type,content)
    SELECT qa_course,staff_id,'desktop-qa-outcomes.md','LEARNING_OUTCOMES',
      '# Learning outcomes
LO1: Distinguish functional and non-functional requirements.
LO2: Explain MVC responsibilities.
LO3: Explain client, backend and AI responsibilities in AIVES.'
    WHERE NOT EXISTS(SELECT 1 FROM markdown_documents WHERE course_id=qa_course AND doc_type='LEARNING_OUTCOMES');
  FOR q IN SELECT * FROM (VALUES
    (1,'Distinguish functional and non-functional requirements. Give one example of each.',3::real,
      'A functional requirement describes a system capability, such as submitting an audio answer. A non-functional requirement describes a quality, such as response time or data security.','Requirements','LO1',ARRAY['capability','quality','example']),
    (2,'What are the responsibilities of the Model, View and Controller in MVC?',3::real,
      'The Model manages data and business rules. The View presents the interface. The Controller handles user input, invokes business logic and coordinates responses.','MVC','LO2',ARRAY['Model','View','Controller']),
    (3,'Why should the AIVES desktop app call the backend instead of calling the AI service directly?',4::real,
      'The backend centralizes authentication, authorization, exam management and recording storage. It orchestrates AI and protects service credentials, keeping clients independent of AI providers.','Client Backend AI','LO3',ARRAY['authentication','authorization','storage','AI'])
  ) AS questions(n,body,score,sample,topic,outcome,keywords)
  LOOP
    INSERT INTO question_bank(course_id,document_id,question_text,max_score,sample_answer,
      topic,learning_outcome,level,is_follow_up_allowed,approval_status,expected_keywords,reviewed_by,reviewed_at)
      SELECT qa_course,content_doc,q.body,q.score,q.sample,q.topic,q.outcome,1,false,
        'APPROVED',q.keywords,staff_id,now()
      WHERE NOT EXISTS(SELECT 1 FROM question_bank WHERE course_id=qa_course AND question_text=q.body);
    SELECT question_id INTO STRICT question_id_qa FROM question_bank
      WHERE course_id=qa_course AND question_text=q.body;
    IF NOT EXISTS(SELECT 1 FROM question_rubric_criteria WHERE question_id=question_id_qa) THEN
      INSERT INTO question_rubric_criteria(question_id,position,description,max_score) VALUES
        (question_id_qa,0,'Identify the key concepts and component responsibilities',q.score/2),
        (question_id_qa,1,'Explain clearly and provide an appropriate example',q.score/2);
    END IF;
  END LOOP;
  INSERT INTO exams(course_id,created_by,title,exam_code,duration_minutes,max_questions,exam_status,is_active)
    VALUES(qa_course,staff_id,'[DESKTOP QA] Three questions - 30 minutes','DESKTOP-QA-EX-01',30,3,'OPEN',true)
    ON CONFLICT(exam_code) DO NOTHING;
  SELECT exam_id INTO STRICT qa_exam FROM exams
    WHERE exam_code='DESKTOP-QA-EX-01' AND course_id=qa_course AND created_by=staff_id;
  INSERT INTO exam_questions(exam_id,question_id,question_score)
    SELECT qa_exam,question_id,max_score FROM question_bank WHERE course_id=qa_course
    ON CONFLICT(exam_id,question_id) DO NOTHING;
  INSERT INTO exam_periods(course_id,period_name,description,start_date,end_date,status,created_by)
    SELECT qa_course,'[DESKTOP QA] Neon test period 2026-10-27','Shared Neon desktop demonstration',
      now()-interval '1 day',now()+interval '3 days',1,staff_id
    WHERE NOT EXISTS(SELECT 1 FROM exam_periods WHERE course_id=qa_course
      AND period_name='[DESKTOP QA] Neon test period 2026-10-27');
  SELECT period_id INTO STRICT qa_period FROM exam_periods WHERE course_id=qa_course
    AND period_name='[DESKTOP QA] Neon test period 2026-10-27';
  INSERT INTO exam_sessions(exam_id,course_id,period_id,scheduled_by,session_name,room,start_time,end_time,
    status,session_code,max_capacity,current_enrollment) VALUES
    (qa_exam,qa_course,qa_period,staff_id,'[DESKTOP QA NEON] Exam session for SV001','Shared Neon',
      now()-interval '10 minutes',now()+interval '12 hours','IN_PROGRESS','ESS-D3270001',20,0),
    (qa_exam,qa_course,qa_period,staff_id,'[DESKTOP QA NEON] Verification only','API verification',
      now()-interval '10 minutes',now()+interval '12 hours','IN_PROGRESS','ESS-D3270002',5,0)
    ON CONFLICT(session_code) DO NOTHING;
  IF EXISTS(SELECT 1 FROM exam_sessions WHERE session_code IN ('ESS-D3270001','ESS-D3270002')
    AND (exam_id IS DISTINCT FROM qa_exam OR scheduled_by IS DISTINCT FROM staff_id)) THEN
    RAISE EXCEPTION 'Reserved QA session code belongs to a different exam.';
  END IF;
  INSERT INTO session_exams(session_id,exam_id)
    SELECT session_id,qa_exam FROM exam_sessions WHERE session_code IN ('ESS-D3270001','ESS-D3270002')
    ON CONFLICT(session_id,exam_id) DO NOTHING;
  IF (SELECT count(*) FROM exam_questions WHERE exam_id=qa_exam)<>3 THEN
    RAISE EXCEPTION 'QA exam must have exactly three questions.';
  END IF;
END
$fixture$;
