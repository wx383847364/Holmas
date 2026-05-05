using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Contracts;
using App.Shared.Holmas.Leaderboards;

namespace App.HotUpdate.Holmas.Leaderboards
{
    public static class HolmasLeaderboardPeriodUtility
    {
        private const long MillisecondsPerDay = 24L * 60L * 60L * 1000L;
        private static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

        public static string GetPeriodKey(HolmasLeaderboardType type, long utcMilliseconds)
        {
            DateTime local = ToBeijingLocal(utcMilliseconds);
            switch (type)
            {
                case HolmasLeaderboardType.DailyTaskIncome:
                    return local.ToString("yyyyMMdd");
                case HolmasLeaderboardType.WeeklyCatsFound:
                    return local.ToString("yyyy") + "W" + GetMondayWeekOfYear(local).ToString("00");
                default:
                    return "alltime";
            }
        }

        public static long GetNextResetAtUtcMilliseconds(HolmasLeaderboardType type, long utcMilliseconds)
        {
            DateTime local = ToBeijingLocal(utcMilliseconds);
            if (type == HolmasLeaderboardType.DailyTaskIncome)
            {
                return ToUtcMilliseconds(local.Date.AddDays(1));
            }

            if (type == HolmasLeaderboardType.WeeklyCatsFound)
            {
                int daysSinceMonday = ((int)local.DayOfWeek + 6) % 7;
                return ToUtcMilliseconds(local.Date.AddDays(7 - daysSinceMonday));
            }

            return 0L;
        }

        private static DateTime ToBeijingLocal(long utcMilliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(0L, utcMilliseconds))
                .ToOffset(BeijingOffset)
                .DateTime;
        }

        private static long ToUtcMilliseconds(DateTime beijingLocal)
        {
            return new DateTimeOffset(beijingLocal, BeijingOffset).ToUnixTimeMilliseconds();
        }

