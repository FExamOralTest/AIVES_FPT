using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aives.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkedUploadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R2 multipart uploadId dài hơn 64 ký tự → cột text, tách khỏi upload_id (client id).
            migrationBuilder.AddColumn<string>(
                name: "r2_upload_id",
                table: "media_uploads",
                type: "text",
                nullable: true);

            // Danh sách ETag của các part đã upload — cần cho CompleteMultipartUpload.
            migrationBuilder.AddColumn<string>(
                name: "part_etags",
                table: "media_uploads",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "r2_upload_id",
                table: "media_uploads");

            migrationBuilder.DropColumn(
                name: "part_etags",
                table: "media_uploads");
        }
    }
}
