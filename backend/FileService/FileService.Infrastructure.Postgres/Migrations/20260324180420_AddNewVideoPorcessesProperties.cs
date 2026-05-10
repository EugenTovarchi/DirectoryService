using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddNewVideoPorcessesProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codec",
                table: "video_processes");

            migrationBuilder.DropColumn(
                name: "frame_rate",
                table: "video_processes");

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "video_processes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "video_processes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_critical",
                table: "video_processes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_retry_at",
                table: "video_processes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "video_asset_id",
                table: "video_processes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "video_processes");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "video_processes");

            migrationBuilder.DropColumn(
                name: "is_critical",
                table: "video_processes");

            migrationBuilder.DropColumn(
                name: "next_retry_at",
                table: "video_processes");

            migrationBuilder.DropColumn(
                name: "video_asset_id",
                table: "video_processes");

            migrationBuilder.AddColumn<string>(
                name: "codec",
                table: "video_processes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "frame_rate",
                table: "video_processes",
                type: "double precision",
                nullable: true);
        }
    }
}
