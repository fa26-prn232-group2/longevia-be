using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameDemoFoods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-4111-8111-111111111111"),
                column: "Name",
                value: "Ức gà");

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-4222-8222-222222222222"),
                column: "Name",
                value: "Cơm trắng");

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-4333-8333-333333333333"),
                column: "Name",
                value: "Nước lọc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-4111-8111-111111111111"),
                column: "Name",
                value: "Ức gà (demo)");

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-4222-8222-222222222222"),
                column: "Name",
                value: "Cơm trắng (demo)");

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-4333-8333-333333333333"),
                column: "Name",
                value: "Nước lọc (demo)");
        }
    }
}
