using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.HotUpdate.Holmas.Tutorial
{
    [CreateAssetMenu(fileName = "CoreFindCatTutorialVisualConfig", menuName = "Holmas/Tutorial/Visual Config", order = 10)]
    public sealed class TutorialVisualConfig : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/HotUpdateContent/Res/Tutorial/CoreFindCatTutorialVisualConfig.asset";
        public const string PlaceholderSpritePath = "Assets/HotUpdateContent/Res/Tutorial/Placeholder/tutorial_placeholder.png";

        [SerializeField]
        private List<TutorialStepVisualDefinition> steps = new List<TutorialStepVisualDefinition>();

        public IReadOnlyList<TutorialStepVisualDefinition> Steps => steps;

        public TutorialStepVisualDefinition Find(string stepId)
        {
            if (string.IsNullOrWhiteSpace(stepId) || steps == null)
            {
                return null;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStepVisualDefinition item = steps[i];
                if (item != null && string.Equals(item.StepId, stepId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        public void ReplaceSteps(IEnumerable<TutorialStepVisualDefinition> nextSteps)
        {
            steps = nextSteps != null
                ? new List<TutorialStepVisualDefinition>(nextSteps)
                : new List<TutorialStepVisualDefinition>();
        }
    }

    [Serializable]
    public sealed class TutorialStepVisualDefinition
    {
        public string StepId = string.Empty;
        public string MainImagePath = string.Empty;
        public string DialogBackgroundPath = string.Empty;
        public string TipsIconPath = string.Empty;
        public string FingerIconPath = string.Empty;
        public string HighlightSpritePath = string.Empty;
        public string ArrowSpritePath = string.Empty;
    }
}
