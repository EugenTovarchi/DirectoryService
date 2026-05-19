using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryService.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class MakeLocationAddressIndexesNonUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_location_with_flat_unique",
            table: "locations");

        migrationBuilder.DropIndex(
            name: "ix_location_without_flat_unique",
            table: "locations");

        migrationBuilder.CreateIndex(
            name: "ix_location_with_flat",
            table: "locations",
            columns: new[] { "country", "city", "street", "house", "flat" },
            filter: "flat IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_location_without_flat",
            table: "locations",
            columns: new[] { "country", "city", "street", "house" },
            filter: "flat IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_location_with_flat",
            table: "locations");

        migrationBuilder.DropIndex(
            name: "ix_location_without_flat",
            table: "locations");

        migrationBuilder.CreateIndex(
            name: "ix_location_with_flat_unique",
            table: "locations",
            columns: new[] { "country", "city", "street", "house", "flat" },
            unique: true,
            filter: "flat IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_location_without_flat_unique",
            table: "locations",
            columns: new[] { "country", "city", "street", "house" },
            unique: true,
            filter: "flat IS NULL");
    }
}
