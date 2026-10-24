using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDiet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_diet_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCalories = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TargetProteinGrams = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TargetCarbsGrams = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TargetFatGrams = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_diet_targets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "food_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ServingSize = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ServingUnit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Calories = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    ProteinGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    CarbsGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    FatGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    FiberGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_items", x => x.Id);
                    table.CheckConstraint("CK_food_serving", "\"ServingSize\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "meal_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MealType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "meal_log_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServingUnit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Calories = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    ProteinGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    CarbsGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    FatGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    FiberGrams = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_log_items", x => x.Id);
                    table.CheckConstraint("CK_meal_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_meal_log_items_food_items_FoodId",
                        column: x => x.FoodId,
                        principalTable: "food_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meal_log_items_meal_logs_MealLogId",
                        column: x => x.MealLogId,
                        principalTable: "meal_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "food_items",
                columns: new[] { "Id", "Category", "Name", "ServingSize", "ServingUnit", "UserId", "Calories", "CarbsGrams", "FatGrams", "FiberGrams", "ProteinGrams" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-4111-8111-111111111111"), "Meat", "Ức gà (demo)", 100m, "g", null, 165m, 0m, 3.6m, 0m, 31m },
                    { new Guid("22222222-2222-4222-8222-222222222222"), "Grains", "Cơm trắng (demo)", 100m, "g", null, 130m, 28m, 0.3m, 0.4m, 2.7m },
                    { new Guid("33333333-3333-4333-8333-333333333333"), "Beverage", "Nước lọc (demo)", 100m, "ml", null, 0m, 0m, 0m, 0m, 0m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_diet_targets_UserId",
                table: "daily_diet_targets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_items_UserId_Category",
                table: "food_items",
                columns: new[] { "UserId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_meal_log_items_FoodId",
                table: "meal_log_items",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_meal_log_items_MealLogId",
                table: "meal_log_items",
                column: "MealLogId");

            migrationBuilder.CreateIndex(
                name: "IX_meal_logs_UserId_Date",
                table: "meal_logs",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_diet_targets");

            migrationBuilder.DropTable(
                name: "meal_log_items");

            migrationBuilder.DropTable(
                name: "food_items");

            migrationBuilder.DropTable(
                name: "meal_logs");
        }
    }
}
