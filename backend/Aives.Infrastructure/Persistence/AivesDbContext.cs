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

    public virtual DbSet<ExamQuestion> ExamQuestions { get; set; }

    public virtual DbSet<ExamSession> ExamSessions { get; set; }

    public virtual DbSet<MarkdownDocument> MarkdownDocuments { get; set; }

    public virtual DbSet<QuestionBank> QuestionBanks { get; set; }

    public virtual DbSet<Result> Results { get; set; }

    public virtual DbSet<SessionEnrollment> SessionEnrollments { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("doc_type", new[] { "CONTENT", "LEARNING_OUTCOMES" })
            .HasPostgresEnum("exam_status", new[] { "DRAFT", "SCHEDULED", "OPEN", "CLOSED", "CANCELLED" })
            .HasPostgresEnum("session_status", new[] { "NOT_STARTED", "IN_PROGRESS", "COMPLETED", "CANCELLED" })
            .HasPostgresEnum("submission_status", new[] { "PENDING_REVIEW", "SUBMITTED", "REVIEWED", "PUBLISHED", "CANCELLED" })
            .HasPostgresEnum("user_role", new[] { "ADMIN", "TEACHER", "STUDENT" });

        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(e => e.AnswerId).HasName("answers_pkey");

            entity.ToTable("answers");

            entity.HasIndex(e => e.ResultId, "idx_answers_result");

            entity.HasIndex(e => new { e.ResultId, e.QuestionId }, "unique_result_question").IsUnique();

            entity.Property(e => e.AnswerId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("answer_id");
            entity.Property(e => e.AiFeedback).HasColumnName("ai_feedback");
            entity.Property(e => e.AiScore).HasColumnName("ai_score");
            entity.Property(e => e.AnsweredAt).HasColumnName("answered_at");
            entity.Property(e => e.AudioUrl).HasColumnName("audio_url");
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
            entity.Property(e => e.ExamId).HasColumnName("exam_id");
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

            entity.HasOne(d => d.Exam).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.ExamId)
                .HasConstraintName("exam_sessions_exam_id_fkey");

            entity.HasOne(d => d.ScheduledByNavigation).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.ScheduledBy)
                .HasConstraintName("exam_sessions_scheduled_by_fkey");
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
            entity.Property(e => e.SampleAnswer).HasColumnName("sample_answer");

            entity.HasOne(d => d.Course).WithMany(p => p.QuestionBanks)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("question_bank_course_id_fkey");

            entity.HasOne(d => d.Document).WithMany(p => p.QuestionBanks)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("question_bank_document_id_fkey");
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
            entity.Property(e => e.AiScore).HasColumnName("ai_score");
            entity.Property(e => e.FinalFeedback).HasColumnName("final_feedback");
            entity.Property(e => e.FinalScore).HasColumnName("final_score");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("submitted_at");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ResultReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("results_reviewed_by_fkey");

            entity.HasOne(d => d.Session).WithMany(p => p.Results)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("results_session_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ResultStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("results_student_id_fkey");
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
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Session).WithMany(p => p.SessionEnrollments)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("session_enrollments_session_id_fkey");

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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
