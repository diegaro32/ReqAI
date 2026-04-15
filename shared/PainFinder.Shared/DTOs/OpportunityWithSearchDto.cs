namespace PainFinder.Shared.DTOs;

public record OpportunityWithSearchDto(
    Guid SearchRunId,
    string SearchKeyword,
    List<OpportunityDto> Opportunities);
