using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionCodeAndMonitoringFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add columns to exam_sessions
            migrationBuilder.AddColumn<string>(
                name: "session_code",
                table: "exam_sessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "max_capacity",
                table: "exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "current_enrollment",
                table: "exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Add unique index on session_code
            migrationBuilder.CreateIndex(
                name: "exam_sessions_session_code_key",
                table: "exam_sessions",
                column: "session_code",
                unique: true);

            // Add columns to session_enrollments
            migrationBuilder.AddColumn<DateTime>(
                name: "started_at",
                table: "session_enrollments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_at",
                table: "session_enrollments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_question_index",
                table: "session_enrollments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "exam_sessions_session_code_key",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "session_code",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "max_capacity",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "current_enrollment",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "started_at",
                table: "session_enrollments");

            migrationBuilder.DropColumn(
                name: "last_activity_at",
                table: "session_enrollments");

            migrationBuilder.DropColumn(
                name: "current_question_index",
                table: "session_enrollments");
        }
    }
}