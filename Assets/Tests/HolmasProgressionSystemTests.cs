using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;

namespace Holmas.Tests
{
    public sealed class HolmasProgressionSystemTests
    {
        [Test]
        public void HolmasMetaProgressionService_ClaimTaskReward_AddsGoldWithoutExperience()
        {
            var catalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(catalog);
            var metaService = new HolmasMetaProgressionService(
                catalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 123456 });

            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 1;
            state.Experience = 5;

            var task = new TaskInstanceData
            {
                TaskInstanceId = "task-1",
                SourceTaskTypeId = "task-template-1",
                TaskKind = "Money",
                CatId = "cat-a",
                TargetCount = 3,
                CurrentCount = 3,
                Reward = 37,
                SlotIndex = 0,
            };

            long gold = metaService.ApplyTaskClaim(state, task);

            Assert.That(gold, Is.EqualTo(37));
            Assert.That(state.GoldBalance, Is.EqualTo(37));
            Assert.That(state.Experience, Is.EqualTo(5));
            Assert.That(state.PlayerLevel, Is.EqualTo(1));
            Assert.That(state.ClaimedTaskCount, Is.EqualTo(1));
        }

        [Test]
        public void HolmasMetaProgressionService_ApplyMapCompletion_DoesNotGrantExperience()
        {
            var catalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(catalog);
            var metaService = new HolmasMetaProgressionService(
                catalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 123456 });

            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 2;
            state.Experience = 5;

            long gained = metaService.ApplyMapCompletion(
                state,
                new[]
                {
                    new SpawnedCatData { CatId = "cat-a", CellIndex = 0 },
                    new SpawnedCatData { CatId = "cat-b", CellIndex = 1 },
                });

