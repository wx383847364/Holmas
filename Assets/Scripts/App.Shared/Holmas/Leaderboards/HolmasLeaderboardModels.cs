using System;
using System.Threading.Tasks;

namespace App.Shared.Holmas.Leaderboards
{
    public static class HolmasLeaderboardAvatarDefaults
    {
        public const string DefaultAvatarIconPath = "Assets/HotUpdateContent/Res/Icons/cat_01.png";
    }

    public enum HolmasLeaderboardType
    {
        Unknown = 0,
        Level = 1,
        WeeklyCatsFound = 2,
        DailyTaskIncome = 3,
    }

    [Serializable]
    public sealed class HolmasLeaderboardEntry
    {
        public string PlayerId = string.Empty;
        public string DisplayName = string.Empty;
        public string WechatAvatarUrl = string.Empty;
        public string AvatarIconPath = string.Empty;
        public int Rank;
        public long Score;
        public long SecondaryScore;
        public long UpdatedAtUtcMilliseconds;
        public bool IsSelf;
    }

    [Serializable]
    public sealed class HolmasLeaderboardResponse
    {
        public bool Success = true;
        public string FailureReason = string.Empty;
        public HolmasLeaderboardType Type = HolmasLeaderboardType.Unknown;
        public string DisplayName = string.Empty;
        public string PeriodKey = string.Empty;
        public long NextResetAtUtcMilliseconds;
        public HolmasLeaderboardEntry[] Entries = Array.Empty<HolmasLeaderboardEntry>();
        public HolmasLeaderboardEntry SelfEntry;
    }

    [Serializable]
    public sealed class HolmasLeaderboardSnapshot
    {
        public string PlayerId = string.Empty;
        public string DisplayName = string.Empty;
        public string WechatAvatarUrl = string.Empty;
        public string AvatarIconPath = string.Empty;
        public long SubmittedAtUtcMilliseconds;
        public int PlayerLevel;
        public long Experience;
        public long LevelRankUpdatedAtUtcMilliseconds;
        public int CurrentWeeklyCatsFound;
        public string CurrentWeeklyPeriodKey = string.Empty;
        public long CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds;
        public long CurrentDailyTaskIncome;
        public string CurrentDailyPeriodKey = string.Empty;
        public long CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds;
    }

    [Serializable]
    public sealed class HolmasLeaderboardSubmitResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
    }

    public interface IHolmasLeaderboardGateway
    {
        Task<HolmasLeaderboardSubmitResult> SubmitSnapshotAsync(HolmasLeaderboardSnapshot snapshot);
        Task<HolmasLeaderboardResponse> GetLeaderboardAsync(HolmasLeaderboardType type, string playerId, int topN);
    }
}
