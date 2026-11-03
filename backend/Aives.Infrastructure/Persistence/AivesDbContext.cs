using System;
using System.Collections.Generic;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Persistence;

public partial class AivesDbContext : DbContext
{
    public AivesDbContext(DbContextOptions<AivesDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Answer> Answers { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<Enrollment> Enrollments { get; set; }

    public virtual DbSet<Exam> Exams { get; set; }

    public virtual DbSet<ExamPeriod> ExamPeriods { get; set; }

    public virtual DbSet<ExamQuestion> ExamQuestions { get; set; }

    public virtual DbSet<ExamSession> ExamSessions { get; set; }

    public virtual DbSet<ExamSessionExam> ExamSessionExams { get; set; }

    public virtual DbSet<ExamSessionSlot> ExamSessionSlots { get; set; }

    public virtual DbSet<ExamSlotTemplate> ExamSlotTemplates { get; set; }

    public virtual DbSet<ExamSlotTemplateExam> ExamSlotTemplateExams { get; set; }

    public virtual DbSet<ExamSessionSlotExam> ExamSessionSlotExams { get; set; }

    public virtual DbSet<MarkdownDocument> MarkdownDocuments { get; set; }

    public virtual DbSet<QuestionBank> QuestionBanks { get; set; }
    public virtual DbSet<QuestionRubricCriterion> QuestionRubricCriteria { get; set; }

    public virtual DbSet<Result> Results { get; set; }

    public DbSet<ExamRetake> ExamRetakes { get; set; }

    public virtual DbSet<SessionEnrollment> SessionEnrollments { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<MediaUpload> MediaUploads { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("doc_type", new[] { "CONTENT", "LEARNING_OUTCOMES" })
            .HasPostgresEnum("exam_period_status", new[] { "DRAFT", "ACTIVE", "COMPLETED", "CANCELLED" })
            .HasPostgresEnum("exam_status", new[] { "DRAFT", "SCHEDULED", "OPEN", "CLOSED", "CANCELLED" })
            .HasPostgresEnum("session_status", new[] { "NOT_STARTED", "IN_PROGRESS", "COMPLETED", "CANCELLED" })
            .HasPostgresEnum("submission_status", new[] { "PENDING_REVIEW", "SUBMITTED", "REVIEWED", "PUBLISHED", "CANCELLED" })
            .HasPostgresEnum("user_role", new[] { "ADMIN", "STAFF", "EXAMINER", "STUDENT" });

        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(e => e.AnswerId).HasName("answers_pkey");

            entity.ToTable("answers");

            entity.HasIndex(e => e.ResultId, "idx_answers_result");

            entity.HasIndex(e => new { e.ResultId, e.QuestionId }, "unique_result_question").IsUnique();

            entity.Property(e => e.AnswerId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("answer_id");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(AnswerStatus.PROCESSING);
            entity.Property(e => e.AiFeedback).HasColumnName("ai_feedback");
            entity.Property(e => e.AiAnalysis).HasColumnName("ai_analysis").HasColumnType("jsonb");
            entity.Property(e => e.AiScore).HasColumnName("ai_score");
            entity.Property(e => e.AnsweredAt).HasColumnName("answered_at");
            entity.Property(e => e.AudioUrl).HasColumnName("audio_url");
            entity.Property(e => e.ObjectKey)
                .HasMaxLength(500)
                .HasColumnName("object_key");
            entity.Property(e => e.JobId)
                .HasMaxLength(100)
                .HasColumnName("job_id");
            entity.Property(e => e.AttemptCount)
                .HasDefaultValue(0)
                .HasColumnName("attempt_count");
            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");
            entity.Property(e => e.DurationSeconds)
                .HasColumnName("duration_seconds");
            entity.Property(e => e.DetectedLanguage)
                .HasMaxLength(20)
                .HasColumnName("detected_language");
            entity.Property(e => e.FinalScore).HasColumnName("final_score");
            entity.Property(e => e.IsFollowUp)
                .HasDefaultValue(false)
                .HasColumnName("is_follow_up");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.ResultId).HasColumnName("result_id");
            entity.Property(e => e.StudentAnswer).HasColumnName("student_answer");
            entity.Property(e => e.TeacherComment).HasColumnName("teacher_comment");
            entity.Property(e => e.Transcript).HasColumnName("transcript");

            entity.HasOne(d => d.Question).WithMany(p => p.Answers)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("answers_question_id_fkey");

            entity.HasOne(d => d.Result).WithMany(p => p.Answers)
                .HasForeignKey(d => d.ResultId)
                .HasConstraintName("answers_result_id_fkey");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("courses_pkey");

            entity.ToTable("courses");

            entity.HasIndex(e => e.CourseCode, "courses_course_code_key").IsUnique();

            entity.Property(e => e.CourseId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("course_id");
            entity.Property(e => e.CourseCode)
                .HasMaxLength(20)
                .HasColumnName("course_code");
            entity.Property(e => e.CourseName)
                .HasMaxLength(150)
                .HasColumnName("course_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CourseCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("courses_created_by_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.CourseTeachers)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("courses_teacher_id_fkey");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(e => e.EnrollmentId).HasName("enrollments_pkey");

            entity.ToTable("enrollments");

            entity.HasIndex(e => e.CourseId, "idx_enrollments_course");

            entity.HasIndex(e => e.StudentId, "idx_enrollments_student");

            entity.HasIndex(e => new { e.StudentId, e.CourseId }, "unique_student_course").IsUnique();

            entity.Property(e => e.EnrollmentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("enrollment_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.EnrolledAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("enrolled_at");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Course).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("enrollments_course_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("enrollments_student_id_fkey");
        });

        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(e => e.ExamId).HasName("exams_pkey");

            entity.ToTable("exams");

            entity.HasIndex(e => e.ExamCode, "exams_exam_code_key").IsUnique();

            entity.Property(e => e.ExamId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("exam_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(e => e.ExamCode)
                .HasMaxLength(50)
                .HasColumnName("exam_code");
            entity.Property(e => e.ExamStatus).HasColumnName("exam_status");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.MaxQuestions)
                .HasDefaultValue(10)
                .HasColumnName("max_questions");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");

            entity.HasOne(d => d.Course).WithMany(p => p.Exams)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("exams_course_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Exams)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("exams_created_by_fkey");
        });

        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(e => new { e.ExamId, e.QuestionId }).HasName("exam_questions_pkey");

            entity.ToTable("exam_questions");

            entity.Property(e => e.ExamId).HasColumnName("exam_id");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.QuestionScore).HasColumnName("question_score");

            entity.HasOne(d => d.Exam).WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.ExamId)
                .HasConstraintName("exam_questions_exam_id_fkey");

            entity.HasOne(d => d.Question).WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("exam_questions_question_id_fkey");
        });

        modelBuilder.Entity<ExamPeriod>(entity =>
        {
            entity.HasKey(e => e.PeriodId).HasName("exam_periods_pkey");

            entity.ToTable("exam_periods");

            entity.Property(e => e.PeriodId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("period_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.PeriodName)
                .HasMaxLength(150)
                .HasColumnName("period_name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Course).WithMany(p => p.ExamPeriods)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("exam_periods_course_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ExamPeriods)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("exam_periods_created_by_fkey");
        });

        modelBuilder.Entity<ExamSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("exam_sessions_pkey");

            entity.ToTable("exam_sessions");

            entity.HasIndex(e => e.SessionCode, "exam_sessions_session_code_key").IsUnique();

            entity.Property(e => e.SessionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("session_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.ExamId)
                .IsRequired(false)
                .HasColumnName("exam_id");
            entity.Property(e => e.CourseId)
                .IsRequired(false)
                .HasColumnName("course_id");
            entity.Property(e => e.PeriodId)
                .IsRequired(false)
                .HasColumnName("period_id");
            entity.Property(e => e.MaxCapacity)
                .HasDefaultValue(50)
                .HasColumnName("max_capacity");
            entity.Property(e => e.CurrentEnrollment)
                .HasDefaultValue(0)
                .HasColumnName("current_enrollment");
            entity.Property(e => e.Room)
                .HasMaxLength(150)
                .HasColumnName("room");
            entity.Property(e => e.ScheduledBy).HasColumnName("scheduled_by");
            entity.Property(e => e.SessionCode)
                .HasMaxLength(50)
                .HasColumnName("session_code");
            entity.Property(e => e.SessionName)
                .HasMaxLength(150)
                .HasColumnName("session_name");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.Status).HasColumnName("status");

            entity.HasOne(d => d.Course).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false)
                .HasConstraintName("exam_sessions_course_id_fkey");

            entity.HasOne(d => d.Period).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.PeriodId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false)
                .HasConstraintName("exam_sessions_period_id_fkey");

            entity.HasOne(d => d.Exam).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.ExamId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false)
                .HasConstraintName("exam_sessions_exam_id_fkey");

            entity.HasOne(d => d.ScheduledByNavigation).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.ScheduledBy)
                .HasConstraintName("exam_sessions_scheduled_by_fkey");
        });

        modelBuilder.Entity<ExamSessionExam>(entity =>
        {
            entity.HasKey(e => new { e.SessionId, e.ExamId }).HasName("session_exams_pkey");

            entity.ToTable("session_exams");

            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.ExamId).HasColumnName("exam_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Session).WithMany(p => p.SessionExams)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("session_exams_session_id_fkey");

            entity.HasOne(d => d.Exam).WithMany(p => p.SessionExams)
                .HasForeignKey(d => d.ExamId)
                .HasConstraintName("session_exams_exam_id_fkey");
        });

        modelBuilder.Entity<ExamSessionSlot>(entity =>
        {
            entity.HasKey(e => e.SlotId).HasName("session_slots_pkey");

            entity.ToTable("session_slots");

            entity.HasIndex(e => new { e.SessionId, e.SlotIndex }, "session_slots_session_slot_key").IsUnique();
            entity.HasIndex(e => e.SlotCode, "session_slots_slot_code_key").IsUnique();

            entity.Property(e => e.SlotId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("slot_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.SlotIndex).HasColumnName("slot_index");
            entity.Property(e => e.SlotName)
                .HasMaxLength(100)
                .HasColumnName("slot_name");
            entity.Property(e => e.SlotCode)
                .HasMaxLength(50)
                .HasColumnName("slot_code");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.MaxCapacity)
                .HasDefaultValue(10)
                .HasColumnName("max_capacity");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Session).WithMany(p => p.Slots)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("session_slots_session_id_fkey");

            entity.Property(e => e.SlotTemplateId).HasColumnName("slot_template_id");

            entity.HasOne(d => d.SlotTemplate).WithMany(p => p.SessionSlots)
                .HasForeignKey(d => d.SlotTemplateId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("session_slots_slot_template_id_fkey");
        });

        modelBuilder.Entity<ExamSlotTemplate>(entity =>
        {
            entity.HasKey(e => e.SlotTemplateId).HasName("exam_slot_templates_pkey");

            entity.ToTable("exam_slot_templates");

            entity.HasIndex(e => new { e.ExamId, e.SlotIndex }, "exam_slot_templates_exam_slot_key").IsUnique();

            entity.Property(e => e.SlotTemplateId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("slot_template_id");
            entity.Property(e => e.ExamId).HasColumnName("exam_id");
            entity.Property(e => e.SlotIndex).HasColumnName("slot_index");
            entity.Property(e => e.SlotName)
                .HasMaxLength(100)
                .HasColumnName("slot_name");
            entity.Property(e => e.DurationMinutes)
                .HasDefaultValue(30)
                .HasColumnName("duration_minutes");
            entity.Property(e => e.MaxCapacity)
                .HasDefaultValue(20)
                .HasColumnName("max_capacity");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Exam).WithMany(p => p.SlotTemplates)
                .HasForeignKey(d => d.ExamId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("exam_slot_templates_exam_id_fkey");
        });

        modelBuilder.Entity<ExamSlotTemplateExam>(entity =>
        {
            entity.HasKey(e => new { e.SlotTemplateId, e.ExamPaperId }).HasName("exam_slot_template_exams_pkey");

            entity.ToTable("exam_slot_template_exams");

            entity.Property(e => e.SlotTemplateId).HasColumnName("slot_template_id");
            entity.Property(e => e.ExamPaperId).HasColumnName("exam_paper_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            entity.HasOne(d => d.SlotTemplate).WithMany(p => p.TemplateExams)
                .HasForeignKey(d => d.SlotTemplateId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("exam_slot_template_exams_slot_template_id_fkey");

            // Edge Case #1: Restrict to avoid multiple cascade paths pointing to exams
            entity.HasOne(d => d.ExamPaper).WithMany(p => p.SlotTemplateExams)
                .HasForeignKey(d => d.ExamPaperId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("exam_slot_template_exams_exam_paper_id_fkey");
        });

        modelBuilder.Entity<ExamSessionSlotExam>(entity =>
        {
            entity.HasKey(e => new { e.SlotId, e.ExamId }).HasName("session_slot_exams_pkey");

            entity.ToTable("session_slot_exams");

            entity.Property(e => e.SlotId).HasColumnName("slot_id");
            entity.Property(e => e.ExamId).HasColumnName("exam_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Slot).WithMany(p => p.SlotExams)
                .HasForeignKey(d => d.SlotId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("session_slot_exams_slot_id_fkey");

            // Edge Case #1: Restrict to avoid multiple cascade paths pointing to exams
            entity.HasOne(d => d.Exam).WithMany(p => p.SessionSlotExams)
                .HasForeignKey(d => d.ExamId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("session_slot_exams_exam_id_fkey");
        });

        modelBuilder.Entity<MarkdownDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("markdown_documents_pkey");

            entity.ToTable("markdown_documents");

            entity.HasIndex(e => e.CourseId, "idx_markdown_documents_course");

            entity.Property(e => e.DocumentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("document_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.DocType).HasColumnName("doc_type");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

            entity.HasOne(d => d.Course).WithMany(p => p.MarkdownDocuments)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("markdown_documents_course_id_fkey");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.MarkdownDocuments)
                .HasForeignKey(d => d.UploadedBy)
                .HasConstraintName("markdown_documents_uploaded_by_fkey");
        });

        modelBuilder.Entity<QuestionBank>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("question_bank_pkey");

            entity.ToTable("question_bank");

            entity.HasIndex(e => e.CourseId, "idx_question_bank_course");

            entity.Property(e => e.QuestionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("question_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.IsFollowUpAllowed)
                .HasDefaultValue(true)
                .HasColumnName("is_follow_up_allowed");
            entity.Property(e => e.MaxScore)
                .HasDefaultValueSql("1.0")
                .HasColumnName("max_score");
            entity.Property(e => e.QuestionText).HasColumnName("question_text");
            entity.Property(e => e.Topic).HasColumnName("topic");
            entity.Property(e => e.SampleAnswer).HasColumnName("sample_answer");
            entity.Property(e => e.LearningOutcome).HasMaxLength(2000).HasColumnName("learning_outcome");
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.ExpectedKeywords).HasColumnName("expected_keywords");
            entity.Property(e => e.ApprovalStatus).HasConversion<string>()
                .HasDefaultValue(QuestionApprovalStatus.PENDING_REVIEW)
                .HasColumnName("approval_status");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.RejectionReason).HasMaxLength(2000).HasColumnName("rejection_reason");

            entity.HasOne(d => d.Course).WithMany(p => p.QuestionBanks)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("question_bank_course_id_fkey");

            entity.HasOne(d => d.Document).WithMany(p => p.QuestionBanks)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("question_bank_document_id_fkey");
        });

        modelBuilder.Entity<QuestionRubricCriterion>(entity =>
        {
            entity.HasKey(e => e.CriterionId).HasName("question_rubric_criteria_pkey");
            entity.ToTable("question_rubric_criteria", table =>
            {
                table.HasCheckConstraint("ck_question_rubric_score", "max_score > 0 AND max_score <= 100");
                table.HasCheckConstraint("ck_question_rubric_position", "position >= 0 AND position < 20");
            });
            entity.HasIndex(e => e.QuestionId, "idx_question_rubric_question");
            entity.Property(e => e.CriterionId).UseIdentityAlwaysColumn().HasColumnName("criterion_id");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.Description).HasMaxLength(2000).HasColumnName("description");
            entity.Property(e => e.MaxScore).HasPrecision(8, 4).HasColumnName("max_score");
            entity.HasOne(e => e.Question).WithMany(e => e.Rubric)
                .HasForeignKey(e => e.QuestionId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("question_rubric_question_id_fkey");
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("results_pkey");

            entity.ToTable("results");

            entity.HasIndex(e => e.SessionId, "idx_results_session");

            entity.HasIndex(e => e.StudentId, "idx_results_student");

            entity.HasIndex(e => new { e.StudentId, e.SessionId }, "unique_result_student_session").IsUnique();

            entity.Property(e => e.ResultId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("result_id");
            entity.Property(e => e.AiFeedback).HasColumnName("ai_feedback");
            entity.Property(e => e.AiAnalysis).HasColumnName("ai_analysis").HasColumnType("jsonb");
            entity.Property(e => e.AiEvaluationStatus).HasColumnName("ai_evaluation_status")
                .HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.AiEvaluationError).HasColumnName("ai_evaluation_error");
            entity.Property(e => e.AiScore).HasColumnName("ai_score");
            entity.Property(e => e.FinalFeedback).HasColumnName("final_feedback");
            entity.Property(e => e.FinalScore).HasColumnName("final_score");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.PublishedBy).HasColumnName("published_by");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.ExamId)
                .IsRequired(false)
                .HasColumnName("exam_id");
            entity.Property(e => e.Status).HasColumnName("status").IsConcurrencyToken();
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedNever()
                .HasColumnName("submitted_at");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ResultReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("results_reviewed_by_fkey");

            entity.HasOne(d => d.PublishedByNavigation).WithMany(p => p.ResultPublishedByNavigations)
                .HasForeignKey(d => d.PublishedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("results_published_by_fkey");

            entity.HasOne(d => d.Session).WithMany(p => p.Results)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("results_session_id_fkey");

            entity.HasOne(d => d.Exam).WithMany(p => p.Results)
                .HasForeignKey(d => d.ExamId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false)
                .HasConstraintName("results_exam_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ResultStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("results_student_id_fkey");
        });

        modelBuilder.Entity<ExamRetake>(entity =>
        {
            entity.ToTable("exam_retakes", t => t.HasCheckConstraint("ck_retake_duration", "duration_minutes BETWEEN 1 AND 240"));
            entity.HasKey(e => e.RetakeId);
            entity.Property(e => e.RetakeId).HasColumnName("retake_id").UseIdentityAlwaysColumn();
            entity.Property(e => e.OriginalResultId).HasColumnName("original_result_id");
            entity.Property(e => e.RetakeSessionId).HasColumnName("retake_session_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(1000);
            entity.Property(e => e.GrantedAt).HasColumnName("granted_at");
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(e => e.ConsumedAt).HasColumnName("consumed_at").IsConcurrencyToken();
            entity.Property(e => e.Revision).HasColumnName("revision").IsConcurrencyToken();
            entity.HasIndex(e => e.OriginalResultId).IsUnique();
            entity.HasIndex(e => e.RetakeSessionId).IsUnique();
            entity.HasOne(e => e.OriginalResult).WithMany().HasForeignKey(e => e.OriginalResultId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RetakeSession).WithMany().HasForeignKey(e => e.RetakeSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.GrantedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SessionEnrollment>(entity =>
        {
            entity.HasKey(e => e.SessionEnrollmentId).HasName("session_enrollments_pkey");

            entity.ToTable("session_enrollments");

            entity.HasIndex(e => e.SessionId, "idx_session_enrollments_session");

            entity.HasIndex(e => e.StudentId, "idx_session_enrollments_student");

            entity.HasIndex(e => new { e.StudentId, e.SessionId }, "unique_student_session").IsUnique();

            entity.Property(e => e.SessionEnrollmentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("session_enrollment_id");
            entity.Property(e => e.CurrentQuestionIndex).HasColumnName("current_question_index");
            entity.Property(e => e.EnrolledAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("enrolled_at");
            entity.Property(e => e.LastActivityAt).HasColumnName("last_activity_at");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.AssignedExamId)
                .IsRequired(false)
                .HasColumnName("assigned_exam_id");
            entity.Property(e => e.AssignedSlotId)
                .IsRequired(false)
                .HasColumnName("assigned_slot_id");
            entity.Property(e => e.StartedAt).HasColumnName("started_at").IsConcurrencyToken();
            entity.Property(e => e.Status).HasColumnName("status").IsConcurrencyToken();
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Session).WithMany(p => p.SessionEnrollments)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("session_enrollments_session_id_fkey");

            entity.HasOne(d => d.AssignedExam).WithMany(p => p.AssignedEnrollments)
                .HasForeignKey(d => d.AssignedExamId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false)
                .HasConstraintName("session_enrollments_assigned_exam_id_fkey");

            entity.HasOne(d => d.AssignedSlot).WithMany(p => p.SessionEnrollments)
                .HasForeignKey(d => d.AssignedSlotId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false)
                .HasConstraintName("session_enrollments_assigned_slot_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.SessionEnrollments)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("session_enrollments_student_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.UserId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Role)
                .HasColumnName("role")
                .HasColumnType("user_role");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("audit_logs_pkey");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => new { e.EntityName, e.EntityId }, "idx_audit_logs_entity");

            entity.HasIndex(e => e.PerformedBy, "idx_audit_logs_performed_by");

            entity.Property(e => e.LogId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("log_id");

            entity.Property(e => e.EntityName)
                .HasMaxLength(100)
                .HasColumnName("entity_name");

            entity.Property(e => e.EntityId)
                .HasColumnName("entity_id");

            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");

            entity.Property(e => e.OldValue)
                .HasColumnType("jsonb")
                .HasColumnName("old_value");

            entity.Property(e => e.NewValue)
                .HasColumnType("jsonb")
                .HasColumnName("new_value");

            entity.Property(e => e.Reason)
                .HasColumnName("reason");

            entity.Property(e => e.PerformedBy)
                .HasColumnName("performed_by");

            entity.Property(e => e.PerformedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("performed_at");

            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.PerformedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("audit_logs_performed_by_fkey");
        });

        modelBuilder.Entity<MediaUpload>(entity =>
        {
            entity.HasKey(e => e.UploadId).HasName("media_uploads_pkey");
            entity.ToTable("media_uploads");

            entity.HasIndex(e => e.UploadId, "media_uploads_upload_id_key").IsUnique();
            entity.HasIndex(e => e.StudentId, "IX_media_uploads_student_id");
            entity.HasIndex(e => new { e.SessionId, e.StudentId, e.QuestionId }, "idx_media_uploads_question");

            entity.Property(e => e.UploadId).HasMaxLength(64).HasColumnName("upload_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.AnswerId).HasColumnName("answer_id");
            entity.Property(e => e.FileName).HasMaxLength(255).HasColumnName("file_name");
            entity.Property(e => e.ContentType).HasMaxLength(100).HasColumnName("content_type");
            entity.Property(e => e.TotalSize).HasColumnName("total_size");
            entity.Property(e => e.ChunkSize).HasColumnName("chunk_size");
            entity.Property(e => e.TotalChunks).HasColumnName("total_chunks");
            entity.Property(e => e.ReceivedChunkIndexes)
                .HasColumnType("jsonb")
                .HasColumnName("received_chunk_indexes");
            entity.Property(e => e.UploadStatus).HasMaxLength(20).HasColumnName("upload_status");
            entity.Property(e => e.R2UploadId).HasColumnName("r2_upload_id");
            entity.Property(e => e.PartETags).HasColumnName("part_etags");
            entity.Property(e => e.ObjectKey).HasMaxLength(500).HasColumnName("object_key");
            entity.Property(e => e.LocalFilePath).HasMaxLength(500).HasColumnName("local_file_path");
            entity.Property(e => e.LastError).HasColumnName("last_error");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasOne<ExamSession>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("media_uploads_session_id_fkey");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("media_uploads_student_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

