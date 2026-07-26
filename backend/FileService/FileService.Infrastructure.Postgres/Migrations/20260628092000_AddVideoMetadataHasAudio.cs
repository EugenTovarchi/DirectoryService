using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Postgres.Migrations;

[DbContext(typeof(FileServiceDbContext))]
[Migration("20260628092000_AddVideoMetadataHasAudio")]
public partial class AddVideoMetadataHasAudio : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "has_audio",
            table: "video_processes",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "has_audio",
            table: "video_processes");
    }
}
