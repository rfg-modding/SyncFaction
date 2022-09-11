namespace SyncFaction.Core.Services.FactionFiles;

public class CategoryPage
{
    public long TotalPages { get; set; }
    public long CurrentPage { get; set; }
    public long ResultsThisPage { get; set; }
    public long ResultsTotal { get; set; }
    public Dictionary<string, Mod> Results { get; set; }
}
