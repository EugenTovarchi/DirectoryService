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
            migrationBuilder.Sql(@"
        CREATE UNIQUE INDEX IF NOT EXISTS ix_location_name ON locations (name);

        CREATE UNIQUE INDEX IF NOT EXISTS ix_location_address_unique 
        ON locations (country, city, street, house, flat);

            ");

            migrationBuilder.Sql(@"
        CREATE UNIQUE INDEX IF NOT EXISTS ix_department_name ON departments (name);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_department_identifier ON departments (identifier);
            ");

            migrationBuilder.Sql(@"
        CREATE UNIQUE INDEX IF NOT EXISTS ix_position_name ON positions (name);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        DROP INDEX IF EXISTS ix_position_name;
        DROP INDEX IF EXISTS ix_department_name;
        DROP INDEX IF EXISTS ix_department_identifier;
        DROP INDEX IF EXISTS ix_location_name;
        DROP INDEX IF EXISTS ix_location_address_unique;
            ");
        }
    }
}
