using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tìm món không dấu: bật extension <c>unaccent</c> để truy vấn
    /// <c>lower(unaccent("Name"))</c> khớp "ca chua" với "Cà chua".
    ///
    /// Danh mục món hiện chỉ vài chục bản ghi nên chưa cần index biểu thức;
    /// nếu danh mục lớn lên, tạo hàm wrapper IMMUTABLE + index trigram sau.
    /// Down không drop extension vì đây là tài nguyên dùng chung của database.
    /// </summary>
    public partial class AddFoodFullTextUnaccent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Giữ extension: các migration/feature khác có thể đang dùng unaccent.
        }
    }
}
