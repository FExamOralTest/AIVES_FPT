using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Aives.Infrastructure.Migrations;

[DbContext(typeof(AivesDbContext))]
[Migration("20261012090000_AddQuestionBankTopic")]
public partial class AddQuestionBankTopic : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "topic", table: "question_bank", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "topic", table: "question_bank");
    }
}
