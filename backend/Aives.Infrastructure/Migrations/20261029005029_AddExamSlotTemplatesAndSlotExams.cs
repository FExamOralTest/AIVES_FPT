using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSlotTemplatesAndSlotExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "slot_template_id",
                table: "session_slots",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exam_slot_templates",
                columns: table => new
                {
                    slot_template_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    exam_id = table.Column<int>(type: "integer", nullable: false),
                    slot_index = table.Column<int>(type: "integer", nullable: false),
                    slot_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    max_capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_slot_templates_pkey", x => x.slot_template_id);
                    table.ForeignKey(
                        name: "exam_slot_templates_exam_id_fkey",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_slot_exams",
                columns: table => new
                {
                    slot_id = table.Column<int>(type: "integer", nullable: false),
                    exam_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("session_slot_exams_pkey", x => new { x.slot_id, x.exam_id });
                    table.ForeignKey(
                        name: "session_slot_exams_exam_id_fkey",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "session_slot_exams_slot_id_fkey",
                        column: x => x.slot_id,
                        principalTable: "session_slots",
                        principalColumn: "slot_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_slot_template_exams",
                columns: table => new
                {
                    slot_template_id = table.Column<int>(type: "integer", nullable: false),
                    exam_paper_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_slot_template_exams_pkey", x => new { x.slot_template_id, x.exam_paper_id });
                    table.ForeignKey(
                        name: "exam_slot_template_exams_exam_paper_id_fkey",
                        column: x => x.exam_paper_id,
                        principalTable: "exams",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "exam_slot_template_exams_slot_template_id_fkey",
                        column: x => x.slot_template_id,
                        principalTable: "exam_slot_templates",
                        principalColumn: "slot_template_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_slots_slot_template_id",
                table: "session_slots",
                column: "slot_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_slot_template_exams_exam_paper_id",
                table: "exam_slot_template_exams",
                column: "exam_paper_id");

            migrationBuilder.CreateIndex(
                name: "exam_slot_templates_exam_slot_key",
                table: "exam_slot_templates",
                columns: new[] { "exam_id", "slot_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_slot_exams_exam_id",
                table: "session_slot_exams",
                column: "exam_id");

            migrationBuilder.AddForeignKey(
                name: "session_slots_slot_template_id_fkey",
                table: "session_slots",
                column: "slot_template_id",
                principalTable: "exam_slot_templates",
                principalColumn: "slot_template_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "session_slots_slot_template_id_fkey",
                table: "session_slots");

            migrationBuilder.DropTable(
                name: "exam_slot_template_exams");

            migrationBuilder.DropTable(
                name: "session_slot_exams");

            migrationBuilder.DropTable(
                name: "exam_slot_templates");

            migrationBuilder.DropIndex(
                name: "IX_session_slots_slot_template_id",
                table: "session_slots");

            migrationBuilder.DropColumn(
                name: "slot_template_id",
                table: "session_slots");
        }
    }
}
