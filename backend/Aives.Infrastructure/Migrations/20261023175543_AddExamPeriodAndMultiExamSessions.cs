using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamPeriodAndMultiExamSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "exam_sessions_exam_id_fkey",
                table: "exam_sessions");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:doc_type", "CONTENT,LEARNING_OUTCOMES")
                .Annotation("Npgsql:Enum:exam_period_status", "DRAFT,ACTIVE,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:exam_status", "DRAFT,SCHEDULED,OPEN,CLOSED,CANCELLED")
                .Annotation("Npgsql:Enum:session_status", "NOT_STARTED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:submission_status", "PENDING_REVIEW,SUBMITTED,REVIEWED,PUBLISHED,CANCELLED")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,STAFF,EXAMINER,STUDENT")
                .OldAnnotation("Npgsql:Enum:doc_type", "CONTENT,LEARNING_OUTCOMES")
                .OldAnnotation("Npgsql:Enum:exam_status", "DRAFT,SCHEDULED,OPEN,CLOSED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:session_status", "NOT_STARTED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:submission_status", "PENDING_REVIEW,SUBMITTED,REVIEWED,PUBLISHED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:user_role", "ADMIN,STAFF,EXAMINER,STUDENT");

            migrationBuilder.AddColumn<int>(
                name: "assigned_exam_id",
                table: "session_enrollments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "exam_id",
                table: "results",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "exam_id",
                table: "exam_sessions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "course_id",
                table: "exam_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "period_id",
                table: "exam_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exam_periods",
                columns: table => new
                {
                    period_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<int>(type: "integer", nullable: false),
                    period_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_periods_pkey", x => x.period_id);
                    table.ForeignKey(
                        name: "exam_periods_course_id_fkey",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "course_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "exam_periods_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_exams",
                columns: table => new
                {
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    exam_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("session_exams_pkey", x => new { x.session_id, x.exam_id });
                    table.ForeignKey(
                        name: "session_exams_exam_id_fkey",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "session_exams_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_enrollments_assigned_exam_id",
                table: "session_enrollments",
                column: "assigned_exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_results_exam_id",
                table: "results",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_sessions_course_id",
                table: "exam_sessions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_sessions_period_id",
                table: "exam_sessions",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_periods_course_id",
                table: "exam_periods",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_periods_created_by",
                table: "exam_periods",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_session_exams_exam_id",
                table: "session_exams",
                column: "exam_id");

            migrationBuilder.AddForeignKey(
                name: "exam_sessions_course_id_fkey",
                table: "exam_sessions",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "course_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "exam_sessions_exam_id_fkey",
                table: "exam_sessions",
                column: "exam_id",
                principalTable: "exams",
                principalColumn: "exam_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "exam_sessions_period_id_fkey",
                table: "exam_sessions",
                column: "period_id",
                principalTable: "exam_periods",
                principalColumn: "period_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "results_exam_id_fkey",
                table: "results",
                column: "exam_id",
                principalTable: "exams",
                principalColumn: "exam_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "session_enrollments_assigned_exam_id_fkey",
                table: "session_enrollments",
                column: "assigned_exam_id",
                principalTable: "exams",
                principalColumn: "exam_id",
                onDelete: ReferentialAction.SetNull);

            // Backfill legacy single-exam data into new columns & session_exams
            migrationBuilder.Sql(@"
                UPDATE exam_sessions es
                SET course_id = e.course_id
                FROM exams e
                WHERE es.exam_id = e.exam_id AND es.course_id IS NULL;

                INSERT INTO session_exams (session_id, exam_id)
                SELECT session_id, exam_id
                FROM exam_sessions
                WHERE exam_id IS NOT NULL
                ON CONFLICT DO NOTHING;

                UPDATE session_enrollments se
                SET assigned_exam_id = es.exam_id
                FROM exam_sessions es
                WHERE se.session_id = es.session_id AND se.assigned_exam_id IS NULL AND es.exam_id IS NOT NULL;

                UPDATE results r
                SET exam_id = es.exam_id
                FROM exam_sessions es
                WHERE r.session_id = es.session_id AND r.exam_id IS NULL AND es.exam_id IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "exam_sessions_course_id_fkey",
                table: "exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "exam_sessions_exam_id_fkey",
                table: "exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "exam_sessions_period_id_fkey",
                table: "exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "results_exam_id_fkey",
                table: "results");

            migrationBuilder.DropForeignKey(
                name: "session_enrollments_assigned_exam_id_fkey",
                table: "session_enrollments");

            migrationBuilder.DropTable(
                name: "exam_periods");

            migrationBuilder.DropTable(
                name: "session_exams");

            migrationBuilder.DropIndex(
                name: "IX_session_enrollments_assigned_exam_id",
                table: "session_enrollments");

            migrationBuilder.DropIndex(
                name: "IX_results_exam_id",
                table: "results");

            migrationBuilder.DropIndex(
                name: "IX_exam_sessions_course_id",
                table: "exam_sessions");

            migrationBuilder.DropIndex(
                name: "IX_exam_sessions_period_id",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "assigned_exam_id",
                table: "session_enrollments");

            migrationBuilder.DropColumn(
                name: "exam_id",
                table: "results");

            migrationBuilder.DropColumn(
                name: "course_id",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "period_id",
                table: "exam_sessions");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:doc_type", "CONTENT,LEARNING_OUTCOMES")
                .Annotation("Npgsql:Enum:exam_status", "DRAFT,SCHEDULED,OPEN,CLOSED,CANCELLED")
                .Annotation("Npgsql:Enum:session_status", "NOT_STARTED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:submission_status", "PENDING_REVIEW,SUBMITTED,REVIEWED,PUBLISHED,CANCELLED")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,STAFF,EXAMINER,STUDENT")
                .OldAnnotation("Npgsql:Enum:doc_type", "CONTENT,LEARNING_OUTCOMES")
                .OldAnnotation("Npgsql:Enum:exam_period_status", "DRAFT,ACTIVE,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:exam_status", "DRAFT,SCHEDULED,OPEN,CLOSED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:session_status", "NOT_STARTED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:submission_status", "PENDING_REVIEW,SUBMITTED,REVIEWED,PUBLISHED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:user_role", "ADMIN,STAFF,EXAMINER,STUDENT");

            migrationBuilder.AlterColumn<int>(
                name: "exam_id",
                table: "exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "exam_sessions_exam_id_fkey",
                table: "exam_sessions",
                column: "exam_id",
                principalTable: "exams",
                principalColumn: "exam_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
