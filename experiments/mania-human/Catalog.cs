using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaHuman;

sealed class OsuSecrets
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUri { get; set; } = "http://127.0.0.1:7272/callback";
    public string? RefreshToken { get; set; }
    public string? ApiKey { get; set; }
}

sealed class Catalog
{
    public readonly string DataDir;
    public readonly string ReplayDir;
    public readonly string BeatmapDir;
    readonly Dictionary<long, ScoreMeta> manifest = new();
    readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
    };
    readonly JsonSerializerOptions json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    readonly OsuSecrets secrets;
    string? accessToken;
    DateTime tokenExpiry;
    DateTime nextCall = DateTime.MinValue;

    public Catalog(string dataDir)
    {
        DataDir = dataDir;
        ReplayDir = Path.Combine(dataDir, "replays");
        BeatmapDir = Path.Combine(dataDir, "beatmaps");
        Directory.CreateDirectory(ReplayDir);
        Directory.CreateDirectory(BeatmapDir);
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "maniac-lazer-mania-human/0.1");
        http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-version", "20220705");
        secrets = LoadSecrets();
        LoadManifest();
    }

    public bool HasV2 => !string.IsNullOrWhiteSpace(secrets.ClientId) && !string.IsNullOrWhiteSpace(secrets.ClientSecret);
    public bool HasUserToken => !string.IsNullOrWhiteSpace(secrets.RefreshToken);
    public bool HasV1 => !string.IsNullOrWhiteSpace(secrets.ApiKey);

    public string SecretsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "maniac-lazer", "osu-oauth.json");

    string ManifestPath => Path.Combine(DataDir, "manifest.json");

    void ClearReplays()
    {
        foreach (string f in Directory.EnumerateFiles(ReplayDir, "*.osr"))
            File.Delete(f);
        manifest.Clear();
        Console.WriteLine("cleared old replays");
    }

    void LoadManifest()
    {
        if (!File.Exists(ManifestPath))
            return;
        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, ScoreMeta>>(File.ReadAllText(ManifestPath), json);
            if (loaded is null) return;
            foreach (var kv in loaded)
            {
                if (long.TryParse(kv.Key, out long id))
                    manifest[id] = kv.Value;
            }
        }
        catch { /* start empty */ }
    }

    void SaveManifest()
    {
        var boxed = manifest.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(boxed, json));
    }

    public async Task LoginAsync()
    {
        if (!HasV2)
            throw new InvalidOperationException(CredentialHelp());
        string redirect = secrets.RedirectUri ?? "http://127.0.0.1:7272/callback";
        var uri = new Uri(redirect);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"{uri.Scheme}://{uri.Authority}/");
        listener.Start();
        string auth = "https://osu.ppy.sh/oauth/authorize"
                      + $"?client_id={Uri.EscapeDataString(secrets.ClientId!)}"
                      + $"&redirect_uri={Uri.EscapeDataString(redirect)}"
                      + "&response_type=code&scope=public";
        Console.WriteLine($"Opening browser for osu! OAuth. Redirect must be exactly: {redirect}");
        Process.Start(new ProcessStartInfo(auth) { UseShellExecute = true });
        var ctx = await listener.GetContextAsync();
        string? code = ctx.Request.QueryString["code"];
        byte[] ok = Encoding.UTF8.GetBytes("<html><body>maniac-lazer: login complete, you can close this tab.</body></html>");
        ctx.Response.ContentType = "text/html";
        ctx.Response.OutputStream.Write(ok);
        ctx.Response.Close();
        listener.Stop();
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("OAuth did not return a code.");
        await ExchangeAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirect,
            ["client_id"] = secrets.ClientId!,
            ["client_secret"] = secrets.ClientSecret!,
        }, saveRefresh: true);
        Console.WriteLine("Saved refresh token.");
    }

    public async Task<int> FetchAsync(int maps, int scoresPerMap, bool me, int? beatmapId, bool replace)
    {
        if (!HasV2 && !HasV1)
            throw new InvalidOperationException(CredentialHelp());

        if (replace)
            ClearReplays();

        int saved = 0;
        if (me)
        {
            var mine = await FetchMyScoresAsync(scoresPerMap * maps);
            foreach (var score in mine)
                saved += await TrySaveScoreAsync(score) ? 1 : 0;
            SaveManifest();
            return saved;
        }

        if (beatmapId is int oneId)
        {
            saved += await FetchLeaderboardSpreadAsync(oneId, stars: 0, scoresPerMap);
            SaveManifest();
            return saved;
        }

        var hard = await SearchHardManiaMapsAsync(maps);
        foreach (var map in hard)
        {
            Console.WriteLine($"scores  #{map.Id}  {map.Stars:0.0}*  {map.Keys}K");
            saved += await FetchLeaderboardSpreadAsync(map.Id, map.Stars, scoresPerMap);
        }

        saved += await FetchMidTierRecentAsync(12);
        SaveManifest();
        return saved;
    }

    public ScoreMeta? MetaFor(OsrReplay replay, string? path)
    {
        if (replay.OnlineId > 0 && manifest.TryGetValue(replay.OnlineId, out var byOnline))
            return byOnline;
        if (path is not null)
        {
            string name = Path.GetFileName(path);
            int dash = name.IndexOf('-');
            if (dash > 0 && long.TryParse(name[..dash], out long fromName)
                && manifest.TryGetValue(fromName, out var byFile))
                return byFile;
        }

        return null;
    }

    public async Task<ManiaBeatmap?> ResolveBeatmapAsync(OsrReplay replay)
    {
        string md5 = replay.BeatmapMd5.ToLowerInvariant();
        string cached = Path.Combine(BeatmapDir, md5 + ".osu");
        if (File.Exists(cached))
            return ReplayIO.ParseOsu(cached);

        foreach (string existing in Directory.EnumerateFiles(BeatmapDir, "*.osu"))
        {
            try
            {
                var map = ReplayIO.ParseOsu(existing);
                if (map.Md5.Equals(md5, StringComparison.OrdinalIgnoreCase))
                    return map;
            }
            catch { /* skip */ }
        }

        int? onlineId = null;
        if (HasV2)
        {
            try
            {
                using var doc = await GetJsonAsync($"https://osu.ppy.sh/api/v2/beatmaps/lookup?checksum={md5}");
                if (Long(doc.RootElement, "id") is long bid)
                    onlineId = (int)bid;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"lookup {md5}: {ex.Message}");
            }
        }

        if (onlineId is int bid2)
        {
            var downloaded = await DownloadBeatmapAsync(bid2, md5);
            if (downloaded is not null)
                return downloaded;
        }

        return FindLocalBeatmap(md5);
    }

    public int ScanLocal()
    {
        int n = 0;
        n += ScanStable();
        n += ScanLazerFiles();
        return n;
    }

    public IEnumerable<string> ReplayFiles()
        => Directory.EnumerateFiles(ReplayDir, "*.osr", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(DataDir, "*.osr", SearchOption.TopDirectoryOnly));

    string CredentialHelp() => """
        No osu! API credentials. Either:
          1. Create an OAuth app at https://osu.ppy.sh/home/account/edit#oauth
             Callback: http://127.0.0.1:7272/callback
             Then set OSU_CLIENT_ID / OSU_CLIENT_SECRET (or write %APPDATA%\maniac-lazer\osu-oauth.json)
             and run:  dotnet run --project experiments/mania-human -- login
          2. Set OSU_API_KEY to a legacy v1 key (https://osu.ppy.sh/p/api) — stable replays only
          3. Skip fetch: export .osr from the game into experiments/mania-human/data/replays
             or run:  dotnet run --project experiments/mania-human -- scan
        """;

    OsuSecrets LoadSecrets()
    {
        var s = new OsuSecrets();
        foreach (string path in SecretCandidates())
        {
            if (!File.Exists(path))
                continue;
            try
            {
                s = JsonSerializer.Deserialize<OsuSecrets>(File.ReadAllText(path), json) ?? s;
                break;
            }
            catch { /* keep defaults */ }
        }

        s.ClientId = Env("OSU_CLIENT_ID") ?? s.ClientId;
        s.ClientSecret = Env("OSU_CLIENT_SECRET") ?? s.ClientSecret;
        s.RefreshToken = Env("OSU_REFRESH_TOKEN") ?? s.RefreshToken;
        s.ApiKey = Env("OSU_API_KEY") ?? s.ApiKey;
        s.RedirectUri = Env("OSU_REDIRECT_URI") ?? s.RedirectUri ?? "http://127.0.0.1:7272/callback";
        return s;
    }

    IEnumerable<string> SecretCandidates()
    {
        yield return SecretsPath;
        yield return Path.Combine(DataDir, "..", "osu-oauth.json");
        // people sometimes create a literal "%APPDATA%" folder in the repo
        string cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "%APPDATA%", "maniac-lazer", "osu-oauth.json");
        yield return Path.Combine(cwd, "experiments", "mania-human", "osu-oauth.json");
    }

    void SaveSecrets()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsPath)!);
        File.WriteAllText(SecretsPath, JsonSerializer.Serialize(secrets, json));
    }

    static string? Env(string name)
    {
        string? v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    async Task EnsureTokenAsync(bool preferUser)
    {
        if (accessToken is not null && DateTime.UtcNow < tokenExpiry)
            return;
        if (preferUser && HasUserToken)
        {
            await ExchangeAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = secrets.RefreshToken!,
                ["client_id"] = secrets.ClientId!,
                ["client_secret"] = secrets.ClientSecret!,
            }, saveRefresh: true);
            return;
        }

        if (!HasV2)
            return;
        await ExchangeAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = secrets.ClientId!,
            ["client_secret"] = secrets.ClientSecret!,
            ["scope"] = "public",
        }, saveRefresh: false);
    }

    async Task ExchangeAsync(Dictionary<string, string> form, bool saveRefresh)
    {
        using var content = new FormUrlEncodedContent(form);
        using var resp = await http.PostAsync("https://osu.ppy.sh/oauth/token", content);
        string body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"oauth token: {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        accessToken = doc.RootElement.GetProperty("access_token").GetString();
        int expires = doc.RootElement.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 3600;
        tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(30, expires - 30));
        if (saveRefresh && doc.RootElement.TryGetProperty("refresh_token", out var rt))
        {
            secrets.RefreshToken = rt.GetString();
            SaveSecrets();
        }
    }

    async Task PaceAsync()
    {
        var wait = nextCall - DateTime.UtcNow;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait);
        nextCall = DateTime.UtcNow.AddMilliseconds(1100);
    }

    async Task<JsonDocument> GetJsonAsync(string url, bool user = false)
    {
        await EnsureTokenAsync(user);
        await PaceAsync();
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (accessToken is not null)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await http.SendAsync(req);
        string body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"{resp.StatusCode} {url}: {Trim(body)}");
        return JsonDocument.Parse(body);
    }

    async Task<List<MapRef>> SearchHardManiaMapsAsync(int maps)
    {
        var found = new List<MapRef>();
        using var doc = await GetJsonAsync("https://osu.ppy.sh/api/v2/beatmapsets/search?m=3&s=ranked&sort=difficulty_desc");
        if (!doc.RootElement.TryGetProperty("beatmapsets", out var sets))
            return found;
        foreach (var set in sets.EnumerateArray())
        {
            if (!set.TryGetProperty("beatmaps", out var beatmaps))
                continue;
            MapRef? best = null;
            foreach (var b in beatmaps.EnumerateArray())
            {
                string mode = b.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";
                bool convert = b.TryGetProperty("convert", out var c) && c.ValueKind is JsonValueKind.True;
                if (mode != "mania" || convert)
                    continue;
                int keys = (int)Math.Round(Num(b, "cs"));
                if (keys is not (4 or 7))
                    continue;
                double stars = Num(b, "difficulty_rating");
                if (stars < 6)
                    continue;
                if (Num(b, "playcount") < 3000)
                    continue;
                if (best is null || stars > best.Stars)
                {
                    long id = Long(b, "id") ?? 0;
                    if (id <= 0) continue;
                    best = new MapRef { Id = (int)id, Stars = stars, Keys = keys };
                }
            }

            if (best is not null)
            {
                found.Add(best);
                if (found.Count >= maps)
                    break;
            }
        }

        return found;
    }

    async Task<int> FetchLeaderboardSpreadAsync(int beatmapId, double stars, int take)
    {
        var all = await ListScoresAsync(beatmapId, int.MaxValue);
        var spread = Spread(all, Math.Max(take, 1));
        int saved = 0;
        for (int i = 0; i < spread.Count; i++)
        {
            var score = spread[i];
            score.Stars = stars;
            score.Source = "leaderboard";
            if (score.LeaderboardRank is null)
                score.LeaderboardRank = all.IndexOf(score) + 1;
            if (await TrySaveScoreAsync(score))
                saved++;
        }

        return saved;
    }

    async Task<int> FetchMidTierRecentAsync(int want)
    {
        int saved = 0;
        // page 50 ≈ rank 2500, page 150 ≈ 7500, page 400 ≈ 20000 (50 users/page)
        foreach (int page in new[] { 50, 150, 400 })
        {
            Console.WriteLine($"recent  mania pp page {page}");
            List<int> users;
            try { users = await RankingUserIdsAsync(page); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"rankings page {page}: {ex.Message}");
                continue;
            }

            foreach (int userId in users.Take(6))
            {
                if (saved >= want)
                    return saved;
                try
                {
                    using var doc = await GetJsonAsync($"https://osu.ppy.sh/api/v2/users/{userId}/scores/recent?mode=mania&limit=20&include_fails=1");
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var s in doc.RootElement.EnumerateArray())
                    {
                        int bid = (int)(Long(s, "beatmap_id") ?? 0);
                        var parsed = ParseV2Score(s, bid);
                        if (parsed is null || !UsableScore(parsed))
                            continue;
                        parsed.Source = "recent";
                        parsed.UserGlobalRank = page * 50;
                        if (s.TryGetProperty("beatmap", out var bm) && bm.ValueKind == JsonValueKind.Object)
                            parsed.Stars = Num(bm, "difficulty_rating");
                        if (await TrySaveScoreAsync(parsed))
                        {
                            saved++;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"recent {userId}: {ex.Message}");
                }
            }
        }

        return saved;
    }

    async Task<List<int>> RankingUserIdsAsync(int page)
    {
        using var doc = await GetJsonAsync($"https://osu.ppy.sh/api/v2/rankings/mania/performance?page={page}&variant=4k");
        var ids = new List<int>();
        if (!doc.RootElement.TryGetProperty("ranking", out var ranking))
            return ids;
        foreach (var row in ranking.EnumerateArray())
        {
            if (!row.TryGetProperty("user", out var user))
                continue;
            if (Long(user, "id") is long id and > 0)
                ids.Add((int)id);
        }

        return ids;
    }

    static List<ScoreRef> Spread(List<ScoreRef> all, int take)
    {
        if (all.Count == 0 || take <= 0)
            return [];
        if (all.Count <= take)
        {
            for (int i = 0; i < all.Count; i++)
                all[i].LeaderboardRank ??= i + 1;
            return all;
        }

        var picked = new List<ScoreRef>();
        var used = new HashSet<int>();
        for (int i = 0; i < take; i++)
        {
            int idx = (int)Math.Round(i * (all.Count - 1) / (double)(take - 1));
            while (used.Contains(idx) && idx < all.Count - 1)
                idx++;
            used.Add(idx);
            all[idx].LeaderboardRank = idx + 1;
            picked.Add(all[idx]);
        }

        return picked;
    }

    async Task<List<int>> SearchManiaMapsAsync(int maps)
    {
        var ids = new List<int>();
        using var doc = await GetJsonAsync("https://osu.ppy.sh/api/v2/beatmapsets/search?m=3&s=ranked&sort=plays_desc");
        if (!doc.RootElement.TryGetProperty("beatmapsets", out var sets))
            return ids;
        foreach (var set in sets.EnumerateArray())
        {
            if (!set.TryGetProperty("beatmaps", out var beatmaps))
                continue;
            JsonElement? best = null;
            int bestPlays = -1;
            foreach (var b in beatmaps.EnumerateArray())
            {
                string mode = b.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";
                bool convert = b.TryGetProperty("convert", out var c) && c.ValueKind is JsonValueKind.True;
                if (mode != "mania" || convert)
                    continue;
                double cs = Num(b, "cs");
                int keys = (int)Math.Round(cs);
                if (keys is not (4 or 7))
                    continue;
                int plays = (int)Num(b, "playcount");
                if (plays > bestPlays)
                {
                    bestPlays = plays;
                    best = b;
                }
            }

            if (best is { } pick && Long(pick, "id") is long id and > 0)
            {
                ids.Add((int)id);
                if (ids.Count >= maps)
                    break;
            }
        }

        return ids;
    }

    async Task<List<ScoreRef>> ListScoresAsync(int beatmapId, int take)
    {
        var list = new List<ScoreRef>();
        if (HasV2)
        {
            try
            {
                using var doc = await GetJsonAsync($"https://osu.ppy.sh/api/v2/beatmaps/{beatmapId}/scores?mode=mania&type=global");
                if (doc.RootElement.TryGetProperty("scores", out var scores))
                {
                    foreach (var s in scores.EnumerateArray())
                    {
                        var parsed = ParseV2Score(s, beatmapId);
                        if (parsed is not null)
                            list.Add(parsed);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"v2 scores {beatmapId}: {ex.Message}");
            }
        }

        if (list.Count == 0 && HasV1)
            list.AddRange(await ListV1ScoresAsync(beatmapId));

        return list.Where(UsableScore).Take(take).ToList();
    }

    async Task<List<ScoreRef>> FetchMyScoresAsync(int take)
    {
        await EnsureTokenAsync(preferUser: true);
        if (!HasUserToken)
            throw new InvalidOperationException("fetch --me needs `login` first.");
        using var me = await GetJsonAsync("https://osu.ppy.sh/api/v2/me", user: true);
        int userId = me.RootElement.GetProperty("id").GetInt32();
        var list = new List<ScoreRef>();
        using var doc = await GetJsonAsync($"https://osu.ppy.sh/api/v2/users/{userId}/scores/best?mode=mania&limit=100", user: true);
        foreach (var s in doc.RootElement.EnumerateArray())
        {
            int bid = (int)(Long(s, "beatmap_id") ?? 0);
            var parsed = ParseV2Score(s, bid);
            if (parsed is not null && UsableScore(parsed))
                list.Add(parsed);
            if (list.Count >= take)
                break;
        }

        return list;
    }

    async Task<bool> TrySaveScoreAsync(ScoreRef score)
    {
        string dest = Path.Combine(ReplayDir, $"{score.Id}-{Safe(score.Player)}.osr");
        if (File.Exists(dest))
            return true;
        if (score.Mods.Any(m => m is "RD" or "AU" or "CN" or "RX" or "AP"))
            return false;

        byte[]? bytes = null;
        try { bytes = await DownloadV2ReplayAsync(score.Id); }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) { Console.Error.WriteLine($"v2 replay {score.Id}: {ex.Message}"); }

        if (bytes is null && HasV1 && score.LegacyId is long lid)
        {
            try { bytes = await DownloadV1ReplayAsOsrAsync(score, lid); }
            catch (Exception ex) { Console.Error.WriteLine($"v1 replay {lid}: {ex.Message}"); }
        }

        if (bytes is null || bytes.Length < 16)
            return false;
        await File.WriteAllBytesAsync(dest, bytes);
        manifest[score.Id] = new ScoreMeta
        {
            LeaderboardRank = score.LeaderboardRank,
            Accuracy = score.Accuracy,
            Stars = score.Stars,
            Source = score.Source,
            UserGlobalRank = score.UserGlobalRank,
            BeatmapId = score.BeatmapId,
            Player = score.Player,
        };
        if (score.BeatmapId > 0)
            await DownloadBeatmapAsync(score.BeatmapId, expectedMd5: null);
        Console.WriteLine($"  saved {Path.GetFileName(dest)}  {score.Player}  {score.Accuracy:p1}  rank={score.LeaderboardRank?.ToString() ?? "-"}  {score.Source}  {string.Join("", score.Mods)}");
        return true;
    }

    async Task<byte[]?> DownloadV2ReplayAsync(long scoreId)
    {
        await EnsureTokenAsync(preferUser: HasUserToken);
        await PaceAsync();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://osu.ppy.sh/api/v2/scores/{scoreId}/download");
        if (accessToken is not null)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Accept.ParseAdd("application/octet-stream");
        using var resp = await http.SendAsync(req);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if (!HasUserToken)
                throw new InvalidOperationException("Replay download needs a user token. Run `login` after creating an OAuth app.");
            return null;
        }

        if (!resp.IsSuccessStatusCode)
            return null;
        return await resp.Content.ReadAsByteArrayAsync();
    }

    async Task<List<ScoreRef>> ListV1ScoresAsync(int beatmapId)
    {
        await PaceAsync();
        string url = $"https://osu.ppy.sh/api/get_scores?k={Uri.EscapeDataString(secrets.ApiKey!)}&b={beatmapId}&m=3&limit=20";
        using var resp = await http.GetAsync(url);
        string body = await resp.Content.ReadAsStringAsync();
        var list = new List<ScoreRef>();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var s in doc.RootElement.EnumerateArray())
        {
            bool replay = s.TryGetProperty("replay_available", out var ra) && ra.ToString() is "1" or "true";
            if (!replay)
                continue;
            long sid = s.TryGetProperty("score_id", out var idEl) && idEl.TryGetInt64(out long id) ? id : 0;
            list.Add(new ScoreRef
            {
                Id = sid,
                LegacyId = sid,
                BeatmapId = beatmapId,
                Player = s.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                Accuracy = 0,
                HasReplay = true,
                Mods = SplitLegacyMods(s.TryGetProperty("enabled_mods", out var md) && int.TryParse(md.ToString(), out int mv) ? mv : 0),
            });
        }

        return list;
    }

    async Task<byte[]?> DownloadV1ReplayAsOsrAsync(ScoreRef score, long legacyId)
    {
        string? md5 = null;
        if (score.BeatmapId > 0)
        {
            var map = await DownloadBeatmapAsync(score.BeatmapId, expectedMd5: null);
            md5 = map?.Md5;
        }

        await PaceAsync();
        string url = $"https://osu.ppy.sh/api/get_replay?k={Uri.EscapeDataString(secrets.ApiKey!)}&s={legacyId}&m=3";
        using var resp = await http.GetAsync(url);
        string body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("content", out var content))
            return null;
        string b64 = content.GetString() ?? "";
        if (b64.Length == 0)
            return null;
        byte[] lzma = Convert.FromBase64String(b64);
        return WrapOsr(score, md5 ?? "", lzma);
    }

    static byte[] WrapOsr(ScoreRef score, string beatmapMd5, byte[] compressedReplay)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        bw.Write((byte)GameMode.Mania);
        bw.Write(20140721);
        WriteOsuString(bw, beatmapMd5);
        WriteOsuString(bw, score.Player);
        WriteOsuString(bw, "");
        for (int i = 0; i < 6; i++) bw.Write((ushort)0);
        bw.Write(0); // score
        bw.Write((ushort)0); // combo
        bw.Write((byte)0); // perfect
        bw.Write(0); // mods - unknown from acronyms alone is ok; matcher still works
        WriteOsuString(bw, "");
        bw.Write(DateTime.UtcNow.Ticks);
        bw.Write(compressedReplay.Length);
        bw.Write(compressedReplay);
        bw.Write(score.LegacyId ?? score.Id);
        bw.Flush();
        return ms.ToArray();
    }

    static void WriteOsuString(BinaryWriter bw, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            bw.Write((byte)0);
            return;
        }

        bw.Write((byte)0x0b);
        bw.Write(value);
    }

    async Task<ManiaBeatmap?> DownloadBeatmapAsync(int beatmapId, string? expectedMd5)
    {
        string[] urls =
        [
            $"https://osu.direct/api/osu/{beatmapId}",
            $"https://catboy.best/osu/{beatmapId}",
        ];
        foreach (string url in urls)
        {
            try
            {
                await PaceAsync();
                using var resp = await http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                    continue;
                byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
                if (bytes.Length < 32 || !LooksLikeOsu(bytes))
                    continue;
                var map = ReplayIO.ParseOsu(bytes);
                if (!map.IsMania)
                    continue;
                string md5 = string.IsNullOrEmpty(expectedMd5) ? map.Md5 : expectedMd5;
                string path = Path.Combine(BeatmapDir, md5 + ".osu");
                await File.WriteAllBytesAsync(path, bytes);
                map.SourcePath = path;
                map.BeatmapId = map.BeatmapId == 0 ? beatmapId : map.BeatmapId;
                return map;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"beatmap {beatmapId} via {url}: {ex.Message}");
            }
        }

        return null;
    }

    int ScanStable()
    {
        int n = 0;
        foreach (string root in StableRoots())
        {
            string replays = Path.Combine(root, "Replays");
            if (Directory.Exists(replays))
            {
                foreach (string file in Directory.EnumerateFiles(replays, "*.osr"))
                    n += ImportReplay(file) ? 1 : 0;
            }

            string songs = Path.Combine(root, "Songs");
            if (Directory.Exists(songs))
            {
                foreach (string osu in Directory.EnumerateFiles(songs, "*.osu", SearchOption.AllDirectories))
                    ImportBeatmap(osu);
            }
        }

        return n;
    }

    int ScanLazerFiles()
    {
        int n = 0;
        foreach (string root in LazerRoots())
        {
            string files = Path.Combine(root, "files");
            if (!Directory.Exists(files))
                continue;
            Console.WriteLine($"scan    {files}");
            int seen = 0;
            foreach (string file in Directory.EnumerateFiles(files, "*", SearchOption.AllDirectories))
            {
                seen++;
                if (seen % 2000 == 0)
                    Console.WriteLine($"        {seen} files, {n} mania replays");
                var info = new FileInfo(file);
                if (info.Length is < 64 or > 2_000_000)
                    continue;
                try
                {
                    byte[] head = new byte[Math.Min(24, (int)info.Length)];
                    using (var fs = File.OpenRead(file))
                        _ = fs.Read(head);
                    if (LooksLikeOsu(head))
                    {
                        ImportBeatmap(file);
                        continue;
                    }

                    if (head[0] == (byte)GameMode.Mania && ReplayIO.TryParseOsr(file, out var replay))
                    {
                        string dest = Path.Combine(ReplayDir, $"{Safe(replay.Player)}-{ShortHash(replay.ReplayMd5)}.osr");
                        if (!File.Exists(dest))
                            File.Copy(file, dest, overwrite: false);
                        n++;
                    }
                }
                catch
                {
                    // hashed blobs of unknown type
                }
            }
        }

        return n;
    }

    bool ImportReplay(string path)
    {
        try
        {
            if (!ReplayIO.TryParseOsr(path, out var replay))
                return false;
            string dest = Path.Combine(ReplayDir, Path.GetFileName(path));
            if (!File.Exists(dest))
                File.Copy(path, dest, overwrite: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    void ImportBeatmap(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (!LooksLikeOsu(bytes))
                return;
            var map = ReplayIO.ParseOsu(bytes);
            if (!map.IsMania)
                return;
            string dest = Path.Combine(BeatmapDir, map.Md5 + ".osu");
            if (!File.Exists(dest))
                File.WriteAllBytes(dest, bytes);
        }
        catch { /* ignore */ }
    }

    ManiaBeatmap? FindLocalBeatmap(string md5)
    {
        foreach (string root in StableRoots())
        {
            string songs = Path.Combine(root, "Songs");
            if (!Directory.Exists(songs))
                continue;
            foreach (string path in Directory.EnumerateFiles(songs, "*.osu", SearchOption.AllDirectories))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    if (!LooksLikeOsu(bytes))
                        continue;
                    string hash = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
                    if (!hash.Equals(md5, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string dest = Path.Combine(BeatmapDir, md5 + ".osu");
                    File.WriteAllBytes(dest, bytes);
                    return ReplayIO.ParseOsu(dest);
                }
                catch { /* next */ }
            }
        }

        return null;
    }

    static IEnumerable<string> StableRoots()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        [
            Path.Combine(local, "osu!"),
            @"C:\osu!",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "osu!"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "osu!"),
        ];
        foreach (string c in candidates)
        {
            if (Directory.Exists(Path.Combine(c, "Replays")) || Directory.Exists(Path.Combine(c, "Songs")))
                yield return c;
        }
    }

    static IEnumerable<string> LazerRoots()
    {
        string app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (string name in new[] { "osu", "osu-lazer" })
        {
            string dir = Path.Combine(app, name);
            if (Directory.Exists(dir))
                yield return dir;
        }
    }

    static ScoreRef? ParseV2Score(JsonElement s, int beatmapId)
    {
        if (s.TryGetProperty("has_replay", out var hr) && hr.ValueKind == JsonValueKind.False)
            return null;
        long id = Long(s, "id") ?? 0;
        if (id == 0)
            return null;
        var mods = new List<string>();
        if (s.TryGetProperty("mods", out var modsEl) && modsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in modsEl.EnumerateArray())
            {
                if (m.TryGetProperty("acronym", out var a) && a.GetString() is string ac)
                    mods.Add(ac);
            }
        }

        int bid = beatmapId;
        if (Long(s, "beatmap_id") is long bid2 and > 0)
            bid = (int)bid2;
        else if (s.TryGetProperty("beatmap", out var be) && be.ValueKind == JsonValueKind.Object
                 && Long(be, "id") is long bid3 and > 0)
            bid = (int)bid3;

        long? legacy = Long(s, "legacy_score_id");
        string player = "";
        if (s.TryGetProperty("user", out var user) && user.TryGetProperty("username", out var un))
            player = un.GetString() ?? "";
        double acc = Num(s, "accuracy");
        return new ScoreRef
        {
            Id = id,
            LegacyId = legacy,
            BeatmapId = bid,
            Player = player,
            Accuracy = acc,
            HasReplay = true,
            Mods = mods.ToArray(),
        };
    }

    static long? Long(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Number)
            return null;
        if (p.TryGetInt64(out long v))
            return v;
        if (p.TryGetDouble(out double d))
            return (long)d;
        return null;
    }

    static double Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Number)
            return 0;
        return p.TryGetDouble(out double v) ? v : 0;
    }

    static bool UsableScore(ScoreRef score)
        => score.HasReplay && !score.Mods.Any(m => m is "RD" or "AU" or "CN" or "RX" or "AP" or "4K" or "5K" or "6K" or "7K" or "8K" or "9K");

    static string[] SplitLegacyMods(int bits)
    {
        var mods = (LegacyMods)bits;
        var list = new List<string>();
        if (mods.HasFlag(LegacyMods.Random)) list.Add("RD");
        if (mods.HasFlag(LegacyMods.Autoplay)) list.Add("AU");
        if (mods.HasFlag(LegacyMods.DoubleTime)) list.Add("DT");
        if (mods.HasFlag(LegacyMods.Nightcore)) list.Add("NC");
        if (mods.HasFlag(LegacyMods.HalfTime)) list.Add("HT");
        if (mods.HasFlag(LegacyMods.Mirror)) list.Add("MR");
        if (mods.HasFlag(LegacyMods.Hidden)) list.Add("HD");
        if (mods.HasFlag(LegacyMods.HardRock)) list.Add("HR");
        if (mods.HasFlag(LegacyMods.Easy)) list.Add("EZ");
        return list.ToArray();
    }

    static bool LooksLikeOsu(byte[] bytes)
    {
        string head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 24));
        return head.Contains("osu file format", StringComparison.Ordinal);
    }

    static string Safe(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "unknown" : name;
    }

    static string ShortHash(string hash)
        => string.IsNullOrEmpty(hash) ? "noreplay" : hash[..Math.Min(8, hash.Length)];

    static string Trim(string s) => s.Length <= 180 ? s : s[..180];
}

sealed class ScoreRef
{
    public long Id;
    public long? LegacyId;
    public int BeatmapId;
    public string Player = "";
    public double Accuracy;
    public bool HasReplay;
    public string[] Mods = [];
    public int? LeaderboardRank;
    public double Stars;
    public string Source = "leaderboard";
    public int? UserGlobalRank;
}

sealed class ScoreMeta
{
    public int? LeaderboardRank { get; set; }
    public double Accuracy { get; set; }
    public double Stars { get; set; }
    public string Source { get; set; } = "";
    public int? UserGlobalRank { get; set; }
    public int BeatmapId { get; set; }
    public string Player { get; set; } = "";
}

sealed class MapRef
{
    public int Id;
    public double Stars;
    public int Keys;
}
