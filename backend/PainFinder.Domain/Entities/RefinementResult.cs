namespace PainFinder.Domain.Entities;

public class RefinementResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GenerationId { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public string RefinedOutput { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RequirementGeneration Generation { get; set; } = null!;
}
