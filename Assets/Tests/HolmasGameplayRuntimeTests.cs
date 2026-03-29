using System.Collections.Generic;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;

namespace Holmas.Tests
{
    public sealed class HolmasGameplayRuntimeTests
    {
        [Test]
        public void HolmasGameplayRuntime_ConnectsMapCompletionTaskProgressAndMetaProgress()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

            runtime.RefillAvailableTasks(1);

            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var request = HolmasTestSupport.CreateRequest(
                "map-1",
                "terrain://map-1",
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            BoardRuntime boardRuntime = runtime.StartLevel(terrain, request);
            Assert.That(boardRuntime.TotalCatCount, Is.EqualTo(1));

            var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult progressionResult);

            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(reveal.Completed, Is.True);
            Assert.That(progressionResult, Is.Not.Null);
            Assert.That(progressionResult.ProgressedTaskIds, Has.Count.EqualTo(1));
            Assert.That(progressionResult.CompletedTaskIds, Has.Count.EqualTo(1));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(6));

            var claim = runtime.ClaimTaskReward(0, 1);

            Assert.That(claim.Success, Is.True);
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(8));
            Assert.That(runtime.MetaProgressionState.AgencyLevel, Is.EqualTo(2));
            Assert.That(runtime.MetaProgressionState.ClaimedTaskCount, Is.EqualTo(1));
        }

        [Test]
        public void HolmasGameplayRuntime_RejectsSettlementBeforeLevelCompleted()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

            runtime.RefillAvailableTasks(1);

            var terrain = HolmasTestSupport.CreateTerrain(1, 2);
            var request = HolmasTestSupport.CreateRequest(
                "map-2",
                "terrain://map-2",
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevel(terrain, request);

            Assert.Throws<System.InvalidOperationException>(() => runtime.ApplyCurrentLevelCompletion());
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(0));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(0));
        }

        [Test]
        public void HolmasGameplayRuntime_DoesNotApplyCompletionTwice()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var randomSource = new ScriptedRandomSource(0, 0, 1, 0, 1, 1);
            var clock = new FixedUtcClock { UtcNowMilliseconds = 1000 };
            var taskService = new HolmasTaskProgressService(catalog, randomSource, clock);
            var metaService = new HolmasMetaProgressionService(
                HolmasTestSupport.CreateMetaCatalog(),
                new HolmasDefaultMetaExperienceSource(),
                new HolmasDefaultMetaExperienceSource());
            var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
            var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

            runtime.RefillAvailableTasks(1);

            var terrain = HolmasTestSupport.CreateTerrain(1, 1);
            var request = HolmasTestSupport.CreateRequest(
                "map-repeat",
                "terrain://map-repeat",
                1,
                1,
                1,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            runtime.StartLevel(terrain, request);
            var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult firstResult);

            Assert.That(reveal.Completed, Is.True);
            Assert.That(firstResult, Is.Not.Null);
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(6));

            HolmasProgressionAdvanceResult secondResult = runtime.ApplyCurrentLevelCompletion();

            Assert.That(secondResult.ProgressedTaskIds, Is.Empty);
            Assert.That(secondResult.CompletedTaskIds, Is.Empty);
            Assert.That(secondResult.MetaExperienceGained, Is.EqualTo(0));
            Assert.That(runtime.MetaProgressionState.Experience, Is.EqualTo(6));
            Assert.That(runtime.TaskBarState.GetTaskBySlot(0).Task.CurrentCount, Is.EqualTo(1));
        }
    }
}
