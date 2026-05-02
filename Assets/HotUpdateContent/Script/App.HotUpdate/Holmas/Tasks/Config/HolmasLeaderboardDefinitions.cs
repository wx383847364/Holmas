using System;

namespace App.HotUpdate.Holmas.Tasks.Config
{
    public enum HolmasLeaderboardPeriodType : byte
    {
        AllTime = 0,
        Weekly = 1,
        Daily = 2,
    }

    [Serializable]
    public sealed class HolmasLeaderboardDefinition
    {
        public int LeaderboardIndex;
        public string LeaderboardType = string.Empty;
        public string DisplayName = string.Empty;
        public HolmasLeaderboardPeriodType PeriodType = HolmasLeaderboardPeriodType.AllTime;
        public string TimeZoneId = "Asia/Shanghai";
        public int ResetDayOfWeek;
        public int ResetHour;
        public int ResetMinute;
        public int TopEntryCount = 20;
        public int MockEntryCount = 100;
        public bool IsEnabled = true;
        public string Notes = string.Empty;
    }
}
