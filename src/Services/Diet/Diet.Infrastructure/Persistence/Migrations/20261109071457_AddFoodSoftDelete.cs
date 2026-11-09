using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "food_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-4111-8111-111111111111"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-4222-8222-222222222222"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-4333-8333-333333333333"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000001"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000002"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000003"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000004"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000005"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000006"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000007"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000011"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000012"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000013"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000021"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000022"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000023"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000024"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000025"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000031"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000032"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000033"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000034"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000041"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000042"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000043"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000044"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000051"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000052"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000053"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000054"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000055"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000056"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000057"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000061"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000062"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000063"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000064"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000065"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000071"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000072"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000073"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000074"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "food_items",
                keyColumn: "Id",
                keyValue: new Guid("d1e70000-0000-4000-8000-000000000075"),
                columns: new string[0],
                values: new object[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "food_items");
        }
    }
}
