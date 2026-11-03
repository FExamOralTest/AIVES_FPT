using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionApprovalAndExpectedKeywords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "question_bank",
                type: "text",
                nullable: false,
                defaultValue: "PENDING_REVIEW");

            migrationBuilder.AddColumn<List<string>>(
                name: "expected_keywords",
                table: "question_bank",
                type: "text[]",
                nullable: true);

            migrationBuilder.Sql("UPDATE question_bank SET expected_keywords = ARRAY[]::text[] WHERE expected_keywords IS NULL;");

            migrationBuilder.AlterColumn<List<string>>(
                name: "expected_keywords",
                table: "question_bank",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "question_bank",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "question_bank",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reviewed_by",
                table: "question_bank",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "question_bank");

            migrationBuilder.DropColumn(
                name: "expected_keywords",
                table: "question_bank");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "question_bank");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "question_bank");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                table: "question_bank");
        }
    }
}
