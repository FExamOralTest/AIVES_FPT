using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotCodeToExamSessionSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slot_code",
                table: "session_slots",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "session_slots_slot_code_key",
                table: "session_slots",
                column: "slot_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "session_slots_slot_code_key",
                table: "session_slots");

            migrationBuilder.DropColumn(
                name: "slot_code",
                table: "session_slots");
        }
    }
}
