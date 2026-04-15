namespace PainFinder.Shared.DTOs;

public record DeepOpportunityAnalysisDto(
    string SpecificPain,
    OperativeContextDto OperativeContext,
    CurrentSolutionDto CurrentSolution,
    string OpportunityGap,
    WedgeDto Wedge,
    string ValueProposition,
    ImmediateActionDto ImmediateAction,
    ValidationTestDto ValidationTest,
    MvpDefinitionDto MvpDefinition,
    string PaymentTrigger,
    RedFlagDto RedFlag);

public record OperativeContextDto(
    string Channel,
    string Timing,
    string AffectedUser,
    string Frequency,
    string DirectConsequence);

public record CurrentSolutionDto(
    List<string> ToolsUsed,
    string WhatWorks,
    string WhatFails);

public record WedgeDto(
    string Statement,
    string Justification);

public record ImmediateActionDto(
    string WhoToContact,
    string WhereToFind,
    string WhatToSay);

public record ValidationTestDto(
    string ManualService,
    string ResultToDeliver,
    string WhatToMeasure);

public record MvpDefinitionDto(
    string Feature1,
    string Feature2);

public record RedFlagDto(
    bool IsGenericRisk,
    string Explanation,
    string HowToMakeSpecific);
