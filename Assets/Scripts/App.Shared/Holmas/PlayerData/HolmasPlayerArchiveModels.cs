using System;
using System.Threading.Tasks;
using App.Shared.Holmas.RuntimeData;

namespace App.Shared.Holmas.PlayerData
{
    /// <summary>
    /// 本地模拟服务器返回的角色档案根。
    /// 第一阶段把它视为本地 authoritative archive，后续可平滑替换为真服下发结构。
    /// </summary>
    [Serializable]
    public sealed class HolmasPlayerArchiveRoot
    {
        public string PlayerId = string.Empty;
        public string SchemaVersion = string.Empty;
        public long Revision;
        public long SavedAtUtcMilliseconds;
        public HolmasProgressionArchiveData Progression = new HolmasProgressionArchiveData();
        public HolmasTaskBarArchiveData TaskBar = new HolmasTaskBarArchiveData();
        public LevelSnapshot CurrentLevel;
        public HolmasTutorialSuspendedSessionArchiveData TutorialSuspendedSession;
    }

    [Serializable]
    public sealed class HolmasTutorialSuspendedSessionArchiveData
    {
        public string SchemaVersion = string.Empty;
        public string Reason = string.Empty;
        public string Source = string.Empty;
        public long CreatedAtUtcMilliseconds;
        public HolmasTaskBarArchiveData TaskBar = new HolmasTaskBarArchiveData();
        public LevelSnapshot CurrentLevel;
    }

    /// <summary>
    /// 长期成长的可持久化字段集合。
    /// </summary>
    [Serializable]
    public sealed class HolmasProgressionArchiveData
    {
        public long Experience;
        public int PlayerLevel = 1;
        public int AgencyStageId = 1;
        public long GoldBalance;
        public int CompletedMapCount;
        public int ClaimedTaskCount;
        public long OfflineRewardTotal;
        public long LastOfflineSettlementAtUtcMilliseconds;
        public bool EnergyInitialized = true;
        public int EnergyCurrent = 50;
        public int EnergyRecoveryLimit = 50;
        public long EnergyLastRecoveryAtUtcMilliseconds;
        public HolmasArchiveCounterEntry[] CatDiscoveryCounts = Array.Empty<HolmasArchiveCounterEntry>();
        public HolmasPromotionLevelEntry[] PromotionLevels = Array.Empty<HolmasPromotionLevelEntry>();
    }

    /// <summary>
    /// 任务栏档案。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskBarArchiveData
    {
        public int TotalSlots = 5;
        public int DefaultOpenSlots = 2;
        public TaskSlotState[] Slots = Array.Empty<TaskSlotState>();
        public HolmasTaskRuntimeArchiveData[] Tasks = Array.Empty<HolmasTaskRuntimeArchiveData>();
    }

    /// <summary>
    /// 单条运行时任务的档案结构。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskRuntimeArchiveData
    {
        public TaskInstanceData Task = new TaskInstanceData();
        public bool IsRewardClaimed;
    }

    /// <summary>
    /// 通用计数字典条目。
    /// </summary>
    [Serializable]
    public sealed class HolmasArchiveCounterEntry
    {
        public string Key = string.Empty;
        public int Value;
    }

    /// <summary>
    /// 宣传等级条目。
    /// </summary>
    [Serializable]
    public sealed class HolmasPromotionLevelEntry
    {
        public string PromotionId = string.Empty;
        public int Level;
    }

    /// <summary>
    /// 供宿主生命周期在关停前冲刷玩家档案。
    /// </summary>
    public interface IHolmasPlayerArchiveDrain
    {
        Task<bool> FlushAsync();
    }
}
