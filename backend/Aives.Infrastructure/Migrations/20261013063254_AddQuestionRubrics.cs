using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    public partial class AddQuestionRubrics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "learning_outcome",
                table: "question_bank",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "question_rubric_criteria",
                columns: table => new
                {
                    criterion_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    question_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("question_rubric_criteria_pkey", x => x.criterion_id);
                    table.CheckConstraint("ck_question_rubric_position", "position >= 0 AND position < 20");
                    table.CheckConstraint("ck_question_rubric_score", "max_score > 0 AND max_score <= 100");
                    table.ForeignKey(
                        name: "question_rubric_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "question_bank",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_question_rubric_question",
                table: "question_rubric_criteria",
                column: "question_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_rubric_criteria");

            migrationBuilder.DropColumn(
                name: "learning_outcome",
                table: "question_bank");

        }
    }
}
