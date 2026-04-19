namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainTaskItemVm
    {
        public int SlotIndex = -1;
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
        public string Summary = string.Empty;
        public string Status = string.Empty;
        public string StartButtonLabel = "开始找猫";
        public bool StartButtonEnabled;
        public string PromotionButtonLabel = "宣传升级";
        public bool PromotionButtonEnabled;
        public string PromotionId = string.Empty;
        public MainTaskItemVm[] TaskItems = System.Array.Empty<MainTaskItemVm>();
    }
}
