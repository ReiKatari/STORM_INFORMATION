using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using StormGameFinder.Models;

namespace StormGameFinder.Services;

public class GameSearchService
{
    private static readonly HttpClient Http;

    static GameSearchService()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true, CookieContainer = new CookieContainer(),
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<GameInfo?> SearchAsync(string query, CancellationToken ct = default)
    {
        var info = new GameInfo { Name = query };

        // 1) Steam — основной источник данных
        var steamId = await SearchSteamAsync(query, ct);
        if (steamId != null)
        {
            var sp = new PlatformInfo
            {
                Platform = "Steam", GameId = steamId,
                StoreUrl = $"https://store.steampowered.com/app/{steamId}"
            };
            await FillSteamDetailsAsync(info, steamId, ct);
            await FillSteamVersionAsync(sp, steamId, ct);
            info.Platforms.Add(sp);

            // Cover: vertical library art (DVD box ratio 2:3)
            info.CoverImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{steamId}/library_600x900_2x.jpg";
            if (sp.Version != "—") info.Version = sp.Version;
            if (sp.LastUpdated != "—") info.LastUpdated = sp.LastUpdated;
        }

        // 2) GOG — прямой API
        await FillGOGAsync(info, query, ct);

        // 3) Генерация ссылок на платформы (поиск в магазинах)
        GeneratePlatformLinks(info, query);

        // 4) Генерация ссылок для скачивания (поисковые запросы)
        GenerateDownloadLinks(info, query);

        return info;
    }

    // ═══ STEAM ═══════════════════════════════════════════════════
    private async Task<string?> SearchSteamAsync(string q, CancellationToken ct)
    {
        try
        {
            var json = await Http.GetStringAsync(
                $"https://steamcommunity.com/actions/SearchApps/{Uri.EscapeDataString(q)}", ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetArrayLength() > 0)
                return doc.RootElement[0].GetProperty("appid").GetString();
        }
        catch { }
        return null;
    }

