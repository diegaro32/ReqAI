using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainFinder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCleanedInput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionPlans");

            migrationBuilder.DropTable(
                name: "DeepAnalyses");

            migrationBuilder.DropTable(
                name: "PainSignals");

            migrationBuilder.DropTable(
                name: "Opportunities");

            migrationBuilder.DropTable(
                name: "RawDocuments");

            migrationBuilder.DropTable(
                name: "PainClusters");

            migrationBuilder.DropTable(
                name: "RadarScans");

            migrationBuilder.DropTable(
                name: "SearchRuns");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "RadarMonitors");

            migrationBuilder.AddColumn<string>(
                name: "CleanedInput",
                table: "RequirementGenerations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanedInput",
                table: "RequirementGenerations");

            migrationBuilder.CreateTable(
                name: "PainClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentCount = table.Column<int>(type: "int", nullable: false),
                    SeverityScore = table.Column<double>(type: "float", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PainClusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadarMonitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Keyword = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastScanAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sources = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalDocuments = table.Column<int>(type: "int", nullable: false),
                    TotalPainsDetected = table.Column<int>(type: "int", nullable: false),
                    TotalScans = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadarMonitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadarMonitors_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SearchRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateRangeFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateRangeTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentsCollected = table.Column<int>(type: "int", nullable: false),
                    ExpandedKeywords = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PainsDetected = table.Column<int>(type: "int", nullable: false),
                    Sources = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchRuns_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Opportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PainClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildDecision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BuildReasoning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CompetitionDensityFactor = table.Column<double>(type: "float", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceQuotesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FrequencyFactor = table.Column<double>(type: "float", nullable: false),
                    GapAnalysis = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IcpContext = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IcpRole = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsGenericOpportunity = table.Column<bool>(type: "bit", nullable: false),
                    MarketCategory = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MarketRealityScore = table.Column<double>(type: "float", nullable: false),
                    MonetizationIntentFactor = table.Column<double>(type: "float", nullable: false),
                    PainIntensityFactor = table.Column<double>(type: "float", nullable: false),
                    ProblemSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SpecializationSuggestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuggestedSolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ToolsDetected = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UrgencyFactor = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Opportunities_PainClusters_PainClusterId",
                        column: x => x.PainClusterId,
                        principalTable: "PainClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RadarScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RadarMonitorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentsCollected = table.Column<int>(type: "int", nullable: false),
                    ExpandedQuery = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PainsDetected = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadarScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadarScans_RadarMonitors_RadarMonitorId",
                        column: x => x.RadarMonitorId,
                        principalTable: "RadarMonitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoreFeaturesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstimatedTimeline = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExactIcp = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FirstStep = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FirstStepTomorrow = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MonetizationStrategiesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, defaultValue: "[]"),
                    OutreachMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ProblemStatement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TargetUsers = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TechStack = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ValidationStrategy = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValidationTest = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValueProposition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionPlans_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeepAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AffectedUser = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DirectConsequence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HowToMakeSpecific = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsGenericRisk = table.Column<bool>(type: "bit", nullable: false),
                    ManualService = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MvpFeature1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MvpFeature2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpportunityGap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentTrigger = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedFlagExplanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultToDeliver = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecificPain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToolsUsedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValueProposition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WedgeJustification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WedgeStatement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatFails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatToMeasure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatToSay = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatWorks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhereToFind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhoToContact = table.Column<string>(type: "nvarchar(max)", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "RawDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RadarScanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SearchRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawDocuments_RadarScans_RadarScanId",
                        column: x => x.RadarScanId,
                        principalTable: "RadarScans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RawDocuments_SearchRuns_SearchRunId",
                        column: x => x.SearchRunId,
                        principalTable: "SearchRuns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RawDocuments_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PainSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PainClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RawDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PainCategory = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PainPhrase = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PainScore = table.Column<double>(type: "float", nullable: false),
                    SentimentScore = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PainSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PainSignals_PainClusters_PainClusterId",
                        column: x => x.PainClusterId,
                        principalTable: "PainClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PainSignals_RawDocuments_RawDocumentId",
                        column: x => x.RawDocumentId,
                        principalTable: "RawDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlans_OpportunityId",
                table: "ActionPlans",
                column: "OpportunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeepAnalyses_OpportunityId",
                table: "DeepAnalyses",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_PainClusterId",
                table: "Opportunities",
                column: "PainClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_PainSignals_PainClusterId",
                table: "PainSignals",
                column: "PainClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_PainSignals_RawDocumentId",
                table: "PainSignals",
                column: "RawDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RadarMonitors_UserId",
                table: "RadarMonitors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RadarScans_RadarMonitorId",
                table: "RadarScans",
                column: "RadarMonitorId");

            migrationBuilder.CreateIndex(
                name: "IX_RawDocuments_RadarScanId",
                table: "RawDocuments",
                column: "RadarScanId");

            migrationBuilder.CreateIndex(
                name: "IX_RawDocuments_SearchRunId",
                table: "RawDocuments",
                column: "SearchRunId");

            migrationBuilder.CreateIndex(
                name: "IX_RawDocuments_SourceId",
                table: "RawDocuments",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchRuns_UserId",
                table: "SearchRuns",
                column: "UserId");
        }
    }
}
