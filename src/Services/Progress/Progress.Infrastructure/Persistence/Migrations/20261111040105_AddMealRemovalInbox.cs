using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progress.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealRemovalInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_meal_removals",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_meal_removals", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_processed_meal_removals_MealLogId",
                table: "processed_meal_removals",
                column: "MealLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processed_meal_removals_UserId_BusinessDate",
                table: "processed_meal_removals",
                columns: new[] { "UserId", "BusinessDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_meal_removals");
        }
    }
}
