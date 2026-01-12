using Migration.Common.Config;
using Migration.Common.Log;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var p = Args.Parse(args);

        using var httpAdo = CreateAdoClient(p.AdoPat);
        using var httpJira = CreateJiraClient(p.JiraEmail, p.JiraToken);

        switch (p.Action)
        {
            case "Parents":
                return await SetFeaturesParentsInADO(p, httpAdo, httpJira);
            case "Relations":
                return await SetRelationsInADO(p, httpAdo, httpJira);
            case "MarkMigrated":
                return await SetMigratedToADOInJira(p, httpAdo, httpJira);
            default: return 0;

        }

       
    }

   

    private static async Task<int> SetFeaturesParentsInADO(Params p, HttpClient httpAdo, HttpClient httpJira)
    {
        // 1. Get WorkItem IDs in ADO by Area Path + Legacy prefix
        var featureIds = await GetFeatureIdsByArea(httpAdo, p);
        if (featureIds.Count == 0)
        {
            Logger.Log(LogLevel.Info, "No workitems found.");
            return 0;
        }

        // 2. Batch-read Features
        var features = await BatchGetWorkItems(httpAdo, p, featureIds);

        // 3. Jira: get WorkItem -> Epic map (batched search)
        var jiraMap = await GetJiraFeatureEpicMap(httpJira, p);

        // 4. Resolve Epic IDs in ADO (cached)
        var epicKeys = jiraMap.Values.Distinct().ToList();
        var epicLinks = await ResolveEpicLinks(httpAdo, p, epicKeys);
        var epicADOIds = await GetEpicsFieldsByLink(httpAdo, epicLinks.Values.Distinct().ToList());
        // var test =  workitems.Where(m => m.HasParent == false).Select(m => new { m.LegacyId, m.Id }).ToList().ToDictionary(p=> p.LegacyId, p=> p.Id);

        // 5. Link
        foreach (var f in features)
        {
            if (f.HasParent) continue;
            if (!jiraMap.TryGetValue(f.LegacyId, out var aft)) continue;
            if (!epicADOIds.TryGetValue(aft, out var adoEpicId)) continue;
            await AddParentLinkInADO(httpAdo, p, f.Id, int.Parse(adoEpicId));
           

            
            //if (!jiraMap.TryGetValue(f.LegacyId, out var epicKey)) continue;
            ////if (!epicLinks.TryGetValue(epicKey, out var epicLink)) continue;
            //if (!epicADOIds.TryGetValue(epicKey, out var targetId)) continue;
            ////await AddParentLinkInADO(httpAdo, p, f.Id, targetId);
            Logger.Log(LogLevel.Info, $"LINKED {f.LegacyId} -> {aft}");

        }

        return 0;
    }

    private static async Task<int> SetRelationsInADO(Params p, HttpClient httpAdo, HttpClient httpJira)
    {
        // 1. Get WorkItem IDs in ADO by Area Path + Legacy prefix
        var featureIds = await GetWorkItemsIdsByArea(httpAdo, p);
        if (featureIds.Count == 0)
        {
            Logger.Log(LogLevel.Info, "No workitems found.");
            return 0;
        }

        // 2. Batch-read workitems (ADO) and issues
        var workitems = await BatchGetWorkItems(httpAdo, p, featureIds); // Get ADO workitems by LegacyID
        var adoByJiraKey = workitems
            .Where(m => !string.IsNullOrEmpty(m.LegacyId))
            .ToDictionary(m => m.LegacyId.Trim(), m => m, StringComparer.OrdinalIgnoreCase);

        var sourceKeys = adoByJiraKey.Keys.ToList();

        // 3. Batch-read issues (Jira)
        var jiraEdges = await GetJiraIssueRelations(httpJira, p, sourceKeys); // Get Jira relations

        var sources = jiraEdges.Select(m => m.SourceKey).ToList();
        var targets = jiraEdges.Select(m => m.TargetKey).ToList();

        List<string> allJiraKeys = new List<string>();
        allJiraKeys.AddRange(sources);
        allJiraKeys.AddRange(targets);
        var filteredJiraKeys = allJiraKeys.Distinct().ToList();


        //4. Batch-read workitems(ADO) of Jira relations Issues
        var jiraKeysInAdo = await GetFeatureIdsByLegacyID(httpAdo, p, filteredJiraKeys);
        var workItemsFromJira = await BatchGetWorkItems(httpAdo, p, jiraKeysInAdo);
        var adoRelationsByJiraKey = workItemsFromJira
            .Where(m => !string.IsNullOrEmpty(m.LegacyId))
            .ToDictionary(m => m.LegacyId.Trim(), m => m, StringComparer.OrdinalIgnoreCase);

       // var JiraInAdoEdges = jiraEdges.Where(m => adoRelationsByJiraKey.Select(m => m.Key).Contains(m.SourceKey) || adoRelationsByJiraKey.Select(m => m.Key).Contains(m.TargetKey)).ToList();

        int created = 0;

        foreach (var edge in jiraEdges)
        {
            if (!adoByJiraKey.TryGetValue(edge.SourceKey, out var sourceWi))
                continue; // source not in ADO selection (shouldn’t happen)

            if (!adoRelationsByJiraKey.TryGetValue(edge.TargetKey, out var targetWi))
                continue; // target not in ADO (or outside area selection) -> skip or resolve via extra WIQL

            var adoRel = MapJiraToAdoRel(edge.JiraLinkTypeName);
            if (adoRel is null) 
            {
                Logger.Log(LogLevel.Info, $"No link available in ADO for {edge.JiraLinkTypeName} on {edge.SourceKey} -> {edge.TargetKey}");
                continue;
            }

            var existing = BuildExistingSet(sourceWi);

            if (existing.Contains((adoRel, targetWi.Id)))
            {
                Logger.Log(LogLevel.Info, $"{edge.SourceKey} already linked as {adoRel} to {edge.TargetKey}, skipping");
                continue; // already linked
            }

            Logger.Log(LogLevel.Info, $"attempting to link {edge.SourceKey} as {adoRel} to {edge.TargetKey} with source:{sourceWi.LegacyId} and target {targetWi.LegacyId}");
            await AddRelationsInADO(httpAdo, p, sourceWi.Id, targetWi.Id, adoRel);
            Logger.Log(LogLevel.Info, $"{edge.SourceKey} linked as {adoRel} to {edge.TargetKey} with source:{sourceWi.LegacyId} and target {targetWi.LegacyId}");
            existing.Add((adoRel, targetWi.Id));
            created++;
        }
        return created;

       
    }

    private static async Task<int> SetMigratedToADOInJira(Params p, HttpClient httpAdo, HttpClient httpJira)
    {
        // 1. Get WorkItem IDs in ADO by Area Path + Legacy prefix
        var featureIds = await GetFeatureIdsByAreaAllWI(httpAdo, p);
        if (featureIds.Count == 0)
        {
            Logger.Log(LogLevel.Info, "No workitems found.");
            return 0;
        }

        // 2. Batch-read Features
        var features = await BatchGetWorkItems(httpAdo, p, featureIds);
        var featuresCleaned = features.Where(m => m.LegacyId != null).ToList();
        
        await BulkUpdateMigratedToAdo(p, httpJira, featuresCleaned);

        return 0;
    }


    #region Utils
    // ---------------- ADO ----------------

    static async Task<List<int>> GetFeatureIdsByArea(HttpClient http, Params p)
    {
        try
        {

            var wiql = $@"
SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{p.AdoProject}' AND [System.WorkItemType] = '{p.AdoWorkItemType}' AND [Custom.LegacyID] CONTAINS '{p.JiraProject}-' " + (string.IsNullOrEmpty(p.AreaPath) ? "" : $" AND [System.AreaPath] = '{p.AreaPath}'");

            var body = JsonSerializer.Serialize(new { query = wiql });

            var res = await http.PostAsync(
                $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/wiql?api-version=7.1",
                new StringContent(body, Encoding.UTF8, "application/json"));

            res.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            return doc.RootElement
                .GetProperty("workItems")
                .EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32())
                .ToList();
        }
        catch (Exception ex)
        {

            Logger.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    static async Task<List<int>> GetFeatureIdsByAreaAllWI(HttpClient http, Params p)
    {
        try
        {

            var wiql = $@"
SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{p.AdoProject}' AND [Custom.LegacyID] CONTAINS '{p.JiraProject}-' " + (string.IsNullOrEmpty(p.AreaPath) ? "" : $" AND [System.AreaPath] = '{p.AreaPath}'");

            var body = JsonSerializer.Serialize(new { query = wiql });

            var res = await http.PostAsync(
                $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/wiql?api-version=7.1",
                new StringContent(body, Encoding.UTF8, "application/json"));

            res.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            return doc.RootElement
                .GetProperty("workItems")
                .EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32())
                .ToList();
        }
        catch (Exception ex)
        {

            Logger.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    static async Task<List<int>> GetFeatureIdsByLegacyID(HttpClient http, Params p,List<string> legacyIds )
    {
        try
        {
            var orClause = string.Join(" OR ", legacyIds.Select( m=> $"[Custom.LegacyID] = '{m}'"));

            var wiql = $@"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{p.AdoProject}' AND ({orClause})";

            var body = JsonSerializer.Serialize(new { query = wiql });

            var res = await http.PostAsync(
                $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/wiql?api-version=7.1",
                new StringContent(body, Encoding.UTF8, "application/json"));

            res.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            return doc.RootElement
                .GetProperty("workItems")
                .EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32())
                .ToList();
        }
        catch (Exception ex)
        {

            Logger.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    static async Task<List<int>> GetWorkItemsIdsByArea(HttpClient http, Params p)
    {
        try
        {

            var wiql = $@"
SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{p.AdoProject}' AND [Custom.LegacyID] CONTAINS '{p.JiraProject}-' " + (string.IsNullOrEmpty(p.AreaPath) ? "" : $" AND [System.AreaPath] = '{p.AreaPath}'");

            var body = JsonSerializer.Serialize(new { query = wiql });

            var res = await http.PostAsync(
                $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/wiql?api-version=7.1",
                new StringContent(body, Encoding.UTF8, "application/json"));

            res.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            return doc.RootElement
                .GetProperty("workItems")
                .EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32())
                .ToList();
        }
        catch (Exception ex)
        {

            Logger.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    static async Task<Dictionary<string, string>> GetEpicsFieldsByLink(HttpClient http, List<string> links)
    {
        try
        {
            var map = new Dictionary<string, string>();
            foreach (string s in links)
            {
                var builder = new UriBuilder(s);


                var res = await http.GetAsync(builder.Uri).ConfigureAwait(false);
                res.EnsureSuccessStatusCode();

                var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync().ConfigureAwait(false));
                var root = doc.RootElement;
                var EpicId = root.GetProperty("id").GetInt32()!;
                var EpicLegacyID = root.GetProperty("fields").GetProperty("Custom.LegacyID").GetString()!;

                if (EpicLegacyID != null)
                {
                    map[EpicLegacyID] = EpicId.ToString();
                }
            }            

            return map;
           
        }
        catch (Exception ex)
        {

            Logger.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    static async Task<List<WorkItem>> BatchGetWorkItems(HttpClient http, Params p, List<int> ids)
    {
        var result = new List<WorkItem>();

        foreach (var chunk in ids.Chunk(200))
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    ids = chunk,
                    fields = new[] { p.AdoLegacyField, "System.WorkItemType" },
                    expand = "relations"
                });

                var res = await http.PostAsync(
                    $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/workitemsbatch?api-version=7.1",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                res.EnsureSuccessStatusCode();
                var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

                foreach (var wi in doc.RootElement.GetProperty("value").EnumerateArray())
                {
                    //var id = wi.GetProperty("id").GetInt32();
                    //var legacy = wi.GetProperty("fields").GetProperty(p.AdoLegacyField).GetString();
                    //var hasParent = wi.TryGetProperty("relations", out var rels) &&
                    //                rels.EnumerateArray().Any(r => r.GetProperty("rel").GetString() == "System.LinkTypes.Hierarchy-Reverse");
                    //var issueType = wi.GetProperty("fields").GetProperty("System.WorkItemType").GetString();

                    //result.Add(new WorkItem(id, legacy!, hasParent, issueType!));


                    var id = wi.GetProperty("id").GetInt32();
                    
                    
                    string legacy = "";
                    //ADO LegacyID
                    if(wi.TryGetProperty("fields", out var fields))
                    {
                        if (fields.TryGetProperty(p.AdoLegacyField, out var legacyEl) && legacyEl.ValueKind != JsonValueKind.Null)
                            legacy = legacyEl.GetString() ?? "";
                    }
                    
                    //ADO WorkItem Type
                    var issueType = "";
                    if(fields.ValueKind != JsonValueKind.Undefined && fields.TryGetProperty("System.WorkItemType", out var typeEl) && typeEl.ValueKind != JsonValueKind.Null)
                    {
                        issueType = typeEl.GetString() ?? "";
                    }

                    var relations = new List<WiRelation>();
                    if (wi.TryGetProperty("relations", out var relsEl) && relsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var r in relsEl.EnumerateArray())
                        {
                            // rel: "System.LinkTypes.Dependency-Forward", etc.
                            var rel = r.TryGetProperty("rel", out var relNameEl) && relNameEl.ValueKind != JsonValueKind.Null
                                ? (relNameEl.GetString() ?? "")
                                : "";

                            var url = r.TryGetProperty("url", out var urlEl) && urlEl.ValueKind != JsonValueKind.Null
                                ? (urlEl.GetString() ?? "")
                                : "";

                            // Extract target ID from URL ".../workItems/{id}"
                            // Example URL: https://dev.azure.com/org/_apis/wit/workItems/12345
                            var targetId = TryExtractWorkItemId(url);

                            if (!string.IsNullOrWhiteSpace(rel) && targetId > 0)
                                relations.Add(new WiRelation(rel, targetId, url));
                        }
                    }

                    // Your current "hasParent" check (still fine) now uses relations list
                    var hasParent = relations.Any(x => x.Rel == "System.LinkTypes.Hierarchy-Reverse");

                    result.Add(new WorkItem(id, legacy, hasParent, issueType, relations));


                }
            }
            catch (Exception ex)
            {

                Logger.Log(LogLevel.Error, ex.Message);
                
            }
        }
        return result;
    }

    static int TryExtractWorkItemId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return 0;

        // Fast path: last segment is the ID
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash == url.Length - 1) return 0;

        var last = url[(lastSlash + 1)..];

        // Sometimes URL can have querystring
        var q = last.IndexOf('?');
        if (q >= 0) last = last[..q];

        return int.TryParse(last, out var id) ? id : 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="http"></param>
    /// <param name="p"></param>
    /// <param name="featureId">FeatureId</param>
    /// <param name="epicId">ParentEpicId</param>
    /// <returns></returns>
    static async Task AddParentLinkInADO(HttpClient http, Params p, int featureId, int epicId)
    {
        var patch = JsonSerializer.Serialize(new[]
        {
            new {
                op = "add",
                path = "/relations/-",
                value = new {
                    rel = "System.LinkTypes.Hierarchy-Reverse",
                    url = $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/workItems/{epicId}",
                    attributes = new {
                        name ="Parent"
                    }
                }
            }
        });

        var req = new HttpRequestMessage(
            new HttpMethod("PATCH"),
            $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/workitems/{featureId}?api-version=7.1");

        req.Content = new StringContent(patch, Encoding.UTF8, "application/json-patch+json");
        var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }

    static async Task AddRelationsInADO(HttpClient http, Params p, int sourceId, int targetId, string relType)
    {
        try
        {
            var patch = JsonSerializer.Serialize(new[]
            {
            new {
                op = "add",
                path = "/relations/-",
                value = new {
                    rel = relType,
                    url = $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/workItems/{targetId}",
                    attributes = new {
                        comment ="Jira Link Sync"
                    }
                }
            }
        });

            var req = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/workitems/{sourceId}?api-version=7.1");

            req.Content = new StringContent(patch, Encoding.UTF8, "application/json-patch+json");
            var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {

            Logger.Log(LogLevel.Error, $"ERROR {targetId} to {sourceId} with rel {relType}: {ex.Message}");
        }
    }

    // ---------------- Jira ----------------

    static async Task<Dictionary<string, string>> GetJiraFeatureEpicMap(HttpClient http, Params p)
    {
        var map = new Dictionary<string, string>();
        int startAt = 0;

        while (true)
        {
           
            var builder = new UriBuilder($"{p.JiraBaseUrl}/rest/api/3/search/jql?jql={p.JiraIssuesQuery} and parent IS NOT EMPTY&fields=key,id,parent&maxResults=100&startAt={startAt}");
                       

            var res = await http.GetAsync(builder.Uri).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync().ConfigureAwait(false));

            foreach (var i in doc.RootElement.GetProperty("issues").EnumerateArray())
            {
                var featureKey = i.GetProperty("key").GetString()!;
                var parentKey = i.GetProperty("fields").GetProperty("parent").GetProperty("key").GetString()!;
                if(parentKey != null)
                {
                    map[featureKey] = parentKey;
                }
                
            }

            if (doc.RootElement.GetProperty("isLast").GetBoolean() == true)
                break;

            startAt += 100;
            await Task.Delay(150);
        }
        return map;
    }

    static async Task<List<issueRelation>> GetJiraIssueRelations(HttpClient http, Params p, List<string> keys)
    {
        var list = string.Join(",", keys.Select(k => $"{k}"));
        var rels = new List<issueRelation>();
        int startAt = 0;

        while (true)
        {
            //// var jql = $"key in ({list}) AND customfield_10893 = Yes";
            //var jql = $"key in ({list})";
            //// Use search endpoint; keep fields minimal
            //var builder = new UriBuilder($"{p.JiraBaseUrl}/rest/api/3/search");
            //var qs =
            //    $"jql={Uri.EscapeDataString(jql)}" +
            //    $"&fields={Uri.EscapeDataString("key,issuelinks,customfield_10893")}" +
            //    $"&maxResults=100&startAt={startAt}";
            //builder.Query = qs;

            //var res = await http.GetAsync(builder.Uri).ConfigureAwait(false);
            //res.EnsureSuccessStatusCode();
            if (startAt <= keys.Count)
            {
                var builder = new UriBuilder($"{p.JiraBaseUrl}/rest/api/3/search/jql?jql={p.JiraIssuesQuery}&fields=key,parent, issuelinks,customfield_10893&maxResults=100&startAt={startAt}");


                var res = await http.GetAsync(builder.Uri).ConfigureAwait(false);
                res.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync().ConfigureAwait(false));
                var root = doc.RootElement;
                bool isLast = false;
                foreach (var issue in root.GetProperty("issues").EnumerateArray())
                {
                    var currentKey = issue.GetProperty("key").GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(currentKey))
                        continue;

                    // OPTIONAL: filter sources by migrated flag
                    if (issue.TryGetProperty("fields", out var fields))
                    {
                        if (fields.TryGetProperty("customfield_10893", out var mig) && mig.ValueKind != JsonValueKind.Null)
                        {
                            var migVal = mig.GetString() ?? "";
                            if (!migVal.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                            {
                                if (fields.TryGetProperty("parent", out var parent) &&
                                            parent.TryGetProperty("key", out var parentKey) &&
                                            parentKey.ValueKind != JsonValueKind.Null)
                                {
                                    
                                    var targetKey = parentKey.GetString();
                                    if (!string.IsNullOrWhiteSpace(targetKey))
                                        rels.Add(new issueRelation(currentKey, targetKey!, "Parent", "outward"));
                                }

                                if (fields.TryGetProperty("issuelinks", out var linksEl) && linksEl.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var link in linksEl.EnumerateArray())
                                    {
                                        var typeName = "";
                                        if (link.TryGetProperty("type", out var typeEl) &&
                                            typeEl.TryGetProperty("name", out var nameEl) &&
                                            nameEl.ValueKind != JsonValueKind.Null)
                                        {
                                            typeName = nameEl.GetString() ?? "";
                                        }

                                        // parent: current -> parent
                                        

                                        // outwardIssue: current -> outward
                                        if (link.TryGetProperty("outwardIssue", out var outward) &&
                                            outward.TryGetProperty("key", out var outKeyEl) &&
                                            outKeyEl.ValueKind != JsonValueKind.Null)
                                        {
                                            var targetKey = outKeyEl.GetString();
                                            if (!string.IsNullOrWhiteSpace(targetKey))
                                                rels.Add(new issueRelation(currentKey, targetKey!, typeName, "outward"));
                                        }

                                        // inwardIssue: inward -> current
                                        if (link.TryGetProperty("inwardIssue", out var inward) &&
                                            inward.TryGetProperty("key", out var inKeyEl) &&
                                            inKeyEl.ValueKind != JsonValueKind.Null)
                                        {
                                            var sourceKey = inKeyEl.GetString();
                                            if (!string.IsNullOrWhiteSpace(sourceKey))
                                                rels.Add(new issueRelation(sourceKey!, currentKey, typeName, "inward"));
                                        }
                                    }
                                }
                            }

                        }
                        else
                        {
                            // If the field is missing/null, treat as not migrated
                            //continue;

                            if (fields.TryGetProperty("parent", out var parent) &&
                                            parent.TryGetProperty("key", out var parentKey) &&
                                            parentKey.ValueKind != JsonValueKind.Null)
                            {

                                var targetKey = parentKey.GetString();
                                if (!string.IsNullOrWhiteSpace(targetKey))
                                    rels.Add(new issueRelation(currentKey, targetKey!, "Parent", "outward"));
                            }


                            if (fields.TryGetProperty("issuelinks", out var linksEl) && linksEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var link in linksEl.EnumerateArray())
                                {
                                    var typeName = "";
                                    if (link.TryGetProperty("type", out var typeEl) &&
                                        typeEl.TryGetProperty("name", out var nameEl) &&
                                        nameEl.ValueKind != JsonValueKind.Null)
                                    {
                                        typeName = nameEl.GetString() ?? "";
                                    }

                                    // outwardIssue: current -> outward
                                    if (link.TryGetProperty("outwardIssue", out var outward) &&
                                        outward.TryGetProperty("key", out var outKeyEl) &&
                                        outKeyEl.ValueKind != JsonValueKind.Null)
                                    {
                                        var targetKey = outKeyEl.GetString();
                                        if (!string.IsNullOrWhiteSpace(targetKey))
                                            rels.Add(new issueRelation(currentKey, targetKey!, typeName, "outward"));
                                    }

                                    // inwardIssue: inward -> current
                                    if (link.TryGetProperty("inwardIssue", out var inward) &&
                                        inward.TryGetProperty("key", out var inKeyEl) &&
                                        inKeyEl.ValueKind != JsonValueKind.Null)
                                    {
                                        var sourceKey = inKeyEl.GetString();
                                        if (!string.IsNullOrWhiteSpace(sourceKey))
                                            rels.Add(new issueRelation(sourceKey!, currentKey, typeName, "inward"));
                                    }
                                }
                            }


                        }
                    }



                }
               
                startAt += 100;
                await Task.Delay(150);

            }
            else
            {
                break;
            }
        }

        return rels;
    }

    static async Task<Dictionary<string, string>> ResolveEpicLinks(HttpClient http, Params p, List<string> epicKeys)
    {
        var map = new Dictionary<string, string>();
        foreach (var chunk in epicKeys.Chunk(50))
        {
            var list = string.Join(",", chunk.Select(k => $"'{k}'"));
            var wiql = $"SELECT [System.Id],[Custom.LegacyID] FROM WorkItems WHERE [Custom.LegacyID] IN ({list})";

            var body = JsonSerializer.Serialize(new { query = wiql });
            var res = await http.PostAsync(
                $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/wiql?api-version=7.1-preview.2",
                new StringContent(body, Encoding.UTF8, "application/json"));

            res.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            foreach (var wi in doc.RootElement.GetProperty("workItems").EnumerateArray())
            {
               // Logger.Log(LogLevel.Info, "LegacyID=" + wi.GetProperty("Custom.LegacyID").GetString());
                map[wi.GetProperty("id").GetUInt32().ToString()!] =
                    wi.GetProperty("url").GetString()!;
            }
        }
        return map;
    }

    static async Task<Dictionary<string, string>> ResolveEpicParent(HttpClient http, Params p, List<string> epicKeys)
    {
        var map = new Dictionary<string, string>();
        foreach (var chunk in epicKeys.Chunk(50))
        {
            var list = string.Join(",", chunk.Select(k => $"'{k}'"));
            var wiql = $"SELECT [System.Id],[Custom.LegacyID] FROM WorkItems WHERE [Custom.LegacyID] IN ({list})";

            var body = JsonSerializer.Serialize(new { query = wiql });
            var res = await http.PostAsync(
                $"https://dev.azure.com/{p.AdoOrg}/{p.AdoProject}/_apis/wit/wiql?api-version=7.1",
                new StringContent(body, Encoding.UTF8, "application/json"));

            res.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            foreach (var wi in doc.RootElement.GetProperty("workItems").EnumerateArray())
            {
                // Logger.Log(LogLevel.Info, "LegacyID=" + wi.GetProperty("Custom.LegacyID").GetString());
                map[wi.GetProperty("id").GetString()!] =
                    wi.GetProperty("url").GetString()!;
            }
        }
        return map;
    }

    #endregion

    #region Update Migrated to ADO in Jira

    /// <summary>
    /// 
    /// </summary>
    /// <param name="http"></param>
    /// <param name="p"></param>
    /// <param name="issueKeys"></param>
    /// <param name="setToValue">Yes or No</param>
    /// <param name="concurrency">Default set to 4</param>
    /// <param name="sleepMsBetweenCalls">Default set to 150 Ms</param>
    /// <returns></returns>
    static async Task BulkUpdateMigratedToAdo(  Params p, HttpClient http, List<WorkItem> issueList, string setToValue = "Yes", int concurrency = 4, int sleepMsBetweenCalls = 150 )
    {

        var adoIssues = issueList.Select(m => m.LegacyId).ToList();

        if (adoIssues.Count > 0)
        {
            var keys = adoIssues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // 1) Get issue types in bulk
            var keyToTypeId = await GetIssueTypesByKey(http, p.JiraBaseUrl, keys);

            // 2) Resolve fieldId per issue type using one sample per type
            var typeToFieldId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in keyToTypeId.GroupBy(kvp => kvp.Value))
            {
                var typeId = group.Key;
                var sampleKey = group.First().Key;

                var fieldId = await ResolveMigratedFieldIdForIssueType(http, p.JiraBaseUrl, sampleKey);
                if (fieldId != null)
                {
                    typeToFieldId[typeId] = fieldId;
                }
                // else: that issue type doesn’t have the field on edit screen, skip updates for that type
                await Task.Delay(sleepMsBetweenCalls);
            }

            // 3) Update issues with controlled concurrency
            using var sem = new SemaphoreSlim(concurrency);
            var tasks = keys.Select(async key =>
            {
                await sem.WaitAsync();
                try
                {
                    if (!keyToTypeId.TryGetValue(key, out var typeId)) return;
                    if (!typeToFieldId.TryGetValue(typeId, out var fieldId)) return;

                    await UpdateMigratedField(http, p.JiraBaseUrl, key, fieldId, setToValue);
                    Logger.Log(LogLevel.Info, $"UPDATED {key} => {setToValue}");

                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, $"ERROR {key}: {ex.Message}");

                }
                finally
                {
                    await Task.Delay(sleepMsBetweenCalls);
                    sem.Release();
                }
            });

            await Task.WhenAll(tasks); 
        }
    }


    static async Task<Dictionary<string, string>> GetIssueTypesByKey(HttpClient http, string jiraBaseUrl, IEnumerable<string> keys)
    {
        // Returns: issueKey -> issueTypeId (or name, but id is better)
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var keyList = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Jira JQL "key in (...)" has practical length limits; chunk it.
        int startAt = 0;
        foreach (var chunk in keyList.Chunk(100))
        {
            var jql = $"key in ({string.Join(",", chunk)})";
            
            if (startAt <= keyList.Count )
            {
                await Task.Delay(150);
                var builder = new UriBuilder($"{jiraBaseUrl}/rest/api/3/search/jql?jql={jql} &fields=issuetype&maxResults=100&startAt={startAt}");


                var res = await http.GetAsync(builder.Uri).ConfigureAwait(false);
                res.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                var issues = doc.RootElement.GetProperty("issues");

                foreach (var issue in issues.EnumerateArray())
                {
                    var key = issue.GetProperty("key").GetString()!;
                    var typeId = issue.GetProperty("fields")
                                        .GetProperty("issuetype")
                                        .GetProperty("id")
                                        .GetString()!;
                    result[key] = typeId;
                }
                
                startAt += 100;
            
            }
            else
            {
                break;
            }

           
            
        }

        return result;
    }

    static async Task<string?> ResolveMigratedFieldIdForIssueType(HttpClient http, string jiraBaseUrl, string sampleIssueKey, string migratedFieldName = "Migrated to ADO?")
    {
        using var res = await http.GetAsync($"{jiraBaseUrl}/rest/api/3/issue/{sampleIssueKey}/editmeta");
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var fields = doc.RootElement.GetProperty("fields");

        foreach (var fieldProp in fields.EnumerateObject())
        {
            var fieldId = fieldProp.Name;              // e.g. "customfield_10893"
            var fieldObj = fieldProp.Value;

            if (fieldObj.TryGetProperty("name", out var nameEl) &&
                string.Equals(nameEl.GetString(), migratedFieldName, StringComparison.OrdinalIgnoreCase))
            {
                // Optional: validate it’s a single select by checking allowedValues/value shape
                return fieldId;
            }
        }

        return null;
    }

    static async Task UpdateMigratedField(HttpClient http, string jiraBaseUrl, string issueKey, string fieldId, string value) // "Yes" or "No"
    {
        var payload = JsonSerializer.Serialize(new
        {
            fields = new Dictionary<string, object>
            {
                [fieldId] = new { value }
            }
        });

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{jiraBaseUrl}/rest/api/3/issue/{issueKey}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var res = await http.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            Logger.Log(LogLevel.Error, $"Jira update failed for {issueKey}. Status={(int)res.StatusCode}. Body={body}");
            throw new Exception($"Jira update failed for {issueKey}. Status={(int)res.StatusCode}. Body={body}");
        }
        else
        {
            Logger.Log(LogLevel.Info, $"Jira field: {fieldId} updated to {value} for {issueKey}");
        }
    }

    #endregion

    #region Update Links in ADO

    /// <summary>
    /// Map relations from Jira to ADO
    /// </summary>
    /// <param name="jiraTypeName"></param>
    /// <returns>ADO Relation Type Name</returns>
    static string? MapJiraToAdoRel(string jiraTypeName)
    {
        var t = jiraTypeName.Trim().ToLowerInvariant();

        return t switch
        {
            //Hierarchy
            "child" => "System.LinkTypes.Hierarchy-Forward",
            "parent" => "System.LinkTypes.Hierarchy-Reverse",

            // Duplicate (name or phrases)
            "duplicate" => "System.LinkTypes.Duplicate-Forward",
            "duplicates" => "System.LinkTypes.Duplicate-Forward",
            "cloners" => "System.LinkTypes.Duplicate-Forward",
            "clones" => "System.LinkTypes.Duplicate-Forward",

            "is duplicated by" => "System.LinkTypes.Duplicate-Reverse",
            "is cloned by" => "System.LinkTypes.Duplicate-Reverse",
            "is duplicate by" => "System.LinkTypes.Duplicate-Reverse",  
            

            // Dependency (name or phrases)
            "blocks" => "System.LinkTypes.Dependency-Forward",
            "finish to start" => "System.LinkTypes.Dependency-Forward",
            "finish to finish" => "System.LinkTypes.Dependency-Forward",
            "start to start" => "System.LinkTypes.Dependency-Forward",
            "start to finish" => "System.LinkTypes.Dependency-Forward",

            "has to be done before" => "System.LinkTypes.Dependency-Forward",
            "has to be done after" => "System.LinkTypes.Dependency-Forward",
            "has to be started together with" => "System.LinkTypes.Dependency-Forward",
            "has to be finished together with" => "System.LinkTypes.Dependency-Forward",

            // Related (name or phrases)
            "relates" => "System.LinkTypes.Related",
            "relates to" => "System.LinkTypes.Related",

            "defect" => "System.LinkTypes.Related",
            "created" => "System.LinkTypes.Related",
            "created by" => "System.LinkTypes.Related",

            "problem/incident" => "System.LinkTypes.Related",
            "causes" => "System.LinkTypes.Related",
            "is caused by" => "System.LinkTypes.Related",

            "test" => "System.LinkTypes.Related",
            "tests" => "System.LinkTypes.Related",
            "is tested by" => "System.LinkTypes.Related",

            "work item split" => "System.LinkTypes.Related",
            "split to" => "System.LinkTypes.Related",
            "split from" => "System.LinkTypes.Related",

            "polaris work item link" => "System.LinkTypes.Related",
            "implements" => "System.LinkTypes.Related",
            "is implemented by" => "System.LinkTypes.Related",

            "polaris merge work item link" => "System.LinkTypes.Related",
            "merged into" => "System.LinkTypes.Related",
            "merged from" => "System.LinkTypes.Related",

            "polaris datapoint work item link" => "System.LinkTypes.Related",
            "added to idea" => "System.LinkTypes.Related",
            "is idea for" => "System.LinkTypes.Related",

            _ => null
        };
    }
    /// <summary>
    /// 1) BuildExistingSet: create a fast lookup of existing relations on a source work item 
    /// Assumes your work item type has: Relations = List<WiRelation> where WiRelation has Rel + TargetId
    /// </summary>
    /// <param name="wi">WorkItem</param>
    /// <returns></returns>
    static HashSet<(string Rel, int TargetId)> BuildExistingSet(WorkItem wi)
    {
        // If Feature.Relations is null-safe, remove the ?? part
        return (wi.Relations ?? new List<WiRelation>())
            .Where(r => !string.IsNullOrWhiteSpace(r.Rel) && r.TargetId > 0)
            .Select(r => (r.Rel, r.TargetId))
            .ToHashSet();
    }

    #endregion

    // ---------------- Helpers ----------------

    static HttpClient CreateAdoClient(string pat)
    {
      
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        
        return http;
    }

    static HttpClient CreateJiraClient(string email, string token)
    {
        var http = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{token}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        return http;
    }

 


}

