using System;
using App.HotUpdate.Holmas.Leaderboards;
using App.HotUpdate.Holmas.PlayerData;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Holmas.Leaderboards;
using App.Shared.Holmas.PlayerData;
using NUnit.Framework;

namespace Holmas.Tests
{
    public sealed class HolmasLeaderboardRuntimeTests
    {
        [Test]
        public void PeriodUtility_UsesFixedBeijingUtc8Rules()
        {
            long utc = new DateTimeOffset(2026, 5, 3, 16, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

            Assert.That(HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.DailyTaskIncome, utc), Is.EqualTo("20260504"));
            Assert.That(HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.WeeklyCatsFound, utc), Is.EqualTo("2026W18"));
            Assert.That(
                HolmasLeaderboardPeriodUtility.GetNextResetAtUtcMilliseconds(HolmasLeaderboardType.DailyTaskIncome, utc),
                Is.EqualTo(new DateTimeOffset(2026, 5, 4, 16, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()));
        }

        [Test]
        public void MockGateway_RanksSubmittedSelfAndPreservesLongScore()
        {
            var clock = new FixedClock(new DateTimeOffset(2026, 5, 5, 4, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
            var gateway = new HolmasLocalMockLeaderboardGateway(CreateDefinitions(), clock);

            gateway.SubmitSnapshotAsync(new HolmasLeaderboardSnapshot
            {
                PlayerId = "self",
                DisplayName = "本地玩家",
                WechatAvatarUrl = "https://avatar.example/self.png",
                AvatarIconPath = "Assets/HotUpdateContent/Res/Icons/cat_02.png",
                PlayerLevel = 99,
                Experience = 9_999_999L,
                LevelRankUpdatedAtUtcMilliseconds = clock.UtcNowMilliseconds,
                CurrentWeeklyCatsFound = 999,
                CurrentWeeklyPeriodKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.WeeklyCatsFound, clock.UtcNowMilliseconds),
                CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds = clock.UtcNowMilliseconds,
                CurrentDailyTaskIncome = 9_000_000_000L,
                CurrentDailyPeriodKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.DailyTaskIncome, clock.UtcNowMilliseconds),
                CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = clock.UtcNowMilliseconds,
            }).GetAwaiter().GetResult();

            HolmasLeaderboardResponse response = gateway.GetLeaderboardAsync(HolmasLeaderboardType.DailyTaskIncome, "self", 20)
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Success, Is.True);
            Assert.That(response.SelfEntry, Is.Not.Null);
            Assert.That(response.SelfEntry.Rank, Is.EqualTo(1));
            Assert.That(response.SelfEntry.Score, Is.EqualTo(9_000_000_000L));
            Assert.That(response.SelfEntry.WechatAvatarUrl, Is.EqualTo("https://avatar.example/self.png"));
            Assert.That(response.SelfEntry.AvatarIconPath, Is.EqualTo("Assets/HotUpdateContent/Res/Icons/cat_02.png"));
            Assert.That(response.Entries[0].IsSelf, Is.True);
            Assert.That(response.Entries[0].WechatAvatarUrl, Is.EqualTo("https://avatar.example/self.png"));
            Assert.That(response.Entries[0].AvatarIconPath, Is.EqualTo("Assets/HotUpdateContent/Res/Icons/cat_02.png"));
        }

