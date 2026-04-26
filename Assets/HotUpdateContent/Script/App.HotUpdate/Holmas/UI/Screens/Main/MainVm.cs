using System.Collections.Generic;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainTaskItemVm
    {
        public int SlotIndex = -1;
        public string CatId = string.Empty;
        public string CatName = string.Empty;
        public string CatIconPath = string.Empty;
        public string Title = string.Empty;
        public string Progress = string.Empty;
        public string Reward = string.Empty;
        public float ProgressNormalized;
        public bool IsLocked;
        public bool IsClaimable;
        public bool IsEmpty;
        public bool ButtonEnabled;
    }

    public sealed class MainVm
    {
        public string LevelLabel = string.Empty;
        public string GoldLabel = string.Empty;
        public string EnergyLabel = string.Empty;
        public string Summary = string.Empty;
        public string Status = string.Empty;
        public string PromotionButtonLabel = "宣传升级";
        public bool PromotionButtonEnabled;
        public string PromotionId = string.Empty;
        public string AddEnergyButtonLabel = "+5体力";
        public bool AddEnergyButtonEnabled;
        public bool BoardVisible;
        public bool UseTutorialBoardLayer;
        public bool WalkToggleIsOn = true;
        public bool FindToggleIsOn;
        public int Rows;
        public int Cols;
        public IReadOnlyList<BoardCellState> Cells = new BoardCellState[0];
        public IReadOnlyDictionary<string, HolmasCatVisualVm> CatVisuals = HolmasCatVisualVm.EmptyLookup;
        public MainTaskItemVm[] TaskItems = System.Array.Empty<MainTaskItemVm>();
    }
}
