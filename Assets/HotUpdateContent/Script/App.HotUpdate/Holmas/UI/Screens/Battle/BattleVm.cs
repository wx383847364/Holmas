using System;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleVm
    {
        public string LevelLabel = string.Empty;
        public string GoldLabel = string.Empty;
        public string EnergyLabel = string.Empty;
        public string Summary = string.Empty;
        public string Status = string.Empty;
        public string BuildButtonLabel = string.Empty;
        public bool BuildButtonEnabled;
        public int SelectedStageId;
        public BattleBuildStageVm[] BuildStages = Array.Empty<BattleBuildStageVm>();
        public BattleStageVm[] Stages = Array.Empty<BattleStageVm>();
        public BattleStageBarVm[] StageBars = Array.Empty<BattleStageBarVm>();
    }

    public sealed class BattleStageVm
    {
        public int SlotIndex;
        public int AgencyStageId;
        public string StageName = string.Empty;
        public string StageImage = string.Empty;
        public string ProgressLabel = string.Empty;
        public int ProgressCurrent;
        public int ProgressCap;
        public int StarCount;
        public bool Visible;
        public bool Unlocked;
        public bool Selected;
        public bool Current;
        public bool Completed;
    }

    public sealed class BattleBuildStageVm
    {
        public int SlotIndex;
        public int AgencyStageId;
        public string StageName = string.Empty;
        public string StageImage = string.Empty;
        public string ProgressLabel = string.Empty;
        public string ActionLabel = string.Empty;
        public int StarCount;
        public bool Visible;
        public bool Unlocked;
        public bool Selected;
        public bool Current;
        public bool Completed;
        public bool CanBuild;
    }

    public sealed class BattleStageBarVm
    {
        public int SlotIndex;
        public bool Visible;
        public float Progress;
    }
}
