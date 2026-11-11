using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergyCalculationIsOther : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Calculation_IsOther",
                table: "daily_diet_targets",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calculation_IsOther",
                table: "daily_diet_targets");
        }
    }
}