        [Test]
        public void MockGateway_ExcludesSelfEntryFromExpiredDailyPeriod()
        {
            var clock = new MutableClock(new DateTimeOffset(2026, 5, 5, 4, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
            var gateway = new HolmasLocalMockLeaderboardGateway(CreateDefinitions(), clock);
            gateway.SubmitSnapshotAsync(new HolmasLeaderboardSnapshot
            {
                PlayerId = "self",
                DisplayName = "本地玩家",
                CurrentDailyTaskIncome = 9_000_000L,
                CurrentDailyPeriodKey = HolmasLeaderboardPeriodUtility.GetPeriodKey(HolmasLeaderboardType.DailyTaskIncome, clock.UtcNowMilliseconds),
                CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = clock.UtcNowMilliseconds,
            }).GetAwaiter().GetResult();

            clock.UtcNowMilliseconds = new DateTimeOffset(2026, 5, 6, 4, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            HolmasLeaderboardResponse response = gateway.GetLeaderboardAsync(HolmasLeaderboardType.DailyTaskIncome, "self", 20)
                .GetAwaiter()
                .GetResult();

            Assert.That(response.PeriodKey, Is.EqualTo("20260506"));
            Assert.That(response.SelfEntry, Is.Null, "过期日榜快照不应继续生成我的名次。");
            Assert.That(response.Entries, Is.Not.Empty, "新周期应能重建 mock 种子。");
            Assert.That(response.Entries[0].IsSelf, Is.False);
        }

        [Test]
        public void ArchiveMapper_PreservesLeaderboardProgressionFieldsWhenResettingTaskBar()
        {
            var mapper = new HolmasPlayerArchiveMapper();
            var source = new HolmasPlayerArchiveRoot
            {
                PlayerId = "self",
                Progression = new HolmasProgressionArchiveData
                {
                    DisplayName = "本地玩家",
                    CurrentDailyTaskIncome = 12345L,
                    CurrentDailyPeriodKey = "20260505",
                    CurrentDailyTaskIncomeUpdatedAtUtcMilliseconds = 10L,
                    CurrentWeeklyCatsFound = 7,
                    CurrentWeeklyPeriodKey = "2026W18",
                    CurrentWeeklyCatsFoundUpdatedAtUtcMilliseconds = 11L,
                    CurrentLevelRankUpdatedAtUtcMilliseconds = 12L,
                    WechatAvatarUrl = "https://avatar.example/self.png",
                    AvatarIconPath = "Assets/HotUpdateContent/Res/Icons/cat_03.png",
                },
            };

            HolmasPlayerArchiveRoot reset = mapper.CreateArchiveWithResetTaskBar(source);

            Assert.That(reset.Progression.DisplayName, Is.EqualTo("本地玩家"));
            Assert.That(reset.Progression.CurrentDailyTaskIncome, Is.EqualTo(12345L));
            Assert.That(reset.Progression.CurrentWeeklyCatsFound, Is.EqualTo(7));
            Assert.That(reset.Progression.CurrentLevelRankUpdatedAtUtcMilliseconds, Is.EqualTo(12L));
            Assert.That(reset.Progression.WechatAvatarUrl, Is.EqualTo("https://avatar.example/self.png"));
            Assert.That(reset.Progression.AvatarIconPath, Is.EqualTo("Assets/HotUpdateContent/Res/Icons/cat_03.png"));
        }

        private static HolmasLeaderboardDefinition[] CreateDefinitions()
        {
            return new[]
            {
                new HolmasLeaderboardDefinition { LeaderboardType = "Level", DisplayName = "等级总榜", TopEntryCount = 20, MockEntryCount = 100, IsEnabled = true },
                new HolmasLeaderboardDefinition { LeaderboardType = "WeeklyCatsFound", DisplayName = "寻猫周榜", TopEntryCount = 20, MockEntryCount = 100, IsEnabled = true },
                new HolmasLeaderboardDefinition { LeaderboardType = "DailyTaskIncome", DisplayName = "收入日榜", TopEntryCount = 20, MockEntryCount = 100, IsEnabled = true },
            };
        }

        private sealed class FixedClock : IHolmasUtcClock
        {
            public FixedClock(long utcNowMilliseconds)
            {
                UtcNowMilliseconds = utcNowMilliseconds;
            }

            public long UtcNowMilliseconds { get; }
        }

        private sealed class MutableClock : IHolmasUtcClock
        {
            public MutableClock(long utcNowMilliseconds)
            {
                UtcNowMilliseconds = utcNowMilliseconds;
            }

            public long UtcNowMilliseconds { get; set; }
        }
    }
}
