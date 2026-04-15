using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Services;

/// <summary>
/// Decision Engine: clusters pain signals, scores market reality,
/// detects ICP/competition, attaches evidence quotes, and outputs
/// a BUILD / DON'T BUILD decision per opportunity.
/// </summary>
public partial class AiInsightGenerationService(
    IChatClient chatClient,
    ILogger<AiInsightGenerationService> logger) : IPainClusteringService, IOpportunityGenerationService
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)];

    private List<Opportunity>? _pendingOpportunities;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<PainCluster>> ClusterPainsAsync(
        IReadOnlyList<PainSignal> signals, CancellationToken cancellationToken = default)
    {
        if (signals.Count == 0)
        {
            _pendingOpportunities = [];
            return [];
        }

        const int MaxSignals = 100;
        if (signals.Count > MaxSignals)
        {
            logger.LogInformation(
                "Insights: Capping {Original} signals to top {Max} by pain score to avoid response truncation",
                signals.Count, MaxSignals);
            signals = signals.OrderByDescending(s => s.PainScore).Take(MaxSignals).ToList();
        }

        var entries = signals.Select((s, i) =>
            $"[{i}] (score:{s.PainScore:F2}) ({s.PainCategory}) \"{s.PainPhrase}\"");
        var signalsBlock = string.Join("\n", entries);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are a DECISION ENGINE for PainFinder.
                Target market: LATIN AMERICA — Colombia, Mexico and Argentina.
                Analyze pain signals and output HIGH-CONFIDENCE, ACTIONABLE startup opportunities
                relevant to the LATAM market context.

                LANGUAGE RULE — CRITICAL:
                ALL text content (title, problem, solution, name, description, icpRole, icpContext,
                gapAnalysis, buildReasoning, userContext, marketCategory)
                MUST be written in SPANISH.
                EXCEPTION: the "buildDecision" field must always be exactly one of: "STRONG YES", "WEAK YES", "NO".
                JSON field names must stay in English camelCase as defined in the output format.

                TITLE RULES — CRITICAL:
                - Titles must be SHORT and SPECIFIC (max 8 words)
                - NEVER include "LATAM", "Latin America", "Latinoamérica" or any region name in the title
                - The title should describe the PROBLEM or SOLUTION, not the geography
                - Bad: "Gestión Documental para PYMEs en LATAM" → Good: "Caos de Documentos en PYMEs"
                - Bad: "Facturación Electrónica en Latinoamérica" → Good: "Facturación Manual que Bloquea Cobros"

                CLUSTERING RULES:
                - Create 3-8 clusters based on SEMANTIC SIMILARITY.
                - Each signal index must appear in exactly one cluster.

                FOR EACH CLUSTER, output an opportunity with ALL of these fields:

                1. MARKET REALITY SCORE — combine these 5 factors (each 0.0-1.0):
                   - painIntensity: average pain_score of signals in this cluster
                   - frequency: relative cluster size (signals / total signals)
                   - urgency: presence of urgent language ("hate","blocked","urgent","nightmare","can't","broken","odio","falla","lento","caos")
                   - monetizationIntent: detect phrases like "would pay","need a tool","looking for solution","willing to switch","pagaría","necesito una herramienta","busco alternativa"
                   - competitionDensity: how many existing tools are mentioned (more tools = LOWER score, means gap is smaller)
                   - marketRealityScore: weighted average = painIntensity*0.25 + frequency*0.20 + urgency*0.25 + monetizationIntent*0.20 + competitionDensity*0.10

                2. ICP (Ideal Customer Profile) — inferred from language patterns, written in Spanish:
                   - role: specific (e.g. "Fundador de SaaS", "Contador freelance", NOT "usuario de negocios")
                   - context: specific (e.g. "usa Stripe para cobros recurrentes", NOT "gestiona pagos")

                3. COMPETITION:
                   - toolsDetected: list of existing tools/products mentioned in the signals
                   - gapAnalysis: in Spanish, "los usuarios se quejan de X a pesar de usar Y" format

                4. EVIDENCE — select up to 5 verbatim quotes from signals with highest pain scores:
                   - quote: the exact painPhrase (keep original language)
                   - signalIndex: which signal index it came from
                   - userContext: in Spanish, infer role from language if possible, else "desconocido"

                5. BUILD DECISION — based on marketRealityScore:
                   >= 0.75 → "STRONG YES"
                   0.50-0.74 → "WEAK YES"
                   < 0.50 → "NO"
                   buildReasoning must be in Spanish: short, sharp reasoning.

                Return ONLY valid JSON. No markdown, no trailing commas.
                Keep names under 60 chars, descriptions under 200 chars.

                Output format:
                {"clusters":[{
                  "name":"... (en español)",
                  "description":"... (en español)",
                  "signalIndices":[0,1,4],
                  "severity":0.0-1.0,
                  "opportunity":{
                    "title":"... (en español)",
                    "problem":"... (en español)",
                    "solution":"... (en español)",
                    "marketCategory":"... (en español)",
                    "confidence":0.0-1.0,
                    "marketRealityScore":0.0-1.0,
                    "painIntensity":0.0-1.0,
                    "frequency":0.0-1.0,
                    "urgency":0.0-1.0,
                    "monetizationIntent":0.0-1.0,
                    "competitionDensity":0.0-1.0,
                    "icpRole":"... (en español)",
                    "icpContext":"... (en español)",
                    "toolsDetected":["Tool1","Tool2"],
                    "gapAnalysis":"... (en español)",
                    "evidence":[{"quote":"...","signalIndex":0,"userContext":"... (en español)"}],
                    "buildDecision":"STRONG YES|WEAK YES|NO",
                    "buildReasoning":"... (en español)"
                  }
                }]}

                Market categories (in Spanish): Optimización de Costos, Herramientas para Desarrolladores, Infraestructura, Éxito del Cliente, Experiencia del Desarrollador, Desarrollo de Producto, Seguridad, Analítica, Automatización, General
                Prefer FEWER but HIGHER-QUALITY opportunities. If a cluster is weak, still include it but mark it NO.
                """),
            new(ChatRole.User, $"Analyze these {signals.Count} pain signals and output build decisions:\n\n{signalsBlock}")
        };

        logger.LogInformation("Insights: Sending {Count} pain signals to Decision Engine...", signals.Count);

        AiClusteringResult? result = null;

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
                var responseText = response.Messages[^1].Text?.Trim() ?? "{}";

                responseText = CleanResponse(responseText);
                result = TryDeserializeWithFallback(responseText);
                break;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Insights: Decision Engine TIMEOUT");
                break;
            }
            catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                if (attempt < RetryDelays.Length)
                {
                    var wait = RetryDelays[attempt];
                    logger.LogWarning(
                        "Insights: Rate-limited (429) — retrying in {Wait}s (attempt {A}/{Max})",
                        wait.TotalSeconds, attempt + 1, RetryDelays.Length);
                    await Task.Delay(wait, cancellationToken);
                }
                else
                {
                    logger.LogWarning(ex,
                        "Insights: Decision Engine FAILED after {Max} retries — {Error}",
                        RetryDelays.Length, ex.Message);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Insights: Decision Engine FAILED — {Error}", ex.Message);
                break;
            }
        }

        if (result?.Clusters is null or { Count: 0 })
        {
            logger.LogWarning("Insights: No clusters returned — falling back to category grouping");
            _pendingOpportunities = [];
            return FallbackClustering(signals);
        }

        var clusters = new List<PainCluster>();
        var opportunities = new List<Opportunity>();

        foreach (var aiCluster in result.Clusters)
        {
            var cluster = new PainCluster
            {
                Id = Guid.NewGuid(),
                Title = Truncate(aiCluster.Name, 100),
                Description = Truncate(aiCluster.Description, 500),
                Category = DetermineCategory(aiCluster.SignalIndices, signals),
                SeverityScore = Math.Clamp(aiCluster.Severity, 0.0, 1.0),
                DocumentCount = 0
            };

            var docIds = new HashSet<Guid>();
            foreach (var idx in aiCluster.SignalIndices)
            {
                if (idx < 0 || idx >= signals.Count) continue;
                var signal = signals[idx];
                signal.PainClusterId = cluster.Id;
                cluster.PainSignals.Add(signal);
                docIds.Add(signal.RawDocumentId);
            }
            cluster.DocumentCount = docIds.Count;

            if (cluster.PainSignals.Count == 0) continue;
            clusters.Add(cluster);

            if (aiCluster.Opportunity is { } opp)
            {
                var evidenceJson = "[]";
                try
                {
                    var evidenceQuotes = (opp.Evidence ?? [])
                        .Take(5)
                        .Select(e => new
                        {
                            quote = e.SignalIndex >= 0 && e.SignalIndex < signals.Count
                                ? signals[e.SignalIndex].PainPhrase
                                : e.Quote,
                            source = e.SignalIndex >= 0 && e.SignalIndex < signals.Count
                                ? signals[e.SignalIndex].RawDocument?.Source?.Name ?? "Unknown"
                                : "Unknown",
                            userContext = string.IsNullOrEmpty(e.UserContext) ? "unknown" : e.UserContext
                        })
                        .ToList();
                    evidenceJson = JsonSerializer.Serialize(evidenceQuotes, JsonOptions);
                }
                catch { }

                var mrs = Math.Clamp(opp.MarketRealityScore, 0.0, 1.0);
                var buildDecision = mrs >= 0.75 ? "STRONG YES" : mrs >= 0.50 ? "WEAK YES" : "NO";

                opportunities.Add(new Opportunity
                {
                    Id = Guid.NewGuid(),
                    PainClusterId = cluster.Id,
                    Title = Truncate(opp.Title, 200),
                    Description = cluster.Description,
                    ProblemSummary = Truncate(opp.Problem, 500),
                    SuggestedSolution = Truncate(opp.Solution, 500),
                    MarketCategory = opp.MarketCategory,
                    ConfidenceScore = Math.Clamp(opp.Confidence, 0.0, 1.0),
                    MarketRealityScore = mrs,
                    PainIntensityFactor = Math.Clamp(opp.PainIntensity, 0.0, 1.0),
                    FrequencyFactor = Math.Clamp(opp.Frequency, 0.0, 1.0),
                    UrgencyFactor = Math.Clamp(opp.Urgency, 0.0, 1.0),
                    MonetizationIntentFactor = Math.Clamp(opp.MonetizationIntent, 0.0, 1.0),
                    CompetitionDensityFactor = Math.Clamp(opp.CompetitionDensity, 0.0, 1.0),
                    IcpRole = Truncate(opp.IcpRole ?? "", 300),
                    IcpContext = Truncate(opp.IcpContext ?? "", 500),
                    ToolsDetected = string.Join("|", opp.ToolsDetected ?? []),
                    GapAnalysis = Truncate(opp.GapAnalysis ?? "", 2000),
                    BuildDecision = string.IsNullOrEmpty(opp.BuildDecision) ? buildDecision : opp.BuildDecision,
                    BuildReasoning = Truncate(opp.BuildReasoning ?? "", 1000),
                    EvidenceQuotesJson = evidenceJson
                });
            }
        }

        _pendingOpportunities = opportunities;

        var strongYes = opportunities.Count(o => o.BuildDecision == "STRONG YES");
        var weakYes = opportunities.Count(o => o.BuildDecision == "WEAK YES");
        var no = opportunities.Count(o => o.BuildDecision == "NO");
        logger.LogInformation(
            "Insights: Decision Engine returned {Clusters} clusters, {Opps} opportunities ({Strong} STRONG YES, {Weak} WEAK YES, {No} NO)",
            clusters.Count, opportunities.Count, strongYes, weakYes, no);

        return clusters;
    }

    public Task<IReadOnlyList<Opportunity>> GenerateOpportunitiesAsync(
        IReadOnlyList<PainCluster> clusters, CancellationToken cancellationToken = default)
    {
        var result = _pendingOpportunities ?? [];
        _pendingOpportunities = null;
        return Task.FromResult<IReadOnlyList<Opportunity>>(result);
    }

    private static IReadOnlyList<PainCluster> FallbackClustering(IReadOnlyList<PainSignal> signals)
    {
        return signals
            .GroupBy(s => s.PainCategory)
            .Select(group =>
            {
                var cluster = new PainCluster
                {
                    Id = Guid.NewGuid(),
                    Title = $"{group.Key} Issues",
                    Description = $"Cluster of {group.Count()} pain signals related to {group.Key}.",
                    Category = group.Key,
                    SeverityScore = Math.Round(group.Average(s => s.PainScore), 2),
                    DocumentCount = group.Select(s => s.RawDocumentId).Distinct().Count()
                };

                foreach (var signal in group)
                {
                    signal.PainClusterId = cluster.Id;
                    cluster.PainSignals.Add(signal);
                }

                return cluster;
            })
            .ToList();
    }

    private AiClusteringResult? TryDeserializeWithFallback(string responseText)
    {
        try
        {
            return JsonSerializer.Deserialize<AiClusteringResult>(responseText, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Insights: JSON parse failed, stripping evidence blocks — {Error}", ex.Message);

            var stripped = EvidenceBlockRegex().Replace(responseText, "\"evidence\":[]");
            try
            {
                return JsonSerializer.Deserialize<AiClusteringResult>(stripped, JsonOptions);
            }
            catch (JsonException ex2)
            {
                logger.LogWarning("Insights: JSON still invalid after stripping evidence — {Error}", ex2.Message);
                return null;
            }
        }
    }

    private static string CleanResponse(string responseText)
    {
        var codeFence = new string('`', 3);
        if (responseText.StartsWith(codeFence))
        {
            responseText = responseText
                .Replace(codeFence + "json", "")
                .Replace(codeFence, "")
                .Trim();
        }

        responseText = LeadingZeroRegex().Replace(responseText, ": -0.$1");
        responseText = PositiveLeadingZeroRegex().Replace(responseText, ": 0.$1");

        return responseText;
    }

    private static string DetermineCategory(List<int> indices, IReadOnlyList<PainSignal> signals)
    {
        var topCategory = indices
            .Where(i => i >= 0 && i < signals.Count)
            .Select(i => signals[i].PainCategory)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return topCategory?.Key ?? "General";
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] : text;

    // --- AI response models ---

    private sealed class AiClusteringResult
    {
        public List<AiClusterResult> Clusters { get; set; } = [];
    }

    private sealed class AiClusterResult
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<int> SignalIndices { get; set; } = [];
        public double Severity { get; set; }
        public AiOpportunityResult? Opportunity { get; set; }
    }

    private sealed class AiOpportunityResult
    {
        public string Title { get; set; } = "";
        public string Problem { get; set; } = "";
        public string Solution { get; set; } = "";
        public string MarketCategory { get; set; } = "";
        public double Confidence { get; set; }
        public double MarketRealityScore { get; set; }
        public double PainIntensity { get; set; }
        public double Frequency { get; set; }
        public double Urgency { get; set; }
        public double MonetizationIntent { get; set; }
        public double CompetitionDensity { get; set; }
        public string? IcpRole { get; set; }
        public string? IcpContext { get; set; }
        public List<string>? ToolsDetected { get; set; }
        public string? GapAnalysis { get; set; }
        public List<AiEvidenceQuote>? Evidence { get; set; }
        public string? BuildDecision { get; set; }
        public string? BuildReasoning { get; set; }
        public bool IsGenericOpportunity { get; set; }
        public List<string>? SpecializationSuggestions { get; set; }
    }

    private sealed class AiEvidenceQuote
    {
        public string Quote { get; set; } = "";
        public int SignalIndex { get; set; } = -1;
        public string? UserContext { get; set; }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@":\s*-\.(\d)")]
    private static partial System.Text.RegularExpressions.Regex LeadingZeroRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@":\s*\.(\d)")]
    private static partial System.Text.RegularExpressions.Regex PositiveLeadingZeroRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"""evidence""\s*:\s*\[.*?\]", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex EvidenceBlockRegex();
}
