using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBankLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "level",
                table: "question_bank",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "level",
                table: "question_bank");
        }
    }
}
