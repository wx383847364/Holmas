using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Screens.Tutorial
{
    public sealed class TutorialOverlayVm
    {
        public string StepId = string.Empty;
        public string TargetKey = string.Empty;
        public string Title = string.Empty;
        public string Body = string.Empty;
        public string NextButtonText = "下一步";
        public string SkipButtonText = "跳过";
        public string CollapseButtonText = "收起";
        public string CollapsedHintText = "引导";
        public string MainImagePath = string.Empty;
        public string TipsIconPath = string.Empty;
        public string FingerIconPath = string.Empty;
        public bool IsCollapsed;
        public bool CanSkip = true;
        public bool AllowPassThroughInput = true;
        public RectTransform TargetRect;
    }
}
