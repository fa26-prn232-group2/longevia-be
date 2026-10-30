using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progress.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddProcessedMealEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "processed_meal_events",
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
                table.PrimaryKey("PK_processed_meal_events", x => x.EventId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_processed_meal_events_MealLogId",
            table: "processed_meal_events",
            column: "MealLogId");

        migrationBuilder.CreateIndex(
            name: "IX_processed_meal_events_UserId_BusinessDate",
            table: "processed_meal_events",
            columns: new[] { "UserId", "BusinessDate" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "processed_meal_events");
    }
}
