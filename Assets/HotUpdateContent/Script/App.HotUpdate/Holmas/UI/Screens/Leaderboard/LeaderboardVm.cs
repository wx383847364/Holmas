using App.Shared.Holmas.Leaderboards;

namespace App.HotUpdate.Holmas.UI.Screens.Leaderboard
{
    public sealed class LeaderboardVm
    {
        public HolmasLeaderboardType SelectedType = HolmasLeaderboardType.Level;
        public string Title = "等级总榜";
        public string PeriodText = string.Empty;
        public string Status = string.Empty;
        public HolmasLeaderboardEntry[] Entries = System.Array.Empty<HolmasLeaderboardEntry>();
        public HolmasLeaderboardEntry SelfEntry;
    }
}
