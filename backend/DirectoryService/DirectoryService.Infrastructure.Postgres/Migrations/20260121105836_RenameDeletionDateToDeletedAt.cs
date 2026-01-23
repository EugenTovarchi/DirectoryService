using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectoryService.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RenameDeletionDateToDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "deletion_date",
                table: "departments",
                newName: "deleted_at");
            
            migrationBuilder.RenameColumn(
                name: "deletion_date",
                table: "locations",
                newName: "deleted_at");
            
            migrationBuilder.RenameColumn(
                name: "deletion_date",
                table: "positions",
                newName: "deleted_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "departments",
                newName: "deletion_date");
            
            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "locations",
                newName: "deletion_date");
            
            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "positions",
                newName: "deletion_date");
        }
    }
}
