using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;

namespace App.HotUpdate.Holmas.Tutorial
{
    public static class CoreFindCatTutorialSteps
    {
        public const string FindFirstCatStepId = "find_first_cat";
        public const string TaskBarStepId = "task_bar";
        public const string ModeStepId = "mode";
        public const string ContinueFindStepId = "continue_find";
        public const string EnergyStepId = "energy";
        public const string PromotionStepId = "promotion";
        public const string HelpStepId = "help";

        private static readonly List<TutorialStepDefinition> Steps = CreateSteps();

        public static IReadOnlyList<TutorialStepDefinition> All => Steps;

        public static int LastIndex => Steps.Count - 1;

        public static int ClampIndex(int stepIndex)
        {
            if (Steps.Count == 0)
            {
                return -1;
            }

            return Math.Min(Math.Max(0, stepIndex), Steps.Count - 1);
        }

        public static int IndexOf(string stepId)
        {
            if (string.IsNullOrWhiteSpace(stepId))
            {
                return -1;
            }

            for (int i = 0; i < Steps.Count; i++)
            {
                if (string.Equals(Steps[i].StepId, stepId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public static TutorialStepDefinition Get(int index)
        {
            return index >= 0 && index < Steps.Count ? Steps[index] : null;
        }

        private static List<TutorialStepDefinition> CreateSteps()
        {
            return new List<TutorialStepDefinition>
            {
                new TutorialStepDefinition
                {
                    StepIndex = 0,
                    StepId = FindFirstCatStepId,
                    TargetKey = "BoardCell:27",
                    VisualKey = FindFirstCatStepId,
                    Title = "找到第一只猫",
                    Body = "这格里藏着第一只猫。点一下格子，看看线索怎么展开。",
                    CollapsedHintText = "找第一只猫",
                    RequiresTutorialBoard = true,
                },
                new TutorialStepDefinition
                {
                    StepIndex = 1,
                    StepId = TaskBarStepId,
                    TargetKey = "TaskBar",
                    VisualKey = TaskBarStepId,
                    Title = "任务目标",
                    Body = "这里显示正在寻找的猫、进度和金币奖励。找到对应猫后会自动推进，完成后自动领奖。",
                    CollapsedHintText = "任务目标",
                },
                new TutorialStepDefinition
                {
                    StepIndex = 2,
                    StepId = ModeStepId,
                    TargetKey = "WalkToggle",
                    VisualKey = ModeStepId,
                    Title = "选择模式",
                    Body = "行走模式适合试探：翻到普通格不耗体力，遇到猫耗 2 点。寻找模式更直接：每翻一个有效格耗 1 点。",
                    CollapsedHintText = "模式说明",
                },
                new TutorialStepDefinition
                {
                    StepIndex = 3,
                    StepId = ContinueFindStepId,
                    TargetKey = "BoardCell:44",
                    VisualKey = ContinueFindStepId,
                    Title = "继续找猫",
                    Body = "继续翻格寻找隐藏猫。数字会提示附近线索；找到本局全部猫后，会自动进入下一局。",
                    CollapsedHintText = "继续找猫",
                    RequiresTutorialBoard = true,
                },
                new TutorialStepDefinition
                {
                    StepIndex = 4,
                    StepId = EnergyStepId,
                    TargetKey = "EnergyArea",
                    VisualKey = EnergyStepId,
                    Title = "体力提示",
                    Body = "翻格需要体力。体力会随时间恢复；体力不足时不能继续翻格。",
                    CollapsedHintText = "体力",
                },
                new TutorialStepDefinition
                {
                    StepIndex = 5,
                    StepId = PromotionStepId,
                    TargetKey = "PromotionButton",
                    VisualKey = PromotionStepId,
                    Title = "金币用途",
                    Body = "任务奖励会变成金币。金币可以用于宣传升级，让侦探社继续成长。",
                    CollapsedHintText = "金币用途",
                },
                new TutorialStepDefinition
                {
                    StepIndex = 6,
                    StepId = HelpStepId,
                    TargetKey = "HelpButton",
                    VisualKey = HelpStepId,
                    Title = "帮助入口",
                    Body = "之后想重看找猫说明，可以点这里打开帮助。",
                    NextButtonText = "完成",
                    CollapsedHintText = "帮助",
                },
            };
        }
    }
}
