using System.Linq;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using NUnit.Framework;

namespace Holmas.Tests
{
    public sealed class HolmasTaskProgressionTests
    {
        [Test]
        public void HolmasTaskProgressService_RefillUnlockedSlots_AvoidsDuplicateCats()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1, 0, 1, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = service.CreateDefaultTaskBarState();
            var refill = service.RefillUnlockedEmptySlots(taskBarState, 1);

            Assert.That(refill.GeneratedTasks, Has.Count.EqualTo(2));
            Assert.That(taskBarState.Tasks, Has.Count.EqualTo(2));
            Assert.That(taskBarState.Tasks.Select(item => item.Task.CatId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(taskBarState.GetSlot(0).TaskInstanceId, Is.Not.Empty);
            Assert.That(taskBarState.GetSlot(1).TaskInstanceId, Is.Not.Empty);
        }

        [Test]
        public void HolmasTaskProgressService_ExpiredAdSlot_IsClearedAndLocked()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = service.CreateDefaultTaskBarState();
            var unlock = service.UnlockAdSlot(taskBarState, 2, 1, 2000);

            Assert.That(unlock.Success, Is.True);
            Assert.That(taskBarState.GetSlot(2).IsUnlocked, Is.True);
            Assert.That(taskBarState.GetTaskBySlot(2), Is.Not.Null);

            service.RefreshExpiredAdSlots(taskBarState, 3000);

            Assert.That(taskBarState.GetSlot(2).IsUnlocked, Is.False);
            Assert.That(taskBarState.GetSlot(2).UnlockExpireAt, Is.EqualTo(0L));
            Assert.That(taskBarState.GetSlot(2).TaskInstanceId, Is.Empty);
            Assert.That(taskBarState.GetTaskBySlot(2), Is.Null);
        }

        [Test]
        public void HolmasTaskProgressService_ClaimReward_RefillsTheSlot()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1, 0, 1, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = new HolmasTaskBarState(1, 1);
            var refill = service.RefillUnlockedEmptySlots(taskBarState, 1);

            Assert.That(refill.GeneratedTasks, Has.Count.EqualTo(1));

            var taskBeforeClaim = taskBarState.GetTaskBySlot(0);
            Assert.That(taskBeforeClaim, Is.Not.Null);
            Assert.That(taskBeforeClaim.Task.CatId, Is.EqualTo("cat-a"));
            taskBeforeClaim.ApplyProgress(1);

            var claim = service.ClaimTaskReward(taskBarState, 0, 1);

            Assert.That(claim.Success, Is.True);
            Assert.That(claim.Reward, Is.EqualTo(20));
            Assert.That(claim.RefilledTask, Is.Not.Null);
            Assert.That(taskBarState.GetTaskBySlot(0), Is.Not.Null);
            Assert.That(taskBarState.GetTaskBySlot(0).Task.CatId, Is.EqualTo("cat-b"));
        }

        [Test]
        public void HolmasTaskProgressService_UnlockAdSlot_InvalidIndex_Fails()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = service.CreateDefaultTaskBarState();
            var unlock = service.UnlockAdSlot(taskBarState, 99, 1, 2000);

