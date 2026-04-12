namespace App.HotUpdate.Holmas.UI.Screens.AgencyMain
{
    public sealed class AgencyMainTaskItemVm
    {
        public int SlotIndex = -1;
        public string Title = string.Empty;
        public string Progress = string.Empty;
        public string Reward = string.Empty;
        public string ClaimButtonLabel = string.Empty;
        public bool ClaimButtonEnabled;
        public bool IsLocked;
    }

    public sealed class AgencyMainVm
    {
        public string Title = string.Empty;
        public string Summary = string.Empty;
        public string TaskSummary = string.Empty;
        public string BoardSummary = string.Empty;
        public string Status = string.Empty;
        public string PrimaryActionLabel = string.Empty;
        public bool PrimaryActionEnabled = true;
        public bool IsPlaceholderView;
        public AgencyMainTaskItemVm[] TaskItems = System.Array.Empty<AgencyMainTaskItemVm>();
    }
}
