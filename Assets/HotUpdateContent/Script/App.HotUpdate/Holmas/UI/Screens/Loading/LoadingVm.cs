namespace App.HotUpdate.Holmas.UI.Screens.Loading
{
    public sealed class LoadingVm
    {
        public string Status = "Loading...";
        public float Progress = 0.5f;
        public float TargetProgress = 0.95f;
        public float AnimationDurationSeconds = 2f;
        public bool Animate = true;
    }
}
