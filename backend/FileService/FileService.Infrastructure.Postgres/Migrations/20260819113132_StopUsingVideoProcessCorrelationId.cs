using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class StopUsingVideoProcessCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deployment A прекращает отображать legacy-столбец в EF-модели,
            // но физически сохраняет его для безопасного отката на предыдущую версию приложения.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // В Deployment A legacy-столбец остаётся в БД, поэтому откат не требует изменения схемы.
        }
    }
}