record issueRelation(string SourceKey, string TargetKey, string JiraLinkTypeName, string Direction);
record WiRelation(string Rel, int TargetId, string Url);
record WorkItem(int Id, string LegacyId, bool HasParent, string IssueType, List<WiRelation> Relations);

record Params(
    string Action,
    string JiraProject,
    string JiraIssuesQuery,
    string JiraBaseUrl,
    string JiraEmail,
    string JiraToken,
    string AdoOrg,
    string AdoProject,
    string AdoPat,
    string AreaPath,
    string AdoWorkItemType,
    string AdoLegacyField //In ADO   
    
);

static class Args
{
    public static Params Parse(string[] a)
    {
        string Get(string k) => a.SkipWhile(x => x != $"--{k}").Skip(1).First();
        return new Params(
            Get("action"),
            Get("jiraProject"),
            Get("jiraIssuesQuery"),
            Get("jiraBaseUrl"),
            Get("jiraEmail"),
            Get("jiraToken"),
            Get("adoOrg"),
            Get("adoProject"),
            Get("adoPat"),
            a.Contains("--areaPath") ? Get("areaPath") : "Playground",
            a.Contains("--adoWorkItemType") ? Get("adoWorkItemType") : "workItem",
            "Custom.LegacyID"            
            
        );
    }
}