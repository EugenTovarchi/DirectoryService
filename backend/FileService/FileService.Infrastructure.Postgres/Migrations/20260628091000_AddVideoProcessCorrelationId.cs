using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Postgres.Migrations;

[Migration("20260628091000_AddVideoProcessCorrelationId")]
public partial class AddVideoProcessCorrelationId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "correlation_id",
            table: "video_processes",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.Sql(
            """
            UPDATE video_processes
            SET correlation_id = md5(random()::text || clock_timestamp()::text)
            WHERE correlation_id = '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "correlation_id",
            table: "video_processes");
    }
}
