using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiMealPlansAndMicronutrients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Micronutrients",
                table: "food_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "meal_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    MealsPerDay = table.Column<int>(type: "integer", nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plans", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-4111-8111-111111111111"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-4222-8222-222222222222"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-4333-8333-333333333333"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000001"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000002"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000003"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000004"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000005"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000006"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000007"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000011"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000012"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000013"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000021"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000022"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000023"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000024"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000025"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000031"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000032"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000033"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000034"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000041"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000042"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000043"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000044"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000051"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000052"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000053"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000054"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000055"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000056"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000057"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000061"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000062"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000063"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000064"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000065"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000071"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000072"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000073"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000074"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000075"),
                column: "Micronutrients",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_UserId_WeekStartDate",
                table: "meal_plans",
                columns: new[] { "UserId", "WeekStartDate" },
                unique: true);

            // Curated micronutrient values per 100 g/ml, rounded from USDA FoodData Central.
            // Missing foods/rows stay NULL (unknown), never zero.
            migrationBuilder.Sql("""
                UPDATE food_items SET "Micronutrients" = '{"Vitamin D":11.0,"Omega-3":1.8,"Calcium":9.0,"Iron":0.3}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000021';
                UPDATE food_items SET "Micronutrients" = '{"Vitamin C":28.1,"Calcium":99.0,"Iron":2.7,"Omega-3":0.14}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000052';
                UPDATE food_items SET "Micronutrients" = '{"Vitamin C":89.2,"Calcium":47.0,"Iron":0.73,"Omega-3":0.02}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000053';
                UPDATE food_items SET "Micronutrients" = '{"Vitamin C":53.2,"Calcium":40.0,"Iron":0.1}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000062';
                UPDATE food_items SET "Micronutrients" = '{"Calcium":113.0,"Vitamin D":1.3,"Iron":0.03}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000041';
                UPDATE food_items SET "Micronutrients" = '{"Calcium":350.0,"Iron":5.4,"Omega-3":0.3}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000032';
                UPDATE food_items SET "Micronutrients" = '{"Iron":1.4,"Calcium":27.0,"Vitamin C":0.0}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000033';
                UPDATE food_items SET "Micronutrients" = '{"Calcium":76.0,"Iron":1.1,"Omega-3":0.003}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000072';
                UPDATE food_items SET "Micronutrients" = '{"Omega-3":2.6,"Calcium":28.0,"Iron":0.8}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000073';
                UPDATE food_items SET "Micronutrients" = '{"Iron":1.7,"Calcium":21.0,"Omega-3":0.05}'::jsonb WHERE "Id" = 'd1e70000-0000-4000-8000-000000000002';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_plans");

            migrationBuilder.DropColumn(
                name: "Micronutrients",
                table: "food_items");
        }
    }
}
