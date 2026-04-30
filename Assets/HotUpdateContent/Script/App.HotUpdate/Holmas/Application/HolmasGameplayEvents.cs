namespace App.HotUpdate.Holmas.Application
{
    public sealed class HolmasGameplayStateChangedEvent
    {
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        public int CurrentEnergy { get; set; }

        public int EnergyRecoveryLimit { get; set; }

        public string EnergyLabel { get; set; } = string.Empty;

        public string TaskRewardTip { get; set; } = string.Empty;

        public int TaskRewardTipVersion { get; set; }

        public int TaskTotalCount { get; set; }

        public int TaskClaimableCount { get; set; }

        public int TaskUnlockedSlotCount { get; set; }

        public string LevelMapId { get; set; } = string.Empty;

        public int LevelSeed { get; set; }

        public bool LevelCompleted { get; set; }
    }

    public sealed class HolmasEnergyChangedEvent
    {
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        public int CurrentEnergy { get; set; }

        public int EnergyRecoveryLimit { get; set; }

        public string EnergyLabel { get; set; } = string.Empty;
    }

    public sealed class HolmasTaskRewardTipChangedEvent
    {
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        public string Tip { get; set; } = string.Empty;

        public int Version { get; set; }
    }

    public sealed class HolmasTaskBarChangedEvent
    {
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        public int TotalTaskCount { get; set; }

        public int ClaimableTaskCount { get; set; }

        public int UnlockedSlotCount { get; set; }
    }

    public sealed class HolmasLevelStateChangedEvent
    {
        public HolmasGameplayRuntimeStateChangeReason Reason { get; set; }

        public string MapId { get; set; } = string.Empty;

        public int Seed { get; set; }

        public bool Completed { get; set; }
    }
}