        private static int GetMondayWeekOfYear(DateTime local)
        {
            DateTime firstDay = new DateTime(local.Year, 1, 1);
            int firstMondayOffset = (8 - (int)firstDay.DayOfWeek) % 7;
            DateTime firstMonday = firstDay.AddDays(firstMondayOffset);
            if (local.Date < firstMonday)
            {
                return 1;
            }

            return (int)((local.Date - firstMonday).TotalDays / 7) + 1;
        }
    }

    public sealed class HolmasLocalMockLeaderboardGateway : IHolmasLeaderboardGateway
    {
        private readonly Dictionary<HolmasLeaderboardType, List<HolmasLeaderboardSnapshot>> _snapshots =
            new Dictionary<HolmasLeaderboardType, List<HolmasLeaderboardSnapshot>>();
        private readonly Dictionary<HolmasLeaderboardType, HolmasLeaderboardDefinition> _definitions =
            new Dictionary<HolmasLeaderboardType, HolmasLeaderboardDefinition>();
        private readonly IHolmasUtcClock _clock;

        public HolmasLocalMockLeaderboardGateway(IEnumerable<HolmasLeaderboardDefinition> definitions, IHolmasUtcClock clock)
        {
            _clock = clock ?? new HolmasSystemUtcClock();
            foreach (HolmasLeaderboardDefinition definition in definitions ?? Array.Empty<HolmasLeaderboardDefinition>())
            {
                HolmasLeaderboardType type = ParseType(definition?.LeaderboardType);
                if (type != HolmasLeaderboardType.Unknown && definition.IsEnabled)
                {
                    _definitions[type] = definition;
                }
            }
        }

        public Task<HolmasLeaderboardSubmitResult> SubmitSnapshotAsync(HolmasLeaderboardSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.PlayerId))
            {
                return Task.FromResult(new HolmasLeaderboardSubmitResult { FailureReason = "排行榜快照为空。" });
            }

            foreach (HolmasLeaderboardType type in new[] { HolmasLeaderboardType.Level, HolmasLeaderboardType.WeeklyCatsFound, HolmasLeaderboardType.DailyTaskIncome })
            {
                List<HolmasLeaderboardSnapshot> list = GetSeededSnapshots(type);
                list.RemoveAll(item => string.Equals(item.PlayerId, snapshot.PlayerId, StringComparison.Ordinal));
                list.Add(CloneSnapshot(snapshot));
            }

            return Task.FromResult(new HolmasLeaderboardSubmitResult { Success = true });
        }

        public Task<HolmasLeaderboardResponse> GetLeaderboardAsync(HolmasLeaderboardType type, string playerId, int topN)
        {
            if (type == HolmasLeaderboardType.Unknown)
            {
                return Task.FromResult(new HolmasLeaderboardResponse
                {
                    Success = false,
                    FailureReason = "排行榜类型非法。",
                    Type = type,
                });
            }

            HolmasLeaderboardDefinition definition = GetDefinition(type);
            int limit = topN > 0 ? topN : Math.Max(1, definition.TopEntryCount);
            long now = _clock.UtcNowMilliseconds;
            string periodKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(type, now);
            IReadOnlyList<HolmasLeaderboardSnapshot> candidates = GetCurrentPeriodSnapshots(type, periodKey);
            List<HolmasLeaderboardEntry> ranked = Rank(type, candidates, playerId).ToList();
            HolmasLeaderboardEntry self = ranked.FirstOrDefault(item => string.Equals(item.PlayerId, playerId, StringComparison.Ordinal));
            return Task.FromResult(new HolmasLeaderboardResponse
            {
                Success = true,
                Type = type,
                DisplayName = definition.DisplayName,
                PeriodKey = periodKey,
                NextResetAtUtcMilliseconds = HolmasLeaderboardPeriodUtility.GetNextResetAtUtcMilliseconds(type, now),
                Entries = ranked.Take(limit).Select(CloneEntry).ToArray(),
                SelfEntry = self != null ? CloneEntry(self) : null,
            });
        }

        private IReadOnlyList<HolmasLeaderboardSnapshot> GetCurrentPeriodSnapshots(HolmasLeaderboardType type, string periodKey)
        {
            List<HolmasLeaderboardSnapshot> list = GetSeededSnapshots(type);
            if (type == HolmasLeaderboardType.DailyTaskIncome)
            {
                List<HolmasLeaderboardSnapshot> current = list
                    .Where(item => string.Equals(item.CurrentDailyPeriodKey, periodKey, StringComparison.Ordinal))
                    .ToList();
                if (current.Count > 0)
                {
                    return current;
                }

                _snapshots.Remove(type);
                return GetSeededSnapshots(type)
                    .Where(item => string.Equals(item.CurrentDailyPeriodKey, periodKey, StringComparison.Ordinal))
                    .ToList();
            }

            if (type == HolmasLeaderboardType.WeeklyCatsFound)
            {
                List<HolmasLeaderboardSnapshot> current = list
                    .Where(item => string.Equals(item.CurrentWeeklyPeriodKey, periodKey, StringComparison.Ordinal))
                    .ToList();
                if (current.Count > 0)
                {
                    return current;
                }

                _snapshots.Remove(type);
                return GetSeededSnapshots(type)
                    .Where(item => string.Equals(item.CurrentWeeklyPeriodKey, periodKey, StringComparison.Ordinal))
                    .ToList();
            }

            return list;
        }

        private List<HolmasLeaderboardSnapshot> GetSeededSnapshots(HolmasLeaderboardType type)
        {
            if (_snapshots.TryGetValue(type, out List<HolmasLeaderboardSnapshot> list))
            {
                return list;
            }

            HolmasLeaderboardDefinition definition = GetDefinition(type);
            int count = Math.Max(definition.TopEntryCount, definition.MockEntryCount);
            list = new List<HolmasLeaderboardSnapshot>(count);
            long now = _clock.UtcNowMilliseconds;
            string dailyKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.DailyTaskIncome, now);
            string weeklyKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.WeeklyCatsFound, now);
            for (int i = 0; i < count; i++)
            {
                int rankSeed = i + 1;
                list.Add(new HolmasLeaderboardSnapshot
                {
                    PlayerId = "mock-" + type + "-" + rankSeed.ToString("000"),
                    DisplayName = BuildMockName(type, rankSeed),
                    AvatarIconPath = HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath,
                    SubmittedAtUtcMilliseconds = now - rankSeed * 37_000L,
                    PlayerLevel = Math.Max(1, 80 - rankSeed / 2),
                    Experience = Math.Max(0, 200_000L - rankSeed * 731L),
                    LevelRankUpdatedAtUtcMilliseconds = now - rankSeed * 43_000L,
                    CurrentWeeklyCatsFound = Math.Max(0, 260 - rankSeed * 2),
                    CurrentWeeklyPeriodKey = weeklyKey,
                    CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds = now - rankSeed * 53_000L,
                    CurrentDailyTaskIncome = Math.Max(0, 80_000L - rankSeed * 417L),
                    CurrentDailyPeriodKey = dailyKey,
                    CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = now - rankSeed * 61_000L,
                });
            }

            _snapshots[type] = list;
            return list;
        }

        private IEnumerable<HolmasLeaderboardEntry> Rank(HolmasLeaderboardType type, IEnumerable<HolmasLeaderboardSnapshot> snapshots, string playerId)
        {
            IEnumerable<HolmasLeaderboardSnapshot> ordered;
            switch (type)
            {
                case HolmasLeaderboardType.WeeklyCatsFound:
                    ordered = snapshots.OrderByDescending(item => item.CurrentWeeklyCatsFound)
                        .ThenBy(item => item.CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds)
                        .ThenBy(item => item.PlayerId, StringComparer.Ordinal);
                    break;
                case HolmasLeaderboardType.DailyTaskIncome:
                    ordered = snapshots.OrderByDescending(item => item.CurrentDailyTaskIncome)
                        .ThenBy(item => item.CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds)
                        .ThenBy(item => item.PlayerId, StringComparer.Ordinal);
                    break;
                default:
                    ordered = snapshots.OrderByDescending(item => item.PlayerLevel)
                        .ThenByDescending(item => item.Experience)
                        .ThenBy(item => item.LevelRankUpdatedAtUtcMilliseconds)
                        .ThenBy(item => item.PlayerId, StringComparer.Ordinal);
                    break;
            }

            int rank = 0;
            foreach (HolmasLeaderboardSnapshot snapshot in ordered)
            {
                rank++;
                yield return BuildEntry(type, snapshot, rank, playerId);
            }
        }

        private HolmasLeaderboardDefinition GetDefinition(HolmasLeaderboardType type)
        {
            if (_definitions.TryGetValue(type, out HolmasLeaderboardDefinition definition))
            {
                return definition;
            }

            return new HolmasLeaderboardDefinition
            {
                LeaderboardType = type.ToString(),
                DisplayName = type.ToString(),
                TopEntryCount = 20,
                MockEntryCount = 100,
                IsEnabled = true,
            };
        }

        private static HolmasLeaderboardEntry BuildEntry(HolmasLeaderboardType type, HolmasLeaderboardSnapshot snapshot, int rank, string playerId)
        {
            long score = snapshot.PlayerLevel;
            long secondary = snapshot.Experience;
            long updatedAt = snapshot.LevelRankUpdatedAtUtcMilliseconds;
            if (type == HolmasLeaderboardType.WeeklyCatsFound)
            {
                score = snapshot.CurrentWeeklyCatsFound;
                secondary = 0L;
                updatedAt = snapshot.CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds;
            }
            else if (type == HolmasLeaderboardType.DailyTaskIncome)
            {
                score = snapshot.CurrentDailyTaskIncome;
                secondary = 0L;
                updatedAt = snapshot.CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds;
            }

            return new HolmasLeaderboardEntry
            {
                PlayerId = snapshot.PlayerId ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(snapshot.DisplayName) ? snapshot.PlayerId ?? string.Empty : snapshot.DisplayName,
                WechatAvatarUrl = snapshot.WechatAvatarUrl ?? string.Empty,
                AvatarIconPath = string.IsNullOrWhiteSpace(snapshot.AvatarIconPath) ? HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath : snapshot.AvatarIconPath,
                Rank = rank,
                Score = score,
                SecondaryScore = secondary,
                UpdatedAtUtcMilliseconds = updatedAt,
                IsSelf = string.Equals(snapshot.PlayerId, playerId, StringComparison.Ordinal),
            };
        }

        private static string BuildMockName(HolmasLeaderboardType type, int index)
        {
            string prefix = type == HolmasLeaderboardType.Level ? "侦探" :
                type == HolmasLeaderboardType.WeeklyCatsFound ? "寻猫员" : "金牌社员";
            return prefix + index.ToString("00");
        }

        public static HolmasLeaderboardType ParseType(string value)
        {
            return Enum.TryParse(value ?? string.Empty, true, out HolmasLeaderboardType type)
                ? type
                : HolmasLeaderboardType.Unknown;
        }

        private static HolmasLeaderboardSnapshot CloneSnapshot(HolmasLeaderboardSnapshot source)
        {
            return new HolmasLeaderboardSnapshot
            {
                PlayerId = source.PlayerId ?? string.Empty,
                DisplayName = source.DisplayName ?? string.Empty,
                WechatAvatarUrl = source.WechatAvatarUrl ?? string.Empty,
                AvatarIconPath = string.IsNullOrWhiteSpace(source.AvatarIconPath) ? HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath : source.AvatarIconPath,
                SubmittedAtUtcMilliseconds = source.SubmittedAtUtcMilliseconds,
                PlayerLevel = source.PlayerLevel,
                Experience = source.Experience,
                LevelRankUpdatedAtUtcMilliseconds = source.LevelRankUpdatedAtUtcMilliseconds,
                CurrentWeeklyCatsFound = source.CurrentWeeklyCatsFound,
                CurrentWeeklyPeriodKey = source.CurrentWeeklyPeriodKey ?? string.Empty,
                CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds = source.CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds,
                CurrentDailyTaskIncome = source.CurrentDailyTaskIncome,
                CurrentDailyPeriodKey = source.CurrentDailyPeriodKey ?? string.Empty,
                CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = source.CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds,
            };
        }

        private static HolmasLeaderboardEntry CloneEntry(HolmasLeaderboardEntry source)
        {
            return new HolmasLeaderboardEntry
            {
                PlayerId = source.PlayerId ?? string.Empty,
                DisplayName = source.DisplayName ?? string.Empty,
                WechatAvatarUrl = source.WechatAvatarUrl ?? string.Empty,
                AvatarIconPath = string.IsNullOrWhiteSpace(source.AvatarIconPath) ? HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath : source.AvatarIconPath,
                Rank = source.Rank,
                Score = source.Score,
                SecondaryScore = source.SecondaryScore,
                UpdatedAtUtcMilliseconds = source.UpdatedAtUtcMilliseconds,
                IsSelf = source.IsSelf,
            };
        }
    }

    public sealed class HolmasLeaderboardTrackerService : IDisposable
    {
        private readonly HolmasGameplayRuntime _runtime;
        private readonly IHolmasLeaderboardGateway _gateway;
        private readonly IHolmasUtcClock _clock;
        private readonly IAppLogger _logger;
        private readonly string _playerId;
        private readonly Dictionary<HolmasLeaderboardType, HolmasLeaderboardDefinition> _definitions =
            new Dictionary<HolmasLeaderboardType, HolmasLeaderboardDefinition>();
        private IDisposable _taskRewardSubscription;
        private IDisposable _catsFoundSubscription;
        private bool _disposed;

        public HolmasLeaderboardTrackerService(
            HolmasGameplayRuntime runtime,
            IHolmasLeaderboardGateway gateway,
            IHolmasUtcClock clock,
            IAppLogger logger,
            IEventBus eventBus,
            string playerId,
            IEnumerable<HolmasLeaderboardDefinition> definitions)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _clock = clock ?? new HolmasSystemUtcClock();
            _logger = logger;
            _playerId = string.IsNullOrWhiteSpace(playerId) ? "local-player" : playerId;

            foreach (HolmasLeaderboardDefinition definition in definitions ?? Array.Empty<HolmasLeaderboardDefinition>())
            {
                HolmasLeaderboardType type = HolmasLocalMockLeaderboardGateway.ParseType(definition?.LeaderboardType);
                if (type != HolmasLeaderboardType.Unknown && definition.IsEnabled)
                {
                    _definitions[type] = definition;
                }
            }

            _runtime.StateChanged += OnRuntimeStateChanged;
            if (eventBus != null)
            {
                _taskRewardSubscription = eventBus.SubscribeScoped<HolmasLeaderboardTaskRewardClaimedEvent>(OnTaskRewardClaimed);
                _catsFoundSubscription = eventBus.SubscribeScoped<HolmasLeaderboardCatsFoundEvent>(OnCatsFound);
            }
        }

        public void Start()
        {
            EnsurePeriodsRolledOver();
            EnsureDisplayName();
            EnsureLevelTimestamp();
            _ = SubmitCurrentSnapshotAsync();
        }

        public async Task<HolmasLeaderboardResponse> GetLeaderboardAsync(HolmasLeaderboardType type)
        {
            EnsurePeriodsRolledOver();
            await SubmitCurrentSnapshotAsync().ConfigureAwait(false);
            int topN = _definitions.TryGetValue(type, out HolmasLeaderboardDefinition definition)
                ? Math.Max(1, definition.TopEntryCount)
                : 20;
            return await _gateway.GetLeaderboardAsync(type, _playerId, topN).ConfigureAwait(false);
        }

        public void SetPlayerAvatar(string wechatAvatarUrl, string avatarIconPath = null)
        {
            HolmasMetaProgressionState state = _runtime.MetaProgressionState;
            state.WechatAvatarUrl = wechatAvatarUrl ?? string.Empty;
            state.AvatarIconPath = string.IsNullOrWhiteSpace(avatarIconPath)
                ? HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath
                : avatarIconPath;
            _ = SubmitCurrentSnapshotAsync();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _runtime.StateChanged -= OnRuntimeStateChanged;
            _taskRewardSubscription?.Dispose();
            _catsFoundSubscription?.Dispose();
        }

        private void OnTaskRewardClaimed(HolmasLeaderboardTaskRewardClaimedEvent evt)
        {
            if (evt == null || evt.RewardGold <= 0)
            {
                return;
            }

            EnsurePeriodsRolledOver();
            HolmasMetaProgressionState state = _runtime.MetaProgressionState;
            long now = _clock.UtcNowMilliseconds;
            state.CurrentDailyTaskIncome += evt.RewardGold;
            state.CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = now;
            _ = SubmitCurrentSnapshotAsync();
        }

        private void OnCatsFound(HolmasLeaderboardCatsFoundEvent evt)
        {
            if (evt == null || evt.FoundCatCount <= 0)
            {
                return;
            }

            EnsurePeriodsRolledOver();
            HolmasMetaProgressionState state = _runtime.MetaProgressionState;
            long now = _clock.UtcNowMilliseconds;
            state.CurrentWeeklyCatsFound += evt.FoundCatCount;
            state.CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds = now;
            _ = SubmitCurrentSnapshotAsync();
        }

        private void OnRuntimeStateChanged(HolmasGameplayRuntimeStateChangeReason reason)
        {
            if (reason == HolmasGameplayRuntimeStateChangeReason.PromotionUpgraded)
            {
                EnsurePeriodsRolledOver();
                _runtime.MetaProgressionState.CurrentLevelRankUpdatedAtUtcMilliseconds = _clock.UtcNowMilliseconds;
                _ = SubmitCurrentSnapshotAsync();
            }
        }

        private async Task SubmitCurrentSnapshotAsync()
        {
            try
            {
                HolmasLeaderboardSubmitResult result = await _gateway.SubmitSnapshotAsync(BuildSnapshot()).ConfigureAwait(false);
                if (result == null || !result.Success)
                {
                    _logger?.LogWarning("HolmasLeaderboardTrackerService: 提交排行榜快照失败。reason={0}", result?.FailureReason ?? "unknown");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("HolmasLeaderboardTrackerService: 提交排行榜快照异常。{0}", ex.Message);
            }
        }

        private HolmasLeaderboardSnapshot BuildSnapshot()
        {
            EnsureDisplayName();
            EnsureLevelTimestamp();
            HolmasMetaProgressionState state = _runtime.MetaProgressionState;
            return new HolmasLeaderboardSnapshot
            {
                PlayerId = _playerId,
                DisplayName = state.DisplayName ?? string.Empty,
                WechatAvatarUrl = state.WechatAvatarUrl ?? string.Empty,
                AvatarIconPath = string.IsNullOrWhiteSpace(state.AvatarIconPath) ? HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath : state.AvatarIconPath,
                SubmittedAtUtcMilliseconds = _clock.UtcNowMilliseconds,
                PlayerLevel = Math.Max(1, state.PlayerLevel),
                Experience = Math.Max(0L, state.Experience),
                LevelRankUpdatedAtUtcMilliseconds = state.CurrentLevelRankUpdatedAtUtcMilliseconds,
                CurrentWeeklyCatsFound = Math.Max(0, state.CurrentWeeklyCatsFound),
                CurrentWeeklyPeriodKey = state.CurrentWeeklyPeriodKey ?? string.Empty,
                CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds = state.CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds,
                CurrentDailyTaskIncome = Math.Max(0L, state.CurrentDailyTaskIncome),
                CurrentDailyPeriodKey = state.CurrentDailyPeriodKey ?? string.Empty,
                CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = state.CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds,
            };
        }

        private void EnsurePeriodsRolledOver()
        {
            HolmasMetaProgressionState state = _runtime.MetaProgressionState;
            long now = _clock.UtcNowMilliseconds;
            string dailyKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.DailyTaskIncome, now);
            if (!string.Equals(state.CurrentDailyPeriodKey, dailyKey, StringComparison.Ordinal))
            {
                state.CurrentDailyPeriodKey = dailyKey;
                state.CurrentDailyTaskIncome = 0L;
                state.CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = now;
            }

            string weeklyKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.WeeklyCatsFound, now);
            if (!string.Equals(state.CurrentWeeklyPeriodKey, weeklyKey, StringComparison.Ordinal))
            {
                state.CurrentWeeklyPeriodKey = weeklyKey;
                state.CurrentWeeklyCatsFound = 0;
                state.CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds = now;
            }
        }

        private void EnsureDisplayName()
        {
            HolmasMetaProgressionState state = _runtime.MetaProgressionState;
            if (string.IsNullOrWhiteSpace(state.DisplayName))
            {
                state.DisplayName = _playerId;
            }
        }

        private void EnsureLevelTimestamp()
        {
            HolmasMetaProgressionState state = _runtime.MetaProgressionState;
            if (state.CurrentLevelRankUpdatedAtUtcMilliseconds <= 0L)
            {
                state.CurrentLevelRankUpdatedAtUtcMilliseconds = _clock.UtcNowMilliseconds;
            }
        }
    }
}
