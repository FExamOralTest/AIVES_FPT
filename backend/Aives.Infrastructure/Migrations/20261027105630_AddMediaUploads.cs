using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_uploads",
                columns: table => new
                {
                    upload_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    student_id = table.Column<int>(type: "integer", nullable: false),
                    question_id = table.Column<int>(type: "integer", nullable: false),
                    answer_id = table.Column<int>(type: "integer", nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    total_size = table.Column<long>(type: "bigint", nullable: false),
                    chunk_size = table.Column<long>(type: "bigint", nullable: false),
                    total_chunks = table.Column<int>(type: "integer", nullable: false),
                    received_chunk_indexes = table.Column<List<int>>(type: "jsonb", nullable: false),
                    upload_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("media_uploads_pkey", x => x.upload_id);
                    table.ForeignKey(
                        name: "media_uploads_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "media_uploads_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_media_uploads_question",
                table: "media_uploads",
                columns: new[] { "session_id", "student_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_student_id",
                table: "media_uploads",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "media_uploads_upload_id_key",
                table: "media_uploads",
                column: "upload_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_uploads");
        }
    }
}
