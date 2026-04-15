using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionEngineFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildDecision",
                table: "Opportunities",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BuildReasoning",
                table: "Opportunities",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "CompetitionDensityFactor",
                table: "Opportunities",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceQuotesJson",
                table: "Opportunities",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "FrequencyFactor",
                table: "Opportunities",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "GapAnalysis",
                table: "Opportunities",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IcpContext",
                table: "Opportunities",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IcpRole",
                table: "Opportunities",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "MarketRealityScore",
                table: "Opportunities",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MonetizationIntentFactor",
                table: "Opportunities",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PainIntensityFactor",
                table: "Opportunities",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ToolsDetected",
                table: "Opportunities",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "UrgencyFactor",
                table: "Opportunities",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildDecision",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "BuildReasoning",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "CompetitionDensityFactor",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "EvidenceQuotesJson",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "FrequencyFactor",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "GapAnalysis",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "IcpContext",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "IcpRole",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "MarketRealityScore",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "MonetizationIntentFactor",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "PainIntensityFactor",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "ToolsDetected",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "UrgencyFactor",
                table: "Opportunities");
        }
    }
}
