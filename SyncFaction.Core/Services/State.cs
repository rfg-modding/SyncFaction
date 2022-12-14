using System.Text;

namespace SyncFaction.Core.Services;

public class State
{
    public long CommunityPatch { get; set; }
    public List<long> CommunityUpdates { get; set; } = new();
    public bool DevMode { get; set; }
    public bool? IsGog { get; set; }
    public bool? IsVerified { get; set; }
    public bool MockMode => false; // used only for testing

    public string GetHumanReadableCommunityVersion()
    {
        var sb = new StringBuilder();
        sb.Append("base: ");
        sb.Append(CommunityPatch == 0 ? "not installed" : CommunityPatch);
        sb.Append(", updates: ");
        var updates = string.Join(", ", CommunityUpdates);
        sb.Append(updates == string.Empty ? "none" : updates);
        return sb.ToString();
    }



}
