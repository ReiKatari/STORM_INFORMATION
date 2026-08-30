namespace StormGameFinder.Models;

public class GameInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public string Version { get; set; } = "—";
    public string LastUpdated { get; set; } = "—";
    public List<PlatformInfo> Platforms { get; set; } = [];
    public List<DownloadLink> DownloadLinks { get; set; } = [];
}

public class PlatformInfo
{
    public string Platform { get; set; } = "";
    public string GameId { get; set; } = "—";
    public string Version { get; set; } = "—";
    public string LastUpdated { get; set; } = "—";
    public string StoreUrl { get; set; } = "";
}

public class DownloadLink
{
    public string SiteName { get; set; } = "";
    public string Url { get; set; } = "";
    public string Domain { get; set; } = "";
}
