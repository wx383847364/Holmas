using App.HotUpdate.Holmas.UI.Binding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleBindings
    {
        public const string RootPanelKey = "battle/root_panel";
        public const string BackButtonKey = "battle/back_button";
        public const string BuildButtonKey = "battle/build_button";
        public const string BuildButtonTextKey = "battle/build_button_text";
        public const string LevelTextKey = "battle/level_text";
        public const string GoldTextKey = "battle/gold_text";
        public const string EnergyTextKey = "battle/energy_text";
        public const string SummaryTextKey = "battle/summary_text";
        public const string StatusTextKey = "battle/status_text";
        public const string ButtonClickEvent = "on_click";

        public const string RootNodePath = "BattlePanel";
        public const string RuntimeOverlayNodeName = "RuntimeOverlay";
        public const string RuntimeOverlayNodePath = RootNodePath + "/" + RuntimeOverlayNodeName;
        public const string BackButtonNodePath = RootNodePath + "/Back_btn";
        public const string BuildButtonNodePath = RootNodePath + "/Build_btn";
        public const string BuildButtonTextNodePath = BuildButtonNodePath + "/Text (TMP)";
        public const string LevelTextNodePath = RootNodePath + "/Headicon_btn/Level";
        public const string GoldTextNodePath = RootNodePath + "/Money_btn/Text (TMP)";
        public const string EnergyTextNodePath = RuntimeOverlayNodePath + "/EnergyText";
        public const string SummaryTextNodePath = RuntimeOverlayNodePath + "/SummaryText";
        public const string StatusTextNodePath = RuntimeOverlayNodePath + "/StatusText";

        public static readonly string[] StageButtonKeys =
        {
            "battle/stage_1_button",
            "battle/stage_2_button",
            "battle/stage_3_button",
            "battle/stage_4_button",
            "battle/stage_5_button",
        };

        public static readonly string[] StageImageKeys =
        {
            "battle/stage_1_image",
            "battle/stage_2_image",
            "battle/stage_3_image",
            "battle/stage_4_image",
            "battle/stage_5_image",
        };

        public static readonly string[] StageNameTextKeys =
        {
            "battle/stage_1_name_text",
            "battle/stage_2_name_text",
            "battle/stage_3_name_text",
            "battle/stage_4_name_text",
            "battle/stage_5_name_text",
        };

        public static readonly string[] StageLockKeys =
        {
            "battle/stage_1_lock",
            "battle/stage_2_lock",
            "battle/stage_3_lock",
            "battle/stage_4_lock",
            "battle/stage_5_lock",
        };

        public static readonly string[] StageBarKeys =
        {
            "battle/stage_bar_1",
            "battle/stage_bar_2",
            "battle/stage_bar_3",
            "battle/stage_bar_4",
        };

        public static readonly string[] BuildStageButtonKeys =
        {
            "battle/build_stage_1_button",
            "battle/build_stage_2_button",
            "battle/build_stage_3_button",
            "battle/build_stage_4_button",
            "battle/build_stage_5_button",
        };

        public static readonly string[] BuildStageImageKeys =
        {
            "battle/build_stage_1_image",
            "battle/build_stage_2_image",
            "battle/build_stage_3_image",
            "battle/build_stage_4_image",
            "battle/build_stage_5_image",
        };

        public static readonly string[] BuildStageNameTextKeys =
        {
            "battle/build_stage_1_name_text",
            "battle/build_stage_2_name_text",
            "battle/build_stage_3_name_text",
            "battle/build_stage_4_name_text",
            "battle/build_stage_5_name_text",
        };

        public static readonly string[] BuildStageLockKeys =
        {
            "battle/build_stage_1_lock",
            "battle/build_stage_2_lock",
            "battle/build_stage_3_lock",
            "battle/build_stage_4_lock",
            "battle/build_stage_5_lock",
        };

        public static readonly string[] BuildStageBaseStarGroupKeys =
        {
            "battle/build_stage_1_base_stars",
            "battle/build_stage_2_base_stars",
            "battle/build_stage_3_base_stars",
            "battle/build_stage_4_base_stars",
            "battle/build_stage_5_base_stars",
        };

        public static readonly string[] BuildStageActiveStarGroupKeys =
        {
            "battle/build_stage_1_active_stars",
            "battle/build_stage_2_active_stars",
            "battle/build_stage_3_active_stars",
            "battle/build_stage_4_active_stars",
            "battle/build_stage_5_active_stars",
        };

        public static readonly string[] StageButtonNodePaths =
        {
            RootNodePath + "/Map_bg/Stage1",
            RootNodePath + "/Map_bg/Stage2",
            RootNodePath + "/Map_bg/Stage3",
            RootNodePath + "/Map_bg/Stage4",
            RootNodePath + "/Map_bg/Stage5",
        };

        public static readonly string[] StageImageNodePaths =
        {
            RootNodePath + "/Map_bg/Stage1",
            RootNodePath + "/Map_bg/Stage2",
            RootNodePath + "/Map_bg/Stage3",
            RootNodePath + "/Map_bg/Stage4",
            RootNodePath + "/Map_bg/Stage5",
        };

        public static readonly string[] StageNameTextNodePaths =
        {
            RootNodePath + "/Map_bg/Stage1/Image/Text (TMP)",
            RootNodePath + "/Map_bg/Stage2/Image/Text (TMP)",
            RootNodePath + "/Map_bg/Stage3/Image/Text (TMP)",
            RootNodePath + "/Map_bg/Stage4/Image/Text (TMP)",
            RootNodePath + "/Map_bg/Stage5/Image/Text (TMP)",
        };

        public static readonly string[] StageLockNodePaths =
        {
            RootNodePath + "/Map_bg/Stage1/Image/lock",
            RootNodePath + "/Map_bg/Stage2/Image/lock",
            RootNodePath + "/Map_bg/Stage3/Image/lock",
            RootNodePath + "/Map_bg/Stage4/Image/lock",
            RootNodePath + "/Map_bg/Stage5/Image/lock",
        };

        public static readonly string[] StageBarNodePaths =
        {
            RootNodePath + "/Map_bg/StageBar1",
            RootNodePath + "/Map_bg/StageBar2",
            RootNodePath + "/Map_bg/StageBar3",
            RootNodePath + "/Map_bg/StageBar4",
        };

        public static readonly string[] BuildStageButtonNodePaths =
        {
            BuildButtonNodePath + "/BuildStage1",
            BuildButtonNodePath + "/BuildStage2",
            BuildButtonNodePath + "/BuildStage3",
            BuildButtonNodePath + "/BuildStage4",
            BuildButtonNodePath + "/BuildStage5",
        };

        public static readonly string[] BuildStageImageNodePaths =
        {
            BuildButtonNodePath + "/BuildStage1/Image",
            BuildButtonNodePath + "/BuildStage2/Image",
            BuildButtonNodePath + "/BuildStage3/Image",
            BuildButtonNodePath + "/BuildStage4/Image",
            BuildButtonNodePath + "/BuildStage5/Image",
        };

        public static readonly string[] BuildStageNameTextNodePaths =
        {
            BuildButtonNodePath + "/BuildStage1/Text (TMP)",
            BuildButtonNodePath + "/BuildStage2/Text (TMP)",
            BuildButtonNodePath + "/BuildStage3/Text (TMP)",
            BuildButtonNodePath + "/BuildStage4/Text (TMP)",
            BuildButtonNodePath + "/BuildStage5/Text (TMP)",
        };

        public static readonly string[] BuildStageLockNodePaths =
        {
            BuildButtonNodePath + "/BuildStage1/lock",
            BuildButtonNodePath + "/BuildStage2/lock",
            BuildButtonNodePath + "/BuildStage3/lock",
            BuildButtonNodePath + "/BuildStage4/lock",
            BuildButtonNodePath + "/BuildStage5/lock",
        };

        public static readonly string[] BuildStageBaseStarGroupNodePaths =
        {
            BuildButtonNodePath + "/BuildStage1/stargroup",
            BuildButtonNodePath + "/BuildStage2/stargroup",
            BuildButtonNodePath + "/BuildStage3/stargroup",
            BuildButtonNodePath + "/BuildStage4/stargroup",
            BuildButtonNodePath + "/BuildStage5/stargroup",
        };

        public static readonly string[] BuildStageActiveStarGroupNodePaths =
        {
            BuildButtonNodePath + "/BuildStage1/stargroup_1",
            BuildButtonNodePath + "/BuildStage2/stargroup_1",
            BuildButtonNodePath + "/BuildStage3/stargroup_1",
            BuildButtonNodePath + "/BuildStage4/stargroup_1",
            BuildButtonNodePath + "/BuildStage5/stargroup_1",
        };

        public RectTransform RootPanel;
        public Button BackButton;
        public Button BuildButton;
        public TextMeshProUGUI BuildButtonText;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI GoldText;
        public TextMeshProUGUI EnergyText;
        public TextMeshProUGUI SummaryText;
        public TextMeshProUGUI StatusText;
        public readonly Button[] StageButtons = new Button[BattlePresenter.VisibleStageCount];
        public readonly Image[] StageImages = new Image[BattlePresenter.VisibleStageCount];
        public readonly TextMeshProUGUI[] StageNameTexts = new TextMeshProUGUI[BattlePresenter.VisibleStageCount];
        public readonly RectTransform[] StageLocks = new RectTransform[BattlePresenter.VisibleStageCount];
        public readonly Image[] StageBars = new Image[BattlePresenter.VisibleStageBarCount];
        public readonly Button[] BuildStageButtons = new Button[BattlePresenter.VisibleStageCount];
        public readonly Image[] BuildStageImages = new Image[BattlePresenter.VisibleStageCount];
        public readonly TextMeshProUGUI[] BuildStageNameTexts = new TextMeshProUGUI[BattlePresenter.VisibleStageCount];
        public readonly RectTransform[] BuildStageLocks = new RectTransform[BattlePresenter.VisibleStageCount];
        public readonly RectTransform[] BuildStageBaseStarGroups = new RectTransform[BattlePresenter.VisibleStageCount];
        public readonly RectTransform[] BuildStageActiveStarGroups = new RectTransform[BattlePresenter.VisibleStageCount];

        public bool HasRequiredBindings
        {
            get
            {
                if (RootPanel == null ||
                    BackButton == null ||
                    BuildButton == null ||
                    LevelText == null ||
                    GoldText == null ||
                    EnergyText == null ||
                    SummaryText == null ||
                    StatusText == null)
                {
                    return false;
                }

                for (int i = 0; i < StageButtons.Length; i++)
                {
                    if (StageButtons[i] == null ||
                        StageImages[i] == null ||
                        StageNameTexts[i] == null ||
                        StageLocks[i] == null ||
                        BuildStageButtons[i] == null ||
                        BuildStageImages[i] == null ||
                        BuildStageNameTexts[i] == null ||
                        BuildStageLocks[i] == null ||
                        BuildStageBaseStarGroups[i] == null ||
                        BuildStageActiveStarGroups[i] == null)
                    {
                        return false;
                    }
                }

                for (int i = 0; i < StageBars.Length; i++)
                {
                    if (StageBars[i] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public static BattleBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new BattleBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel, nodePath: RootNodePath);
            resolver.TryResolve(BackButtonKey, out bindings.BackButton, ButtonClickEvent, BackButtonNodePath);
            resolver.TryResolve(BuildButtonKey, out bindings.BuildButton, ButtonClickEvent, BuildButtonNodePath);
            resolver.TryResolve(BuildButtonTextKey, out bindings.BuildButtonText, nodePath: BuildButtonTextNodePath);
            resolver.TryResolve(LevelTextKey, out bindings.LevelText, nodePath: LevelTextNodePath);
            resolver.TryResolve(GoldTextKey, out bindings.GoldText, nodePath: GoldTextNodePath);
            resolver.TryResolve(EnergyTextKey, out bindings.EnergyText, nodePath: EnergyTextNodePath);
            resolver.TryResolve(SummaryTextKey, out bindings.SummaryText, nodePath: SummaryTextNodePath);
            resolver.TryResolve(StatusTextKey, out bindings.StatusText, nodePath: StatusTextNodePath);
            for (int i = 0; i < bindings.StageButtons.Length; i++)
            {
                resolver.TryResolve(StageButtonKeys[i], out bindings.StageButtons[i], ButtonClickEvent, StageButtonNodePaths[i]);
                resolver.TryResolve(StageImageKeys[i], out bindings.StageImages[i], nodePath: StageImageNodePaths[i]);
                resolver.TryResolve(StageNameTextKeys[i], out bindings.StageNameTexts[i], nodePath: StageNameTextNodePaths[i]);
                resolver.TryResolve(StageLockKeys[i], out bindings.StageLocks[i], nodePath: StageLockNodePaths[i]);
                resolver.TryResolve(BuildStageButtonKeys[i], out bindings.BuildStageButtons[i], ButtonClickEvent, BuildStageButtonNodePaths[i]);
                resolver.TryResolve(BuildStageImageKeys[i], out bindings.BuildStageImages[i], nodePath: BuildStageImageNodePaths[i]);
                resolver.TryResolve(BuildStageNameTextKeys[i], out bindings.BuildStageNameTexts[i], nodePath: BuildStageNameTextNodePaths[i]);
                resolver.TryResolve(BuildStageLockKeys[i], out bindings.BuildStageLocks[i], nodePath: BuildStageLockNodePaths[i]);
                resolver.TryResolve(BuildStageBaseStarGroupKeys[i], out bindings.BuildStageBaseStarGroups[i], nodePath: BuildStageBaseStarGroupNodePaths[i]);
                resolver.TryResolve(BuildStageActiveStarGroupKeys[i], out bindings.BuildStageActiveStarGroups[i], nodePath: BuildStageActiveStarGroupNodePaths[i]);
            }

            for (int i = 0; i < bindings.StageBars.Length; i++)
            {
                resolver.TryResolve(StageBarKeys[i], out bindings.StageBars[i], nodePath: StageBarNodePaths[i]);
            }

            return bindings;
        }
    }
}
