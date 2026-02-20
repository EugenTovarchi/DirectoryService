using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    chunk_count = table.Column<int>(type: "int", nullable: false),
                    asset_type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc',now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc',now())"),
                    key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    prefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    full_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_assets");
        }
    }
}
