using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSessionSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "assigned_slot_id",
                table: "session_enrollments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "session_slots",
                columns: table => new
                {
                    slot_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    slot_index = table.Column<int>(type: "integer", nullable: false),
                    slot_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    max_capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("session_slots_pkey", x => x.slot_id);
                    table.ForeignKey(
                        name: "session_slots_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_enrollments_assigned_slot_id",
                table: "session_enrollments",
                column: "assigned_slot_id");

            migrationBuilder.CreateIndex(
                name: "session_slots_session_slot_key",
                table: "session_slots",
                columns: new[] { "session_id", "slot_index" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "session_enrollments_assigned_slot_id_fkey",
                table: "session_enrollments",
                column: "assigned_slot_id",
                principalTable: "session_slots",
                principalColumn: "slot_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "session_enrollments_assigned_slot_id_fkey",
                table: "session_enrollments");

            migrationBuilder.DropTable(
                name: "session_slots");

            migrationBuilder.DropIndex(
                name: "IX_session_enrollments_assigned_slot_id",
                table: "session_enrollments");

            migrationBuilder.DropColumn(
                name: "assigned_slot_id",
                table: "session_enrollments");
        }
    }
}
