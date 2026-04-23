namespace App.HotUpdate.Holmas.UI.Screens.Tutorial
{
    public sealed class TutorialStepDefinition
    {
        public int StepIndex;
        public string StepId;
        public string TargetKey;
        public string VisualKey;
        public string Title;
        public string Body;
        public string NextButtonText = "下一步";
        public bool AllowPassThroughInput = true;
        public bool CanSkip = true;
        public string CollapsedHintText = "引导";
        public bool RequiresTutorialBoard;
    }
}