            Assert.That(gained, Is.EqualTo(0));
            Assert.That(state.Experience, Is.EqualTo(5));
            Assert.That(state.CompletedMapCount, Is.EqualTo(1));
            Assert.That(state.CatDiscoveryCounts["cat-a"], Is.EqualTo(1));
            Assert.That(state.CatDiscoveryCounts["cat-b"], Is.EqualTo(1));
        }

        [Test]
        public void HolmasMetaProgressionService_ApplyOfflineSettlement_AddsGoldUsingConfiguredRewardRate()
        {
            var catalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(catalog);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 789_000 };
            var metaService = new HolmasMetaProgressionService(
                catalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                clock);

            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 2;
            state.Experience = 5;

            long reward = metaService.ApplyOfflineSettlement(state, 3_600_000L);

            Assert.That(reward, Is.EqualTo(8));
            Assert.That(state.GoldBalance, Is.EqualTo(8));
            Assert.That(state.OfflineRewardTotal, Is.EqualTo(8));
            Assert.That(state.LastOfflineSettlementAtUtcMilliseconds, Is.EqualTo(clock.UtcNowMilliseconds));
            Assert.That(state.Experience, Is.EqualTo(5));
        }

        [Test]
        public void HolmasMetaProgressionService_GetUnlockExpireAt_UsesConfiguredHours()
        {
            var catalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(catalog);
            var metaService = new HolmasMetaProgressionService(
                catalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 1_000 });

            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 2;

            long expireAt = metaService.GetUnlockExpireAt(state, 1_000);

            Assert.That(expireAt, Is.EqualTo(1_000 + 12L * 60L * 60L * 1000L));
        }

        [Test]
        public void HolmasAgencyProgressionService_TryUpgradePromotion_ConsumesGoldGrantsExperienceAndAdvancesStage()
        {
            var metaCatalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(metaCatalog);
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 123456 });
            var agencyService = new HolmasAgencyProgressionService(CreatePromotionCatalog(), metaService);
            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 1;
            state.AgencyStageId = 1;
            state.GoldBalance = 30;

            HolmasAgencyUpgradeResult first = agencyService.TryUpgradePromotion(state, "lobby");

            Assert.That(first.Success, Is.True, first.FailureReason);
            Assert.That(first.GoldSpent, Is.EqualTo(10));
            Assert.That(first.PreviousLevel, Is.EqualTo(0));
            Assert.That(first.NewLevel, Is.EqualTo(1));
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(state, 1, "lobby"), Is.EqualTo(1));
            Assert.That(state.GoldBalance, Is.EqualTo(20));
            Assert.That(state.Experience, Is.EqualTo(1));
            Assert.That(state.PlayerLevel, Is.EqualTo(2));
            Assert.That(state.AgencyStageId, Is.EqualTo(1));

            HolmasAgencyUpgradeResult second = agencyService.TryUpgradePromotion(state, "desk");

            Assert.That(second.Success, Is.True, second.FailureReason);
            Assert.That(second.GoldSpent, Is.EqualTo(20));
            Assert.That(second.StageAdvanced, Is.True);
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(state, 1, "desk"), Is.EqualTo(1));
            Assert.That(state.GoldBalance, Is.EqualTo(0));
            Assert.That(state.Experience, Is.EqualTo(2));
            Assert.That(state.PlayerLevel, Is.EqualTo(3));
            Assert.That(state.AgencyStageId, Is.EqualTo(2));
            Assert.That(state.PromotionLevels.Count, Is.EqualTo(2));
        }

        [Test]
        public void HolmasAgencyProgressionService_RejectsPromotionUpgradeWhenGoldInsufficient()
        {
            var metaCatalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(metaCatalog);
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 123456 });
            var agencyService = new HolmasAgencyProgressionService(CreatePromotionCatalog(), metaService);
            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 1;
            state.AgencyStageId = 1;
            state.GoldBalance = 5;

            HolmasAgencyUpgradeResult result = agencyService.TryUpgradePromotion(state, "lobby");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Does.Contain("金币不足"));
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(state, 1, "lobby"), Is.EqualTo(0));
            Assert.That(state.GoldBalance, Is.EqualTo(5));
            Assert.That(state.Experience, Is.EqualTo(0));
        }

        [Test]
        public void HolmasAgencyProgressionService_RejectsPromotionUpgradeWhenLevelCapReached()
        {
            var metaCatalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(metaCatalog);
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 123456 });
            var agencyService = new HolmasAgencyProgressionService(CreatePromotionCatalog(), metaService);
            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 1;
            state.AgencyStageId = 1;
            state.GoldBalance = 20;

            HolmasAgencyUpgradeResult first = agencyService.TryUpgradePromotion(state, "lobby");
            Assert.That(first.Success, Is.True, first.FailureReason);

            state.GoldBalance = 20;
            HolmasAgencyUpgradeResult second = agencyService.TryUpgradePromotion(state, "lobby");

            Assert.That(second.Success, Is.False);
            Assert.That(second.FailureReason, Does.Contain("等级上限"));
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(state, 1, "lobby"), Is.EqualTo(1));
            Assert.That(state.AgencyStageId, Is.EqualTo(1));
        }

        [Test]
        public void HolmasAgencyProgressionService_DoesNotAdvancePastLastConfiguredStage()
        {
            var metaCatalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(metaCatalog);
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 123456 });
            var agencyService = new HolmasAgencyProgressionService(CreateSingleStagePromotionCatalog(), metaService);
            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 1;
            state.AgencyStageId = 1;
            state.GoldBalance = 10;

            HolmasAgencyUpgradeResult result = agencyService.TryUpgradePromotion(state, "lobby");

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.StageAdvanced, Is.False);
            Assert.That(state.AgencyStageId, Is.EqualTo(1));
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(state, 1, "lobby"), Is.EqualTo(1));
        }

        [Test]
        public void HolmasAgencyProgressionService_TryUpgradePromotion_PrefersCurrentStageWhenPromotionIdsRepeatAcrossStages()
        {
            var metaCatalog = CreateMetaCatalog();
            var sharedPolicy = new HolmasDefaultMetaExperienceSource(metaCatalog);
            var metaService = new HolmasMetaProgressionService(
                metaCatalog,
                CreatePlayerLevelCatalog(),
                sharedPolicy,
                sharedPolicy,
                new FixedUtcClock { UtcNowMilliseconds = 123456 });
            var agencyService = new HolmasAgencyProgressionService(CreateDuplicatePromotionIdCatalog(), metaService);
            HolmasMetaProgressionState state = metaService.CreateState();
            state.PlayerLevel = 1;
            state.AgencyStageId = 1;
            state.GoldBalance = 50;

            HolmasAgencyUpgradeResult result = agencyService.TryUpgradePromotion(state, "leaflet");

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.GoldSpent, Is.EqualTo(10));
            Assert.That(result.AgencyStageId, Is.EqualTo(1));
            Assert.That(HolmasAgencyPromotionStateKey.GetLevel(state, 1, "leaflet"), Is.EqualTo(1));
            Assert.That(state.GoldBalance, Is.EqualTo(40));
            Assert.That(state.AgencyStageId, Is.EqualTo(1));
        }

        private static HolmasMetaCatalog CreateMetaCatalog()
        {
            return new HolmasMetaCatalog(new[]
            {
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 1,
                    MinExperience = 0,
                    OfflineRewardPerHour = 6,
                    AdUnlockHours = 24,
                },
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 2,
                    MinExperience = 1,
                    OfflineRewardPerHour = 8,
                    AdUnlockHours = 12,
                },
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 3,
                    MinExperience = 2,
                    OfflineRewardPerHour = 10,
                    AdUnlockHours = 24,
                },
            });
        }

        private static HolmasTaskCatalog CreatePlayerLevelCatalog()
        {
            return new HolmasTaskCatalog(
                null,
                null,
                new[]
                {
                    new HolmasPlayerLevelDefinition { PlayerLevel = 1, UpgradeExp = 999 },
                    new HolmasPlayerLevelDefinition { PlayerLevel = 2, UpgradeExp = 1999 },
                    new HolmasPlayerLevelDefinition { PlayerLevel = 3, UpgradeExp = 2999 },
                });
        }

        private static HolmasAgencyCatalog CreatePromotionCatalog()
        {
            return new HolmasAgencyCatalog(new[]
            {
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-1",
                    StageImage = "Textures/buildings/building01.png",
                    PromotionId = "lobby",
                    PromotionLevelCap = 1,
                    PromotionUpgradeCosts = new[] { 10 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-1",
                    StageImage = "Textures/buildings/building01.png",
                    PromotionId = "desk",
                    PromotionLevelCap = 1,
                    PromotionUpgradeCosts = new[] { 20 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 2,
                    StageName = "stage-2",
                    StageImage = "Textures/buildings/building02.png",
                    PromotionId = "archive",
                    PromotionLevelCap = 2,
                    PromotionUpgradeCosts = new[] { 30, 40 },
                },
            });
        }

        private static HolmasAgencyCatalog CreateSingleStagePromotionCatalog()
        {
            return new HolmasAgencyCatalog(new[]
            {
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-1",
                    StageImage = "Textures/buildings/building01.png",
                    PromotionId = "lobby",
                    PromotionLevelCap = 1,
                    PromotionUpgradeCosts = new[] { 10 },
                },
            });
        }

        private static HolmasAgencyCatalog CreateDuplicatePromotionIdCatalog()
        {
            return new HolmasAgencyCatalog(new[]
            {
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 1,
                    StageName = "stage-1",
                    StageImage = "Textures/buildings/building01.png",
                    PromotionId = "leaflet",
                    PromotionLevelCap = 2,
                    PromotionUpgradeCosts = new[] { 10, 20 },
                },
                new HolmasAgencyBuildingDefinition
                {
                    AgencyStageId = 2,
                    StageName = "stage-2",
                    StageImage = "Textures/buildings/building02.png",
                    PromotionId = "leaflet",
                    PromotionLevelCap = 3,
                    PromotionUpgradeCosts = new[] { 30, 40, 50 },
                },
            });
        }
    }
}
