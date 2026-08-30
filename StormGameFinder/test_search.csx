using System.Net.Http;

var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

// Test Bing
var bingResp = await http.GetAsync("https://www.bing.com/search?q=cyberpunk+2077+torrent+download&setlang=ru");
var bingHtml = await bingResp.Content.ReadAsStringAsync();
Console.WriteLine($"Bing: {bingResp.StatusCode}, Length: {bingHtml.Length}");
Console.WriteLine($"Bing b_algo count: {System.Text.RegularExpressions.Regex.Matches(bingHtml, "b_algo").Count}");

// Extract first few links
var doc = new HtmlAgilityPack.HtmlDocument();
doc.LoadHtml(bingHtml);
var links = doc.DocumentNode.SelectNodes("//li[contains(@class,'b_algo')]//h2/a[@href]");
Console.WriteLine($"Bing links found: {links?.Count ?? 0}");
if (links != null) foreach (var l in links.Take(3))
    Console.WriteLine($"  -> {l.GetAttributeValue("href", "")}");

// Test DDG
var ddgResp = await http.GetAsync("https://html.duckduckgo.com/html/?q=cyberpunk+2077+torrent+repack");
var ddgHtml = await ddgResp.Content.ReadAsStringAsync();
Console.WriteLine($"\nDDG: {ddgResp.StatusCode}, Length: {ddgHtml.Length}");
Console.WriteLine($"DDG result__a count: {System.Text.RegularExpressions.Regex.Matches(ddgHtml, "result__a").Count}");

// Test Steam News
var newsJson = await http.GetStringAsync("https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=1091500&count=5&maxlength=500");
var newsDoc = System.Text.Json.JsonDocument.Parse(newsJson);
foreach (var item in newsDoc.RootElement.GetProperty("appnews").GetProperty("newsitems").EnumerateArray().Take(3))
{
    Console.WriteLine($"\nSteam News: {item.GetProperty("title").GetString()}");
}
