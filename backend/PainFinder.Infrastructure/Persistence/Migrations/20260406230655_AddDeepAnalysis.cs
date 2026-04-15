using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeepAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeepAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecificPain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AffectedUser = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DirectConsequence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToolsUsedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatWorks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatFails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpportunityGap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WedgeStatement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WedgeJustification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValueProposition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhoToContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhereToFind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatToSay = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ManualService = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultToDeliver = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatToMeasure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MvpFeature1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MvpFeature2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentTrigger = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsGenericRisk = table.Column<bool>(type: "bit", nullable: false),
                    RedFlagExplanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HowToMakeSpecific = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeepAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeepAnalyses_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeepAnalyses_OpportunityId",
                table: "DeepAnalyses",
                column: "OpportunityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeepAnalyses");
        }
    }
}
