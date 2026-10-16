using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureRolesAndAddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_enum e 
        JOIN pg_type t ON e.enumtypid = t.oid 
        WHERE t.typname = 'user_role' AND e.enumlabel = 'TEACHER'
    ) THEN
        ALTER TYPE user_role RENAME VALUE 'TEACHER' TO 'STAFF';
    END IF;
END $$;
", suppressTransaction: true);

            migrationBuilder.Sql("ALTER TYPE user_role ADD VALUE IF NOT EXISTS 'EXAMINER';", suppressTransaction: true);


            migrationBuilder.AddColumn<int>(
                name: "published_by",
                table: "results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "answers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PROCESSING");

            migrationBuilder.CreateIndex(
                name: "IX_results_published_by",
                table: "results",
                column: "published_by");

            migrationBuilder.AddForeignKey(
                name: "results_published_by_fkey",
                table: "results",
                column: "published_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "results_published_by_fkey",
                table: "results");

            migrationBuilder.DropIndex(
                name: "IX_results_published_by",
                table: "results");

            migrationBuilder.DropColumn(
                name: "published_by",
                table: "results");

            migrationBuilder.DropColumn(
                name: "status",
                table: "answers");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:doc_type", "CONTENT,LEARNING_OUTCOMES")
                .Annotation("Npgsql:Enum:exam_status", "DRAFT,SCHEDULED,OPEN,CLOSED,CANCELLED")
                .Annotation("Npgsql:Enum:session_status", "NOT_STARTED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:submission_status", "PENDING_REVIEW,SUBMITTED,REVIEWED,PUBLISHED,CANCELLED")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,TEACHER,STUDENT")
                .OldAnnotation("Npgsql:Enum:doc_type", "CONTENT,LEARNING_OUTCOMES")
                .OldAnnotation("Npgsql:Enum:exam_status", "DRAFT,SCHEDULED,OPEN,CLOSED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:session_status", "NOT_STARTED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:submission_status", "PENDING_REVIEW,SUBMITTED,REVIEWED,PUBLISHED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:user_role", "ADMIN,STAFF,EXAMINER,STUDENT");
        }
    }
}
