using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandFoodCatalogSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-4111-8111-111111111111"),
                column: "Allergens",
                value: new string[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-4222-8222-222222222222"),
                column: "Allergens",
                value: new string[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-4333-8333-333333333333"),
                column: "Allergens",
                value: new string[0]);

            migrationBuilder.InsertData(
                table: "food_items",
                columns: new[] { "Id", "Allergens", "Category", "Name", "ServingSize", "ServingUnit", "UserId", "Calories", "CarbsGrams", "FatGrams", "FiberGrams", "ProteinGrams" },
                values: new object[,]
                {
                    { new Guid("d1e70000-0000-4000-8000-000000000001"), new string[0], "Grains", "Cơm gạo lứt", 100m, "g", null, 112m, 23.5m, 0.8m, 1.8m, 2.3m },
                    { new Guid("d1e70000-0000-4000-8000-000000000002"), new[] { "gluten", "lúa mì", "wheat" }, "Grains", "Yến mạch cán", 40m, "g", null, 156m, 26.5m, 2.8m, 4.2m, 6.8m },
                    { new Guid("d1e70000-0000-4000-8000-000000000003"), new[] { "gluten", "lúa mì", "wheat" }, "Grains", "Bánh mì nguyên cám", 100m, "g", null, 247m, 41m, 3.4m, 7m, 13m },
                    { new Guid("d1e70000-0000-4000-8000-000000000004"), new string[0], "Grains", "Bún tươi", 100m, "g", null, 109m, 25m, 0.2m, 1m, 0.9m },
                    { new Guid("d1e70000-0000-4000-8000-000000000005"), new string[0], "Grains", "Bánh phở tươi", 100m, "g", null, 141m, 32m, 0.3m, 0.6m, 1.8m },
                    { new Guid("d1e70000-0000-4000-8000-000000000006"), new string[0], "Grains", "Khoai lang luộc", 100m, "g", null, 86m, 20.1m, 0.1m, 3m, 1.6m },
                    { new Guid("d1e70000-0000-4000-8000-000000000007"), new string[0], "Grains", "Ngô luộc", 100m, "g", null, 96m, 21m, 1.5m, 2.4m, 3.4m },
                    { new Guid("d1e70000-0000-4000-8000-000000000011"), new string[0], "Meat", "Thịt heo nạc", 100m, "g", null, 143m, 0m, 4.3m, 0m, 26m },
                    { new Guid("d1e70000-0000-4000-8000-000000000012"), new string[0], "Meat", "Thịt bò nạc", 100m, "g", null, 150m, 0m, 7m, 0m, 21m },
                    { new Guid("d1e70000-0000-4000-8000-000000000013"), new string[0], "Meat", "Đùi gà bỏ da", 100m, "g", null, 121m, 0m, 4.1m, 0m, 19.7m },
                    { new Guid("d1e70000-0000-4000-8000-000000000021"), new[] { "cá", "fish", "hải sản" }, "Seafood", "Cá hồi", 100m, "g", null, 208m, 0m, 13m, 0m, 20m },
                    { new Guid("d1e70000-0000-4000-8000-000000000022"), new[] { "cá", "fish", "hải sản" }, "Seafood", "Cá thu", 100m, "g", null, 205m, 0m, 13.9m, 0m, 19m },
                    { new Guid("d1e70000-0000-4000-8000-000000000023"), new[] { "cá", "fish", "hải sản" }, "Seafood", "Cá basa", 100m, "g", null, 90m, 0m, 3.2m, 0m, 15m },
                    { new Guid("d1e70000-0000-4000-8000-000000000024"), new[] { "giáp xác", "hải sản", "shellfish", "tôm" }, "Seafood", "Tôm", 100m, "g", null, 99m, 0.2m, 0.3m, 0m, 24m },
                    { new Guid("d1e70000-0000-4000-8000-000000000025"), new[] { "hải sản", "mollusc", "mực", "nhuyễn thể" }, "Seafood", "Mực", 100m, "g", null, 92m, 3.1m, 1.4m, 0m, 15.6m },
                    { new Guid("d1e70000-0000-4000-8000-000000000031"), new[] { "egg", "trứng" }, "Eggs", "Trứng gà luộc", 50m, "g", null, 78m, 0.6m, 5.3m, 0m, 6.3m },
                    { new Guid("d1e70000-0000-4000-8000-000000000032"), new[] { "soy", "đậu nành" }, "Legumes", "Đậu phụ", 100m, "g", null, 76m, 1.9m, 4.8m, 0.3m, 8m },
                    { new Guid("d1e70000-0000-4000-8000-000000000033"), new string[0], "Legumes", "Đậu xanh nấu chín", 100m, "g", null, 105m, 19m, 0.4m, 7.6m, 7m },
                    { new Guid("d1e70000-0000-4000-8000-000000000034"), new string[0], "Legumes", "Đậu đen nấu chín", 100m, "g", null, 132m, 23.7m, 0.5m, 8.7m, 8.9m },
                    { new Guid("d1e70000-0000-4000-8000-000000000041"), new[] { "lactose", "milk", "sữa" }, "Dairy", "Sữa tươi không đường", 100m, "ml", null, 61m, 4.8m, 3.3m, 0m, 3.2m },
                    { new Guid("d1e70000-0000-4000-8000-000000000042"), new[] { "lactose", "milk", "sữa" }, "Dairy", "Sữa chua không đường", 100m, "g", null, 61m, 4.7m, 3.3m, 0m, 3.5m },
                    { new Guid("d1e70000-0000-4000-8000-000000000043"), new[] { "soy", "đậu nành" }, "Beverage", "Sữa đậu nành không đường", 100m, "ml", null, 33m, 1.7m, 1.6m, 0.4m, 2.9m },
                    { new Guid("d1e70000-0000-4000-8000-000000000044"), new string[0], "Beverage", "Trà xanh không đường", 100m, "ml", null, 1m, 0.2m, 0m, 0m, 0m },
                    { new Guid("d1e70000-0000-4000-8000-000000000051"), new string[0], "Vegetables", "Rau muống luộc", 100m, "g", null, 19m, 3.1m, 0.2m, 2.1m, 2.6m },
                    { new Guid("d1e70000-0000-4000-8000-000000000052"), new string[0], "Vegetables", "Cải bó xôi", 100m, "g", null, 23m, 3.6m, 0.4m, 2.2m, 2.9m },
                    { new Guid("d1e70000-0000-4000-8000-000000000053"), new string[0], "Vegetables", "Bông cải xanh", 100m, "g", null, 34m, 6.6m, 0.4m, 2.6m, 2.8m },
                    { new Guid("d1e70000-0000-4000-8000-000000000054"), new string[0], "Vegetables", "Cà chua", 100m, "g", null, 18m, 3.9m, 0.2m, 1.2m, 0.9m },
                    { new Guid("d1e70000-0000-4000-8000-000000000055"), new string[0], "Vegetables", "Cà rốt", 100m, "g", null, 41m, 9.6m, 0.2m, 2.8m, 0.9m },
                    { new Guid("d1e70000-0000-4000-8000-000000000056"), new string[0], "Vegetables", "Bí đỏ", 100m, "g", null, 26m, 6.5m, 0.1m, 0.5m, 1m },
                    { new Guid("d1e70000-0000-4000-8000-000000000057"), new string[0], "Vegetables", "Nấm rơm", 100m, "g", null, 32m, 4.6m, 0.7m, 2.5m, 3.8m },
                    { new Guid("d1e70000-0000-4000-8000-000000000061"), new string[0], "Fruits", "Chuối", 100m, "g", null, 89m, 22.8m, 0.3m, 2.6m, 1.1m },
                    { new Guid("d1e70000-0000-4000-8000-000000000062"), new string[0], "Fruits", "Cam", 100m, "g", null, 47m, 11.8m, 0.1m, 2.4m, 0.9m },
                    { new Guid("d1e70000-0000-4000-8000-000000000063"), new string[0], "Fruits", "Táo", 100m, "g", null, 52m, 13.8m, 0.2m, 2.4m, 0.3m },
                    { new Guid("d1e70000-0000-4000-8000-000000000064"), new string[0], "Fruits", "Bơ", 100m, "g", null, 160m, 8.5m, 14.7m, 6.7m, 2m },
                    { new Guid("d1e70000-0000-4000-8000-000000000065"), new string[0], "Fruits", "Đu đủ", 100m, "g", null, 43m, 10.8m, 0.3m, 1.7m, 0.5m },
                    { new Guid("d1e70000-0000-4000-8000-000000000071"), new[] { "hạt cây", "hạt điều", "tree_nut" }, "Nuts", "Hạt điều", 30m, "g", null, 166m, 9m, 13.2m, 1m, 5.5m },
                    { new Guid("d1e70000-0000-4000-8000-000000000072"), new[] { "hạnh nhân", "hạt cây", "tree_nut" }, "Nuts", "Hạnh nhân", 30m, "g", null, 174m, 6.5m, 15m, 3.8m, 6.4m },
                    { new Guid("d1e70000-0000-4000-8000-000000000073"), new[] { "hạt cây", "tree_nut", "óc chó" }, "Nuts", "Óc chó", 30m, "g", null, 196m, 4.1m, 19.6m, 2m, 4.6m },
                    { new Guid("d1e70000-0000-4000-8000-000000000074"), new[] { "lạc", "peanut", "đậu phộng" }, "Nuts", "Đậu phộng rang", 30m, "g", null, 176m, 6.5m, 14.9m, 2.6m, 7.1m },
                    { new Guid("d1e70000-0000-4000-8000-000000000075"), new[] { "mè", "sesame", "vừng" }, "Nuts", "Mè đen", 10m, "g", null, 57m, 2.3m, 5m, 1.2m, 1.8m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000011"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000012"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000013"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000021"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000022"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000023"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000024"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000025"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000031"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000032"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000033"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000034"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000041"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000042"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000043"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000044"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000051"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000052"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000053"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000054"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000055"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000056"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000057"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000061"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000062"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000063"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000064"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000065"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000071"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000072"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000073"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000074"));

            migrationBuilder.DeleteData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000075"));

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-4111-8111-111111111111"),
                column: "Allergens",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-4222-8222-222222222222"),
                column: "Allergens",
                value: null);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-4333-8333-333333333333"),
                column: "Allergens",
                value: null);
        }
    }
}