            Assert.That(unlock.Success, Is.False);
            Assert.That(unlock.FailureReason, Is.EqualTo("槽位索引无效。"));
            Assert.That(taskBarState.Tasks, Is.Empty);
        }

        [Test]
        public void HolmasTaskProgressService_UnlockAdSlot_WithExistingTask_DoesNotReplaceTask()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = service.CreateDefaultTaskBarState();
            var refill = service.RefillUnlockedEmptySlots(taskBarState, 1);
            var originalTask = taskBarState.GetTaskBySlot(0);

            Assert.That(refill.GeneratedTasks, Has.Count.EqualTo(2));
            Assert.That(originalTask, Is.Not.Null);

            var unlock = service.UnlockAdSlot(taskBarState, 0, 1, 5000);

            Assert.That(unlock.Success, Is.True);
            Assert.That(unlock.GeneratedTask, Is.Null);
            Assert.That(taskBarState.GetTaskBySlot(0), Is.SameAs(originalTask));
            Assert.That(taskBarState.Tasks, Has.Count.EqualTo(2));
            Assert.That(taskBarState.GetSlot(0).UnlockExpireAt, Is.EqualTo(5000));
        }

        [Test]
        public void HolmasTaskProgressService_RefillUnlockedSlots_MissingPlayerLevel_ReturnsNoTasks()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = service.CreateDefaultTaskBarState();
            var refill = service.RefillUnlockedEmptySlots(taskBarState, 999);

            Assert.That(refill.GeneratedTasks, Is.Empty);
            Assert.That(taskBarState.Tasks, Is.Empty);
            Assert.That(taskBarState.GetSlot(0).TaskInstanceId, Is.Empty);
            Assert.That(taskBarState.GetSlot(1).TaskInstanceId, Is.Empty);
        }

        [Test]
        public void HolmasTaskProgressService_ClaimReward_BeforeCompletion_Fails()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = new HolmasTaskBarState(1, 1);
            var refill = service.RefillUnlockedEmptySlots(taskBarState, 1);
            var taskBeforeClaim = taskBarState.GetTaskBySlot(0);

            Assert.That(refill.GeneratedTasks, Has.Count.EqualTo(1));
            Assert.That(taskBeforeClaim, Is.Not.Null);

            var claim = service.ClaimTaskReward(taskBarState, 0, 1);

            Assert.That(claim.Success, Is.False);
            Assert.That(claim.FailureReason, Is.EqualTo("任务尚未完成，不能领奖。"));
            Assert.That(claim.Reward, Is.EqualTo(0));
            Assert.That(taskBarState.GetTaskBySlot(0), Is.SameAs(taskBeforeClaim));
        }

        [Test]
        public void HolmasTaskProgressService_RefreshExpiredAdSlots_DoesNotLockDefaultOpenSlots()
        {
            var catalog = HolmasTestSupport.CreateStandardTaskCatalog();
            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 1, 0, 1, 1),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = service.CreateDefaultTaskBarState();
            var refill = service.RefillUnlockedEmptySlots(taskBarState, 1);

            Assert.That(refill.GeneratedTasks, Has.Count.EqualTo(2));

            service.RefreshExpiredAdSlots(taskBarState, 999999);

            Assert.That(taskBarState.GetSlot(0).IsUnlocked, Is.True);
            Assert.That(taskBarState.GetSlot(1).IsUnlocked, Is.True);
            Assert.That(taskBarState.GetSlot(0).UnlockExpireAt, Is.EqualTo(0L));
            Assert.That(taskBarState.GetSlot(1).UnlockExpireAt, Is.EqualTo(0L));
            Assert.That(taskBarState.GetTaskBySlot(0), Is.Not.Null);
            Assert.That(taskBarState.GetTaskBySlot(1), Is.Not.Null);
        }

        [Test]
        public void HolmasTaskProgressService_LeavesSlotEmpty_WhenUniqueCatsAreExhausted()
        {
            var catalog = new HolmasTaskCatalog(
                new[]
                {
                    new HolmasCatDefinition { CatId = "cat-a", Price = 10 },
                },
                new[]
                {
                    new HolmasTaskTemplateDefinition
                    {
                        TaskTypeId = "task-single-cat",
                        CatIdList = new[] { "cat-a" },
                        CountMin = 1,
                        CountMax = 1,
                        RewardArray = System.Array.Empty<string>(),
                        LevelRewardFactor = 2f,
                    }
                },
                new[]
                {
                    new HolmasPlayerLevelDefinition
                    {
                        PlayerLevel = 1,
                        UpgradeExp = 0,
                        TaskTypeIds = new[] { "task-single-cat" },
                        TaskTypeWeights = new[] { 1 },
                        MapIds = new[] { "map-1" },
                        MapWeights = new[] { 1 },
                    }
                });

            var service = new HolmasTaskProgressService(
                catalog,
                new ScriptedRandomSource(0, 0, 0, 0),
                new FixedUtcClock { UtcNowMilliseconds = 1000 });

            var taskBarState = service.CreateDefaultTaskBarState();
            var refill = service.RefillUnlockedEmptySlots(taskBarState, 1);

            Assert.That(refill.GeneratedTasks, Has.Count.EqualTo(2));
            Assert.That(refill.GeneratedTasks.Count(item => item.Success), Is.EqualTo(1));
            Assert.That(refill.GeneratedTasks.Count(item => !item.Success), Is.EqualTo(1));
            Assert.That(taskBarState.Tasks, Has.Count.EqualTo(1));
            Assert.That(taskBarState.GetTaskBySlot(0), Is.Not.Null);
            Assert.That(taskBarState.GetTaskBySlot(1), Is.Null);
            Assert.That(taskBarState.GetSlot(1).TaskInstanceId, Is.Empty);
        }
    }
}
