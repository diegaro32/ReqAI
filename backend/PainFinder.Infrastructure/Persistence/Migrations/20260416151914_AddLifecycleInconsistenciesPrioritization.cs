using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleInconsistenciesPrioritization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Inconsistencies",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LifecycleModel",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Prioritization",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Inconsistencies",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "LifecycleModel",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "Prioritization",
                table: "RequirementGenerations");
        }
    }
}