    private async Task FillSteamDetailsAsync(GameInfo info, string appId, CancellationToken ct)
    {
        try
        {
            var json = await Http.GetStringAsync(
                $"https://store.steampowered.com/api/appdetails?appids={appId}&l=russian&cc=RU", ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.GetProperty(appId);
            if (!root.GetProperty("success").GetBoolean()) return;
            var d = root.GetProperty("data");
            info.Name = d.GetProperty("name").GetString() ?? info.Name;
            if (d.TryGetProperty("short_description", out var desc))
                info.Description = desc.GetString() ?? "";
        }
        catch { }
    }

    private async Task FillSteamVersionAsync(PlatformInfo p, string appId, CancellationToken ct)
    {
        try
        {
            var json = await Http.GetStringAsync(
                $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={appId}&count=50&maxlength=5000", ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var item in doc.RootElement.GetProperty("appnews").GetProperty("newsitems").EnumerateArray())
            {
                var title = item.GetProperty("title").GetString() ?? "";
                var body = item.GetProperty("contents").GetString() ?? "";
                var ts = item.GetProperty("date").GetInt64();

                if (p.LastUpdated == "—")
                    p.LastUpdated = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime.ToString("dd.MM.yyyy");

                if (p.Version == "—")
                {
                    var v = ExtractVersion(title) ?? ExtractVersion(body);
                    if (v != null) p.Version = v;
                }
                if (p.Version != "—") break;
            }
        }
        catch { }
    }

    // ═══ GOG ═════════════════════════════════════════════════════
    private async Task FillGOGAsync(GameInfo info, string q, CancellationToken ct)
    {
        try
        {
            var json = await Http.GetStringAsync(
                $"https://embed.gog.com/games/ajax/filtered?mediaType=game&search={Uri.EscapeDataString(q)}&limit=1", ct);
            using var doc = JsonDocument.Parse(json);
            var prods = doc.RootElement.GetProperty("products");
            if (prods.GetArrayLength() > 0)
            {
                var pr = prods[0];
                var title = pr.GetProperty("title").GetString() ?? "";
                if (Similar(title, q))
                {
                    var id = pr.GetProperty("id").GetInt32().ToString();
                    var slug = pr.GetProperty("slug").GetString() ?? "";
                    info.Platforms.Add(new PlatformInfo
                    {
                        Platform = "GOG", GameId = id,
                        StoreUrl = $"https://www.gog.com/en/game/{slug}"
                    });
                }
            }
        }
        catch { }
    }

    // ═══ PLATFORM LINKS (construct search URLs) ══════════════════
    private static void GeneratePlatformLinks(GameInfo info, string query)
    {
        var eq = Uri.EscapeDataString(query);

        // Epic Games Store
        if (info.Platforms.All(p => p.Platform != "Epic Games"))
            info.Platforms.Add(new PlatformInfo
            {
                Platform = "Epic Games", GameId = "→ Найти",
                StoreUrl = $"https://store.epicgames.com/en-US/browse?q={eq}&sortBy=relevancy"
            });

        // Xbox / Microsoft Store
        if (info.Platforms.All(p => p.Platform != "Xbox"))
            info.Platforms.Add(new PlatformInfo
            {
                Platform = "Xbox", GameId = "→ Найти",
                StoreUrl = $"https://www.xbox.com/en-US/Search/Results?q={eq}"
            });

        // PlayStation Store
        if (info.Platforms.All(p => p.Platform != "PlayStation"))
            info.Platforms.Add(new PlatformInfo
            {
                Platform = "PlayStation", GameId = "→ Найти",
                StoreUrl = $"https://store.playstation.com/search/{eq}"
            });

        // Nintendo eShop
        if (info.Platforms.All(p => p.Platform != "Nintendo"))
            info.Platforms.Add(new PlatformInfo
            {
                Platform = "Nintendo", GameId = "→ Найти",
                StoreUrl = $"https://www.nintendo.com/us/search/#q={eq}&cat=games"
            });

        // EA App
        if (info.Platforms.All(p => p.Platform != "EA App"))
            info.Platforms.Add(new PlatformInfo
            {
                Platform = "EA App", GameId = "→ Найти",
                StoreUrl = $"https://www.ea.com/en/games/library?searchTerm={eq}"
            });

        // Ubisoft Store
        if (info.Platforms.All(p => p.Platform != "Ubisoft"))
            info.Platforms.Add(new PlatformInfo
            {
                Platform = "Ubisoft", GameId = "→ Найти",
                StoreUrl = $"https://store.ubisoft.com/us/search?q={eq}"
            });
    }

    // ═══ DOWNLOAD LINKS (search engine queries) ═════════════════
    private static void GenerateDownloadLinks(GameInfo info, string query)
    {
        var eq = Uri.EscapeDataString(query);
        var eqRu = Uri.EscapeDataString(query + " скачать торрент");
        var eqEn = Uri.EscapeDataString(query + " torrent download repack");

        info.DownloadLinks =
        [
            new() { SiteName = "Google — торрент скачать",
                Domain = "google.com",
                Url = $"https://www.google.com/search?q={eqRu}" },
            new() { SiteName = "Yandex — скачать торрент",
                Domain = "yandex.ru",
                Url = $"https://yandex.ru/search/?text={eqRu}" },
            new() { SiteName = "Bing — torrent repack",
                Domain = "bing.com",
                Url = $"https://www.bing.com/search?q={eqEn}" },
            new() { SiteName = "RuTracker — поиск",
                Domain = "rutracker.org",
                Url = $"https://rutracker.org/forum/tracker.php?nm={eq}" },
            new() { SiteName = "1337x — torrents",
                Domain = "1337x.to",
                Url = $"https://1337x.to/search/{eq}/1/" },
            new() { SiteName = "FitGirl Repacks",
                Domain = "fitgirl-repacks.site",
                Url = $"https://fitgirl-repacks.site/?s={eq}" },
            new() { SiteName = "DODI Repacks",
                Domain = "dodi-repacks.site",
                Url = $"https://dodi-repacks.site/?s={eq}" },
            new() { SiteName = "SteamRIP — free download",
                Domain = "steamrip.com",
                Url = $"https://steamrip.com/?s={eq}" },
            new() { SiteName = "GOG Games — DRM free",
                Domain = "gog-games.to",
                Url = $"https://gog-games.to/search/{eq}" },
            new() { SiteName = "Online Fix — multiplayer",
                Domain = "online-fix.me",
                Url = $"https://online-fix.me/?s={eq}" },
        ];
    }

    // ═══ HELPERS ═════════════════════════════════════════════════
    private static string? ExtractVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string[] ps = [
            @"[Vv]\.?\s*(\d+\.\d+(?:\.\d+)*[a-z]?)",
            @"(?:[Vv]ersion|[Вв]ерсия)\s*[:\s]*(\d+[\.\d]*[a-z]?)",
            @"(?:[Uu]pdate|[Оо]бновление)\s+(\d+[\.\d]*)",
            @"(?:[Pp]atch|[Пп]атч)\s+(\d+[\.\d]*)",
            @"(?:[Bb]uild|[Бб]илд)\s+(\d{4,})",
            @"(?:^|\s)(\d+\.\d+\.\d+)(?:\s|$|[,;.\)\]>])" ];
        foreach (var p in ps) { var m = Regex.Match(text, p); if (m.Success) return m.Groups[1].Value; }
        return null;
    }

    private static bool Similar(string a, string b) =>
        a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
}
