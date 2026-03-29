using System.Linq;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
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
    }
}
