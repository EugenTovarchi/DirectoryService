using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryService.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class CreateIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "ix_department_name",
                table: "departments",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                   name: "ix_location_name",
                   table: "locations",
                   column: "name",
                   unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_name",
                table: "positions",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_position_name", table: "positions");
            migrationBuilder.DropIndex(name: "ix_department_name", table: "departments");
            migrationBuilder.DropIndex(name: "ix_location_without_flat_unique", table: "locations");
            migrationBuilder.DropIndex(name: "ix_location_with_flat_unique", table: "locations");
            migrationBuilder.DropIndex(name: "ix_location_name", table: "locations");
        }
    }
}
