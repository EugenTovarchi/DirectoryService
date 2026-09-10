using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipartUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "multipart_upload_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    upload_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_multipart_upload_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_multipart_upload_sessions_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_multipart_upload_sessions_status_expires_at",
                table: "multipart_upload_sessions",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_multipart_upload_sessions_idempotency_key",
                table: "multipart_upload_sessions",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_multipart_upload_sessions_media_asset_id",
                table: "multipart_upload_sessions",
                column: "media_asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_multipart_upload_sessions_upload_id",
                table: "multipart_upload_sessions",
                column: "upload_id",
                unique: true,
                filter: "upload_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "multipart_upload_sessions");
        }
    }
}