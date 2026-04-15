using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.Persistence;

namespace PainFinder.Api.Endpoints;

/// <summary>
/// Dev-only diagnostics endpoints to audit connector health in real time.
/// Only available in Development environment.
/// </summary>
public static class DiagnosticsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static void MapDiagnosticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/debug")
            .WithTags("Diagnostics");

        // ─── Quick connector test — calls real DI connectors ────────────
        group.MapGet("/test-connectors", async (
            string keyword,
            string? source,
            IServiceProvider sp) =>
        {
            using var scope = sp.CreateScope();
            var connectors = scope.ServiceProvider.GetRequiredService<IEnumerable<ISourceConnector>>();
            var results = new List<object>();

            foreach (var connector in connectors)
            {
                if (source is not null &&
                    !connector.SourceType.ToString().Equals(source, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    var docs = await connector.FetchDocumentsAsync(keyword, null, null, cts.Token);
                    results.Add(new
                    {
                        connector = connector.SourceType.ToString(),
                        status = "OK",
                        count = docs.Count,
                        sampleTitles = docs.Take(3).Select(d => d.Title).ToList(),
                        sampleDates = docs.Take(3).Select(d => d.CreatedAt.ToString("yyyy-MM-dd")).ToList()
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        connector = connector.SourceType.ToString(),
                        status = "FAILED",
                        count = 0,
                        error = $"{ex.GetType().Name}: {ex.Message}",
                        sampleTitles = new List<string>(),
                        sampleDates = new List<string>()
                    });
                }
            }

            return Results.Ok(new { keyword, source = source ?? "ALL", results });
        })
        .WithSummary("Test connectors directly — no background service, no DB");

        // StackOverflow raw API test
        group.MapGet("/stackoverflow-raw", async (string keyword, IHttpClientFactory httpClientFactory, IConfiguration config) =>
        {
            using var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });
            client.Timeout = TimeSpan.FromSeconds(30);

            var encoded = Uri.EscapeDataString(keyword);
            var apiKey = config["StackOverflow:ApiKey"];
            var keyParam = string.IsNullOrWhiteSpace(apiKey) ? "" : $"&key={apiKey}";
            var url = $"https://api.stackexchange.com/2.3/search/advanced?order=desc&sort=relevance&q={encoded}&site=stackoverflow&pagesize=5{keyParam}";

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return Results.Json(new
                {
                    error = $"StackOverflow API returned {(int)response.StatusCode} ({response.StatusCode})",
                    hint = (int)response.StatusCode == 403
                        ? "API quota exceeded. Register a free API key at https://stackapps.com and set 'StackOverflow:ApiKey' in appsettings.json."
                        : (string?)null,
                    body = json.Length > 500 ? json[..500] : json
                }, statusCode: (int)response.StatusCode);
            }

            return Results.Text(json, "application/json");
        })
        .WithSummary("Raw StackOverflow API test — requires StackOverflow:ApiKey for higher quota");

        // Reddit raw audit
        group.MapGet("/reddit", async (
            string keyword,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger) =>
        {
            var result = new DiagnosticResult { Connector = "Reddit", Keyword = keyword };

            // Test BOTH the short list (diagnostic) and the full list (actual connector)
            var shortList = "smallbusiness+Entrepreneur+freelance+startups+SaaS+antiwork+rant";
            var fullList = "Accounting+sales+receptionists+OfficeWorkers+sysadmin+smallbusiness+Entrepreneur+freelance+RealEstate+Teachers+productivity+WorkReform+antiwork+rant+TalesFromRetail+TalesFromTheFrontDesk+talesfromtechsupport+jobs+careerguidance+startups+SaaS+webdev+programming+technology";

            var query = Uri.EscapeDataString(keyword);
            var shortUrl = $"https://www.reddit.com/r/{shortList}/search.json?q={query}&sort=relevance&t=month&limit=10&restrict_sr=on";
            var fullUrl  = $"https://www.reddit.com/r/{fullList}/search.json?q={query}&sort=relevance&t=month&limit=10&restrict_sr=on";

            result.UrlCalled = shortUrl;

            var http = httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PainFinder/1.0 (.NET)");
            http.Timeout = TimeSpan.FromSeconds(15);

            // Test short list
            var shortResponse = await http.GetAsync(shortUrl);
            var shortBody = await shortResponse.Content.ReadAsStringAsync();

            // Test full list (what the actual connector uses)
            var fullResponse = await http.GetAsync(fullUrl);
            var fullBody = await fullResponse.Content.ReadAsStringAsync();

            result.Extra = $"SHORT list ({shortList.Split('+').Length} subs): HTTP {(int)shortResponse.StatusCode} — " +
                           $"FULL list ({fullList.Split('+').Length} subs): HTTP {(int)fullResponse.StatusCode}";

            try
            {
                var shortData = JsonSerializer.Deserialize<RedditListingDebug>(shortBody, JsonOptions);
                var fullData  = JsonSerializer.Deserialize<RedditListingDebug>(fullBody, JsonOptions);

                var shortCount = shortData?.Data?.Children?.Count ?? 0;
                var fullCount  = fullData?.Data?.Children?.Count ?? 0;

                result.HttpStatus = (int)shortResponse.StatusCode;
                result.ParsedOk = shortCount > 0;
                result.ParsedChildrenCount = shortCount;
                result.ParsedTitles = shortData?.Data?.Children?
                    .Take(5).Where(c => c.Data is not null)
                    .Select(c => $"[r/{c.Data!.Subreddit}] {c.Data.Title}").ToList() ?? [];

                result.Error = $"SHORT={shortCount} posts | FULL={fullCount} posts — " +
                               (fullCount == 0 && shortCount > 0
                                   ? "⚠️ FULL list failing (too many subreddits)"
                                   : fullCount > 0 ? "✅ Both work" : "❌ Both fail");
            }
            catch (Exception parseEx)
            {
                result.Error = $"JSON parse failed: {parseEx.Message}";
            }

            result.RawBodyPreview = fullBody.Length > 800
                ? fullBody[..800] + "... [TRUNCATED]"
                : fullBody;

            return Results.Ok(result);
        })
        .WithSummary("Audit Reddit connector — compares short vs full subreddit list");

        // HackerNews audit
        group.MapGet("/hackernews", async (
            string keyword,
            IHttpClientFactory httpClientFactory) =>
        {
            var result = new DiagnosticResult { Connector = "HackerNews", Keyword = keyword };
            var url = $"https://hn.algolia.com/api/v1/search_by_date?query={Uri.EscapeDataString(keyword)}&tags=story&hitsPerPage=10";
            result.UrlCalled = url;

            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                var response = await http.GetAsync(url);
                result.HttpStatus = (int)response.StatusCode;
                result.HttpStatusText = response.StatusCode.ToString();

                var rawBody = await response.Content.ReadAsStringAsync();
                result.RawBodyPreview = rawBody.Length > 1500 ? rawBody[..1500] + "... [TRUNCATED]" : rawBody;

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(rawBody, JsonOptions);
                    if (data.TryGetProperty("hits", out var hits))
                    {
                        result.ParsedChildrenCount = hits.GetArrayLength();
                        result.ParsedOk = true;
                        result.ParsedTitles = hits.EnumerateArray()
                            .Take(5)
                            .Select(h => h.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "")
                            .ToList();
                    }
                }
                else
                {
                    result.Error = $"HTTP {result.HttpStatus}";
                }
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name} — {ex.Message}";
            }

            return Results.Ok(result);
        })
        .WithSummary("Audit HackerNews connector");

        // StackOverflow audit
        group.MapGet("/stackoverflow", async (
            string keyword,
            IHttpClientFactory httpClientFactory) =>
        {
            var result = new DiagnosticResult { Connector = "StackOverflow", Keyword = keyword };
            var url = $"https://api.stackexchange.com/2.3/search?order=desc&sort=activity&intitle={Uri.EscapeDataString(keyword)}&site=stackoverflow&pagesize=10&filter=withbody";
            result.UrlCalled = url;

            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                // StackOverflow returns gzip — use HttpCompletionOption to read stream
                var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                result.HttpStatus = (int)response.StatusCode;
                result.HttpStatusText = response.StatusCode.ToString();
                result.ResponseHeaders = response.Content.Headers
                    .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
                    var data = await JsonSerializer.DeserializeAsync<JsonElement>(gzip, JsonOptions);

                    if (data.TryGetProperty("items", out var items))
                    {
                        result.ParsedChildrenCount = items.GetArrayLength();
                        result.ParsedOk = true;
                        result.ParsedTitles = items.EnumerateArray()
                            .Take(5)
                            .Select(i => i.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "")
                            .ToList();
                    }

                    if (data.TryGetProperty("quota_remaining", out var quota))
                        result.Extra = $"Quota remaining: {quota.GetInt32()}";
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    result.Error = $"HTTP {result.HttpStatus} — {body[..Math.Min(500, body.Length)]}";
                }
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name} — {ex.Message}";
            }

            return Results.Ok(result);
        })
        .WithSummary("Audit StackOverflow connector");

        // AppStore audit
        group.MapGet("/appstore", async (
            string keyword,
            IHttpClientFactory httpClientFactory) =>
        {
            var result = new DiagnosticResult { Connector = "AppStore", Keyword = keyword };
            var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(keyword)}&entity=software&limit=3&country=us";
            result.UrlCalled = url;

            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                var response = await http.GetAsync(url);
                result.HttpStatus = (int)response.StatusCode;
                result.HttpStatusText = response.StatusCode.ToString();

                var rawBody = await response.Content.ReadAsStringAsync();
                result.RawBodyPreview = rawBody.Length > 1000 ? rawBody[..1000] + "... [TRUNCATED]" : rawBody;

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(rawBody, JsonOptions);
                    if (data.TryGetProperty("resultCount", out var count))
                    {
                        result.ParsedChildrenCount = count.GetInt32();
                        result.ParsedOk = true;
                        if (data.TryGetProperty("results", out var results))
                            result.ParsedTitles = results.EnumerateArray()
                                .Take(3)
                                .Select(r => r.TryGetProperty("trackName", out var n) ? n.GetString() ?? "" : "")
                                .ToList();
                    }
                }
                else
                {
                    result.Error = $"HTTP {result.HttpStatus}";
                }
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name} — {ex.Message}";
            }

            return Results.Ok(result);
        })
        .WithSummary("Audit AppStore connector");

        // ─── Full pipeline audit ────────────────────────────────────────────
        group.MapGet("/pipeline", async (
            string keyword,
            IServiceProvider sp) =>
        {
            var steps = new List<PipelineStep>();

            // Step 1: Keyword expansion
            var expansionStep = new PipelineStep { Name = "1. Keyword Expansion (Gemini)" };
            IReadOnlyList<string> batches = [keyword];
            try
            {
                var expansion = sp.GetRequiredService<IKeywordExpansionService>();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                batches = await expansion.BuildSearchBatchesAsync(keyword);
                sw.Stop();

                expansionStep.Ok = true;
                expansionStep.Detail = $"{batches.Count} batches in {sw.ElapsedMilliseconds}ms";
                expansionStep.Items = batches.ToList();
            }
            catch (Exception ex)
            {
                expansionStep.Ok = false;
                expansionStep.Detail = $"FAILED: {ex.GetType().Name} — {ex.Message}";
                expansionStep.Items = [keyword, "(expansion failed — using original only)"];
            }
            steps.Add(expansionStep);

            // Step 2: Connector calls — Reddit only (fast check)
            var redditStep = new PipelineStep { Name = "2. Reddit Connector" };
            try
            {
                var connectors = sp.GetRequiredService<IEnumerable<ISourceConnector>>();
                var reddit = connectors.FirstOrDefault(c => c.SourceType == SourceType.Reddit);

                if (reddit is null)
                {
                    redditStep.Ok = false;
                    redditStep.Detail = "RedditConnector NOT registered in DI";
                }
                else
                {
                    var perBatch = new List<string>();
                    var totalDocs = 0;
                    var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var batch in batches)
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var docs = await reddit.FetchDocumentsAsync(batch, null, null);
                        sw.Stop();

                        var newCount = docs.Count(d => seenUrls.Add(d.Url));
                        perBatch.Add($"  batch '{batch[..Math.Min(50, batch.Length)]}...' → {docs.Count} raw, {newCount} unique ({sw.ElapsedMilliseconds}ms)");
                        totalDocs += newCount;
                    }

                    redditStep.Ok = totalDocs > 0;
                    redditStep.Detail = $"{totalDocs} unique docs across {batches.Count} batches";
                    redditStep.Items = perBatch;
                }
            }
            catch (Exception ex)
            {
                redditStep.Ok = false;
                redditStep.Detail = $"FAILED: {ex.GetType().Name} — {ex.Message}";
            }
            steps.Add(redditStep);

            // Step 3: Check pending SearchRuns in DB
            var dbStep = new PipelineStep { Name = "3. Database — SearchRuns status" };
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PainFinderDbContext>();
                var runs = await db.SearchRuns
                    .OrderByDescending(r => r.StartedAt)
                    .Take(5)
                    .Select(r => new { r.Id, r.Keyword, r.Status, r.DocumentsCollected, r.PainsDetected, r.StartedAt })
                    .ToListAsync();

                dbStep.Ok = true;
                dbStep.Detail = $"{runs.Count} recent runs found";
                dbStep.Items = runs.Select(r =>
                    $"[{r.Status}] '{r.Keyword}' → {r.DocumentsCollected} docs, {r.PainsDetected} pains — {r.StartedAt:HH:mm:ss}").ToList();
            }
            catch (Exception ex)
            {
                dbStep.Ok = false;
                dbStep.Detail = $"DB ERROR: {ex.Message}";
            }
            steps.Add(dbStep);

            // Step 4: Check RawDocuments from keyword in DB
            var docsStep = new PipelineStep { Name = "4. Database — RawDocuments from Reddit" };
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PainFinderDbContext>();
                var count = await db.RawDocuments
                    .Where(d => d.Title.Contains("Reddit") && d.Title.Contains(keyword))
                    .CountAsync();

                var recent = await db.RawDocuments
                    .Where(d => d.Title.Contains("Reddit"))
                    .OrderByDescending(d => d.CollectedAt)
                    .Take(3)
                    .Select(d => new { d.Title, d.CollectedAt })
                    .ToListAsync();

                docsStep.Ok = count > 0;
                docsStep.Detail = $"{count} Reddit docs for '{keyword}' in DB";
                docsStep.Items = recent.Select(d => $"{d.Title[..Math.Min(80, d.Title.Length)]} — {d.CollectedAt:HH:mm:ss}").ToList();
            }
            catch (Exception ex)
            {
                docsStep.Ok = false;
                docsStep.Detail = $"DB ERROR: {ex.Message}";
            }
            steps.Add(docsStep);

            var allOk = steps.All(s => s.Ok);
            return Results.Ok(new
            {
                keyword,
                summary = allOk ? "✅ Pipeline healthy" : "⚠️ Pipeline has issues — check steps below",
                steps
            });
        })
        .WithSummary("Full pipeline audit: expansion → connector → DB save → DB query");

        // ─── Synchronous full search (bypass background services) ───────
        group.MapGet("/run-now", async (
            string keyword,
            string? source,
            IServiceProvider sp) =>
        {
            source ??= "Reddit";
            var log = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PainFinderDbContext>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var connectors = scope.ServiceProvider.GetRequiredService<IEnumerable<ISourceConnector>>();
                var keywordExpansion = scope.ServiceProvider.GetRequiredService<IKeywordExpansionService>();
                var painDetection = scope.ServiceProvider.GetRequiredService<IPainDetectionService>();
                var painClustering = scope.ServiceProvider.GetRequiredService<IPainClusteringService>();
                var opportunityGen = scope.ServiceProvider.GetRequiredService<IOpportunityGenerationService>();
                var sourceRepo = scope.ServiceProvider.GetRequiredService<IRepository<Source>>();

                // 1. Create SearchRun
                var run = new SearchRun
                {
                    Id = Guid.NewGuid(),
                    StartedAt = DateTime.UtcNow,
                    Status = SearchRunStatus.Scraping,
                    Sources = source,
                    Keyword = keyword
                };
                db.SearchRuns.Add(run);
                await unitOfWork.SaveChangesAsync();
                log.Add($"✓ SearchRun created: {run.Id}");

                // 2. Keyword expansion
                var batchSw = System.Diagnostics.Stopwatch.StartNew();
                var batches = await keywordExpansion.BuildSearchBatchesAsync(keyword);
                batchSw.Stop();
                log.Add($"✓ {batches.Count} batches in {batchSw.ElapsedMilliseconds}ms: [{string.Join(" | ", batches.Select(b => b[..Math.Min(40, b.Length)]))}]");

                // 3. Scrape — only the requested source
                var connector = connectors.FirstOrDefault(c =>
                    c.SourceType.ToString().Equals(source, StringComparison.OrdinalIgnoreCase));

                if (connector is null)
                {
                    log.Add($"✗ Connector '{source}' not found. Available: {string.Join(", ", connectors.Select(c => c.SourceType))}");
                    run.Status = SearchRunStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    await unitOfWork.SaveChangesAsync();
                    return Results.Ok(new { keyword, source, log, elapsed = sw.Elapsed.TotalSeconds });
                }

                // Create or get Source entity
                var sourceEntity = await db.Sources.FirstOrDefaultAsync(s => s.Type == connector.SourceType);
                if (sourceEntity is null)
                {
                    sourceEntity = new Source
                    {
                        Id = Guid.NewGuid(),
                        Name = connector.SourceType.ToString(),
                        Type = connector.SourceType,
                        BaseUrl = $"https://{connector.SourceType.ToString().ToLowerInvariant()}.com",
                        IsActive = true
                    };
                    await sourceRepo.AddAsync(sourceEntity);
                    await unitOfWork.SaveChangesAsync();
                }

                var totalDocs = 0;
                var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var batch in batches)
                {
                    var bSw = System.Diagnostics.Stopwatch.StartNew();
                    var docs = await connector.FetchDocumentsAsync(batch, null, null);
                    bSw.Stop();

                    var newDocs = new List<RawDocument>();
                    foreach (var doc in docs)
                    {
                        if (!seenUrls.Add(doc.Url)) continue;
                        doc.SourceId = sourceEntity.Id;
                        doc.SearchRunId = run.Id;
                        newDocs.Add(doc);
                    }

                    if (newDocs.Count > 0)
                        await db.RawDocuments.AddRangeAsync(newDocs);

                    totalDocs += newDocs.Count;
                    log.Add($"  batch '{batch[..Math.Min(35, batch.Length)]}...' → {docs.Count} raw, {newDocs.Count} new ({bSw.ElapsedMilliseconds}ms)");
                }

                run.DocumentsCollected = totalDocs;
                await unitOfWork.SaveChangesAsync();
                log.Add($"✓ Phase 1 done: {totalDocs} unique docs saved to DB");

                // 4. Analyze
                var runDocs = await db.RawDocuments.Where(d => d.SearchRunId == run.Id).ToListAsync();
                var allSignals = new List<PainSignal>();

                foreach (var doc in runDocs)
                {
                    var signals = await painDetection.DetectPainsAsync(doc);
                    allSignals.AddRange(signals);
                }

                if (allSignals.Count > 0)
                {
                    await db.PainSignals.AddRangeAsync(allSignals);
                    await unitOfWork.SaveChangesAsync();

                    var clusters = await painClustering.ClusterPainsAsync(allSignals);
                    await db.PainClusters.AddRangeAsync(clusters);
                    await unitOfWork.SaveChangesAsync();

                    var opps = await opportunityGen.GenerateOpportunitiesAsync(clusters);
                    await db.Opportunities.AddRangeAsync(opps);
                }

                run.PainsDetected = allSignals.Count;
                run.Status = SearchRunStatus.Completed;
                run.CompletedAt = DateTime.UtcNow;
                await unitOfWork.SaveChangesAsync();

                sw.Stop();
                log.Add($"✓ Phase 2 done: {allSignals.Count} pains detected");
                log.Add($"✓ COMPLETE in {sw.Elapsed.TotalSeconds:F1}s — {totalDocs} docs, {allSignals.Count} pains");

                return Results.Ok(new
                {
                    keyword,
                    source,
                    runId = run.Id,
                    status = "Completed",
                    documentsCollected = totalDocs,
                    painsDetected = allSignals.Count,
                    elapsed = $"{sw.Elapsed.TotalSeconds:F1}s",
                    log,
                    sampleTitles = runDocs.Take(5).Select(d => d.Title).ToList()
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                log.Add($"✗ FAILED: {ex.GetType().Name} — {ex.Message}");
                if (ex.InnerException is not null)
                    log.Add($"  inner: {ex.InnerException.Message}");

                return Results.Ok(new { keyword, source, status = "Failed", elapsed = $"{sw.Elapsed.TotalSeconds:F1}s", log });
            }
        })
        .WithSummary("Run a FULL synchronous search (scrape + analyze) — no background services");
    }

    private sealed class DiagnosticResult
    {
        public string Connector { get; set; } = "";
        public string Keyword { get; set; } = "";
        public string UrlCalled { get; set; } = "";
        public int HttpStatus { get; set; }
        public string HttpStatusText { get; set; } = "";
        public bool ParsedOk { get; set; }
        public int ParsedChildrenCount { get; set; }
        public List<string> ParsedTitles { get; set; } = [];
        public string? Error { get; set; }
        public string? Extra { get; set; }
        public string? RawBodyPreview { get; set; }
        public Dictionary<string, string>? ResponseHeaders { get; set; }
    }

    // Reddit inner models for debug
    private sealed class RedditListingDebug
    {
        public RedditListingDataDebug? Data { get; set; }
    }
    private sealed class RedditListingDataDebug
    {
        public List<RedditChildDebug>? Children { get; set; }
    }
    private sealed class RedditChildDebug
    {
        public RedditPostDebug? Data { get; set; }
    }
    private sealed class RedditPostDebug
    {
        public string? Title { get; set; }
        public string? Subreddit { get; set; }
    }

    private sealed class PipelineStep
    {
        public string Name { get; set; } = "";
        public bool Ok { get; set; }
        public string Detail { get; set; } = "";
        public List<string> Items { get; set; } = [];
    }
}
