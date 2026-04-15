namespace PainFinder.Domain.Entities;

public class DeepAnalysis
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;

    // 1. Dolor específico
    public string SpecificPain { get; set; } = string.Empty;

    // 2. Contexto operativo
    public string Channel { get; set; } = string.Empty;
    public string Timing { get; set; } = string.Empty;
    public string AffectedUser { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string DirectConsequence { get; set; } = string.Empty;

    // 3. Solución actual
    public string ToolsUsedJson { get; set; } = "[]";
    public string WhatWorks { get; set; } = string.Empty;
    public string WhatFails { get; set; } = string.Empty;

    // 4. Brecha real
    public string OpportunityGap { get; set; } = string.Empty;

    // 5. Wedge
    public string WedgeStatement { get; set; } = string.Empty;
    public string WedgeJustification { get; set; } = string.Empty;

    // 6. Propuesta de valor
    public string ValueProposition { get; set; } = string.Empty;

    // 7. Acción inmediata
    public string WhoToContact { get; set; } = string.Empty;
    public string WhereToFind { get; set; } = string.Empty;
    public string WhatToSay { get; set; } = string.Empty;

    // 8. Test de validación
    public string ManualService { get; set; } = string.Empty;
    public string ResultToDeliver { get; set; } = string.Empty;
    public string WhatToMeasure { get; set; } = string.Empty;

    // 9. MVP
    public string MvpFeature1 { get; set; } = string.Empty;
    public string MvpFeature2 { get; set; } = string.Empty;

    // 10. Disparador de pago
    public string PaymentTrigger { get; set; } = string.Empty;

    // 11. Red Flag
    public bool IsGenericRisk { get; set; }
    public string RedFlagExplanation { get; set; } = string.Empty;
    public string HowToMakeSpecific { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
