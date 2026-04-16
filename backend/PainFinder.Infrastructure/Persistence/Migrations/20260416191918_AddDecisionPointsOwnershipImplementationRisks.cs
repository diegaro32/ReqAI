using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionPointsOwnershipImplementationRisks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionPoints",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImplementationRisks",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnershipActions",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionPoints",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "ImplementationRisks",
                table: "RequirementGenerations");

            migrationBuilder.DropColumn(
                name: "OwnershipActions",
                table: "RequirementGenerations");
        }
    }
}
