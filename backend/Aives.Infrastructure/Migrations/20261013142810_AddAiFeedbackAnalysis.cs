using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    public partial class AddAiFeedbackAnalysis : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_analysis",
                table: "results",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_evaluation_error",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_evaluation_status",
                table: "results",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_analysis",
                table: "answers",
                type: "jsonb",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_analysis",
                table: "results");

            migrationBuilder.DropColumn(
                name: "ai_evaluation_error",
                table: "results");

            migrationBuilder.DropColumn(
                name: "ai_evaluation_status",
                table: "results");

            migrationBuilder.DropColumn(
                name: "ai_analysis",
                table: "answers");

        }
    }
}
