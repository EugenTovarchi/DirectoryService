using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    [Migration("20260519195051_AlignVideoProcessIndexes")]
    public partial class AlignVideoProcessIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_processes_steps_step_status",
                table: "video_processes",
                newName: "ix_video_processes_status");

            migrationBuilder.CreateIndex(
                name: "ix_video_processes_video_asset_id",
                table: "video_processes",
                column: "video_asset_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_video_processes_video_asset_id",
                table: "video_processes");

            migrationBuilder.RenameIndex(
                name: "ix_video_processes_status",
                table: "video_processes",
                newName: "ix_processes_steps_step_status");
        }
    }
}
