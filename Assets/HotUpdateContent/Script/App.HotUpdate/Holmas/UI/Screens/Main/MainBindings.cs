using App.HotUpdate.Holmas.UI.Binding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainBindings
    {
        public const string RootPanelKey = "main/root_panel";
        public const string LevelTextKey = "main/level_text";
        public const string GoldTextKey = "main/gold_text";
        public const string EnergyTextKey = "main/energy_text";
        public const string SummaryTextKey = "main/summary_text";
        public const string StatusTextKey = "main/status_text";
        public const string PromotionButtonKey = "main/promotion_button";
        public const string AddEnergyButtonKey = "main/add_energy_button";
        public const string HelpButtonKey = "main/help_button";
        public const string GmButtonKey = "main/gm_button";
        public const string LeaderboardButtonKey = "main/leaderboard_button";
        public const string TutorialStepInputKey = "main/tutorial_step_input";
        public const string MinesBgImageKey = "main/mines_bg_image";
        public const string MinesBgMaskKey = "main/mines_bg_mask";
        public const string MinesBgFrameOverlayImageKey = "main/mines_bg_frame_overlay_image";
        public const string BoardContentRectKey = "main/board_content_rect";
        public const string MinesGroupKey = "main/mines_group";
        public const string BoardContainerKey = "main/board_container";
        public const string TutorialBoardContainerKey = "main/tutorial_board_container";
        public const string WalkToggleKey = "main/walk_toggle";
        public const string FindToggleKey = "main/find_toggle";
        public const string ButtonClickEvent = "on_click";
        public const string ToggleChangedEvent = "on_value_changed";
        public const int TaskSlotCount = 5;

        public const string RootNodePath = "MainPanel";
        public const string RuntimeOverlayNodeName = "RuntimeOverlay";
        public const string RuntimeOverlayNodePath = RootNodePath + "/" + RuntimeOverlayNodeName;
        public const string BottomToolsNodeName = "BottomTools";
        public const string BottomToolsNodePath = RuntimeOverlayNodePath + "/" + BottomToolsNodeName;
        public const string TopToolsNodeName = "TopTools";
        public const string TopToolsNodePath = RuntimeOverlayNodePath + "/" + TopToolsNodeName;
        public const string LevelTextNodePath = RootNodePath + "/BackgroundImage/Headicon_btn/Level";
        public const string GoldTextNodePath = RootNodePath + "/BackgroundImage/Money_btn/Text (TMP)";
        public const string EnergyTextNodePath = RootNodePath + "/BackgroundImage/Energy_btn/Text (TMP)";
        public const string SummaryTextNodePath = RuntimeOverlayNodePath + "/SummaryText";
        public const string StatusTextNodePath = RuntimeOverlayNodePath + "/StatusText";
        public const string PromotionButtonNodePath = RootNodePath + "/BackgroundImage/Publicity_btn";
        public const string AddEnergyButtonNodePath = RuntimeOverlayNodePath + "/AddEnergyButton";
        public const string HelpButtonNodePath = TopToolsNodePath + "/HelpButton";
        public const string GmButtonNodePath = TopToolsNodePath + "/GmButton";
        public const string LeaderboardButtonNodePath = RootNodePath + "/BackgroundImage/Leaderboard_btn";
        public const string TutorialStepInputNodePath = RuntimeOverlayNodePath + "/TutorialStepInput";
        public const string MinesBgNodePath = RootNodePath + "/BackgroundImage/MinesBg";
        public const string MinesBgFrameOverlayNodePath = MinesBgNodePath + "/MinesBgFrameOverlayImage";
        public const string BoardContentRectNodePath = MinesBgNodePath + "/BoardContentRect";
        public const string MinesGroupNodePath = MinesBgNodePath + "/MinesGroup";
        public const string BoardContainerNodePath = MinesGroupNodePath + "/BoardContainer";
        public const string TutorialBoardContainerNodePath = MinesGroupNodePath + "/TutorialBoardContainer";
        public const string WalkToggleNodePath = "MainPanel/WalkToggle";
        public const string FindToggleNodePath = "MainPanel/FindToggle";
        public static readonly string[] TaskTitleTextKeys =
        {
            "main/task1_title_text",
            "main/task2_title_text",
            "main/task3_title_text",
            "main/task4_title_text",
            "main/task5_title_text",
        };

        public static readonly string[] TaskSlotRootKeys =
        {
            "main/task1_root",
            "main/task2_root",
            "main/task3_root",
            "main/task4_root",
            "main/task5_root",
        };

        public static readonly string[] TaskSlotButtonKeys =
        {
            "main/task1_button",
            "main/task2_button",
            "main/task3_button",
            "main/task4_button",
            "main/task5_button",
        };

        public static readonly string[] TaskSlotBackgroundImageKeys =
        {
            "main/task1_background_image",
            "main/task2_background_image",
            "main/task3_background_image",
            "main/task4_background_image",
            "main/task5_background_image",
        };

        public static readonly string[] TaskProgressTextKeys =
        {
            "main/task1_progress_text",
            "main/task2_progress_text",
            "main/task3_progress_text",
            "main/task4_progress_text",
            "main/task5_progress_text",
        };

        public static readonly string[] TaskProgressSliderKeys =
        {
            "main/task1_progress_slider",
            "main/task2_progress_slider",
            "main/task3_progress_slider",
            "main/task4_progress_slider",
            "main/task5_progress_slider",
        };

        public static readonly string[] TaskRewardIconKeys =
        {
            "main/task1_reward_icon",
            "main/task2_reward_icon",
            "main/task3_reward_icon",
            "main/task4_reward_icon",
            "main/task5_reward_icon",
        };

        public static readonly string[] TaskCatIconKeys =
        {
            "main/task1_cat_icon",
            "main/task2_cat_icon",
            "main/task3_cat_icon",
            "main/task4_cat_icon",
            "main/task5_cat_icon",
        };

        public static readonly string[] TaskLockKeys =
        {
            "main/task1_lock",
            "main/task2_lock",
            "main/task3_lock",
            "main/task4_lock",
            "main/task5_lock",
        };

        public static readonly string[] TaskRewardTextKeys =
        {
            "main/task1_reward_text",
            "main/task2_reward_text",
            "main/task3_reward_text",
            "main/task4_reward_text",
            "main/task5_reward_text",
        };

        public static readonly string[] TaskSlotRootNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5",
        };

        public static readonly string[] TaskTitleTextNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1/TaskTitle",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2/TaskTitle",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3/TaskTitle",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4/TaskTitle",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5/TaskTitle",
        };

        public static readonly string[] TaskRewardTextNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1/TaskReward",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2/TaskReward",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3/TaskReward",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4/TaskReward",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5/TaskReward",
        };

        public static readonly string[] TaskProgressTextNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1/Image/Count",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2/Image/Count",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3/Image/Count",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4/Image/Count",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5/Image/Count",
        };

        public static readonly string[] TaskProgressSliderNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1/Image/Slider",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2/Image/Slider",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3/Image/Slider",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4/Image/Slider",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5/Image/Slider",
        };

        public static readonly string[] TaskRewardIconNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1/RewardIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2/RewardIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3/RewardIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4/RewardIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5/RewardIcon",
        };

        public static readonly string[] TaskCatIconNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1/CatIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2/CatIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3/CatIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4/CatIcon",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5/CatIcon",
        };

        public static readonly string[] TaskLockNodePaths =
        {
            RootNodePath + "/BackgroundImage/TaskGroup/Task1/lock",
            RootNodePath + "/BackgroundImage/TaskGroup/Task2/lock",
            RootNodePath + "/BackgroundImage/TaskGroup/Task3/lock",
            RootNodePath + "/BackgroundImage/TaskGroup/Task4/lock",
            RootNodePath + "/BackgroundImage/TaskGroup/Task5/lock",
        };

        public RectTransform RootPanel;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI GoldText;
        public TextMeshProUGUI EnergyText;
        public TextMeshProUGUI SummaryText;
        public TextMeshProUGUI StatusText;
        public Button PromotionButton;
        public Button AddEnergyButton;
        public Button HelpButton;
        public Button GmButton;
        public Button LeaderboardButton;
        public TMP_InputField TutorialStepInput;
        public Image MinesBgImage;
        public RectMask2D MinesBgMask;
        public Image MinesBgFrameOverlayImage;
        public RectTransform BoardContentRect;
        public RectTransform MinesGroup;
        public RectTransform BoardContainer;
        public RectTransform TutorialBoardContainer;
        public Toggle WalkToggle;
        public Toggle FindToggle;
        public readonly RectTransform[] TaskSlotRoots = new RectTransform[TaskSlotCount];
        public readonly Button[] TaskSlotButtons = new Button[TaskSlotCount];
        public readonly Image[] TaskSlotBackgroundImages = new Image[TaskSlotCount];
        public readonly Text[] TaskProgressTexts = new Text[TaskSlotCount];
        public readonly Slider[] TaskProgressSliders = new Slider[TaskSlotCount];
        public readonly Image[] TaskRewardIcons = new Image[TaskSlotCount];
        public readonly Image[] TaskCatIcons = new Image[TaskSlotCount];
        public readonly RectTransform[] TaskLocks = new RectTransform[TaskSlotCount];
        public readonly TextMeshProUGUI[] TaskTitleTexts = new TextMeshProUGUI[TaskSlotCount];
        public readonly TextMeshProUGUI[] TaskRewardTexts = new TextMeshProUGUI[TaskSlotCount];

        public bool HasRequiredBindings =>
            RootPanel != null &&
            LevelText != null &&
            GoldText != null &&
            EnergyText != null &&
            PromotionButton != null &&
            HelpButton != null &&
            GmButton != null &&
            LeaderboardButton != null &&
            MinesBgImage != null &&
            MinesBgMask != null &&
            MinesBgFrameOverlayImage != null &&
            BoardContentRect != null &&
            MinesGroup != null &&
            BoardContainer != null &&
            TutorialBoardContainer != null &&
            WalkToggle != null &&
            FindToggle != null &&
            HasAllTaskSlotBindings();

        public static bool HasCompleteBindings(UiBindingResolver resolver)
        {
            if (resolver == null)
            {
                return false;
            }

            if (!resolver.HasExplicitBinding<RectTransform>(RootPanelKey, nodePath: RootNodePath) ||
                !resolver.HasExplicitBinding<TextMeshProUGUI>(LevelTextKey, nodePath: LevelTextNodePath) ||
                !resolver.HasExplicitBinding<TextMeshProUGUI>(GoldTextKey, nodePath: GoldTextNodePath) ||
                !resolver.HasExplicitBinding<TextMeshProUGUI>(EnergyTextKey, nodePath: EnergyTextNodePath) ||
                !resolver.HasExplicitBinding<Button>(PromotionButtonKey, ButtonClickEvent, PromotionButtonNodePath) ||
                !resolver.HasExplicitBinding<Button>(HelpButtonKey, ButtonClickEvent, HelpButtonNodePath) ||
                !resolver.HasExplicitBinding<Button>(GmButtonKey, ButtonClickEvent, GmButtonNodePath) ||
                !resolver.HasExplicitBinding<Button>(LeaderboardButtonKey, ButtonClickEvent, LeaderboardButtonNodePath) ||
                !resolver.HasExplicitBinding<Image>(MinesBgImageKey, nodePath: MinesBgNodePath) ||
                !resolver.HasExplicitBinding<RectMask2D>(MinesBgMaskKey, nodePath: MinesBgNodePath) ||
                !resolver.HasExplicitBinding<Image>(MinesBgFrameOverlayImageKey, nodePath: MinesBgFrameOverlayNodePath) ||
                !resolver.HasExplicitBinding<RectTransform>(BoardContentRectKey, nodePath: BoardContentRectNodePath) ||
                !resolver.HasExplicitBinding<RectTransform>(MinesGroupKey, nodePath: MinesGroupNodePath) ||
                !resolver.HasExplicitBinding<RectTransform>(BoardContainerKey, nodePath: BoardContainerNodePath) ||
                !resolver.HasExplicitBinding<RectTransform>(TutorialBoardContainerKey, nodePath: TutorialBoardContainerNodePath) ||
                !resolver.HasExplicitBinding<Toggle>(WalkToggleKey, ToggleChangedEvent, WalkToggleNodePath) ||
                !resolver.HasExplicitBinding<Toggle>(FindToggleKey, ToggleChangedEvent, FindToggleNodePath))
            {
                return false;
            }

            for (int i = 0; i < TaskSlotCount; i++)
            {
                if (!resolver.HasExplicitBinding<RectTransform>(TaskSlotRootKeys[i], nodePath: TaskSlotRootNodePaths[i]) ||
                    !resolver.HasExplicitBinding<Button>(TaskSlotButtonKeys[i], ButtonClickEvent, TaskSlotRootNodePaths[i]) ||
                    !resolver.HasExplicitBinding<Image>(TaskSlotBackgroundImageKeys[i], nodePath: TaskSlotRootNodePaths[i]) ||
                    !resolver.HasExplicitBinding<Text>(TaskProgressTextKeys[i], nodePath: TaskProgressTextNodePaths[i]) ||
                    !resolver.HasExplicitBinding<Slider>(TaskProgressSliderKeys[i], nodePath: TaskProgressSliderNodePaths[i]) ||
                    !resolver.HasExplicitBinding<Image>(TaskRewardIconKeys[i], nodePath: TaskRewardIconNodePaths[i]) ||
                    !resolver.HasExplicitBinding<Image>(TaskCatIconKeys[i], nodePath: TaskCatIconNodePaths[i]) ||
                    !resolver.HasExplicitBinding<RectTransform>(TaskLockKeys[i], nodePath: TaskLockNodePaths[i]) ||
                    !resolver.HasExplicitBinding<TextMeshProUGUI>(TaskTitleTextKeys[i], nodePath: TaskTitleTextNodePaths[i]) ||
                    !resolver.HasExplicitBinding<TextMeshProUGUI>(TaskRewardTextKeys[i], nodePath: TaskRewardTextNodePaths[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static MainBindings Resolve(UiBindingResolver resolver)
        {
            var bindings = new MainBindings();
            if (resolver == null || !resolver.HasCollector)
            {
                return bindings;
            }

            resolver.TryResolve(RootPanelKey, out bindings.RootPanel, nodePath: RootNodePath);
            resolver.TryResolve(LevelTextKey, out bindings.LevelText, nodePath: LevelTextNodePath);
            resolver.TryResolve(GoldTextKey, out bindings.GoldText, nodePath: GoldTextNodePath);
            resolver.TryResolve(EnergyTextKey, out bindings.EnergyText, nodePath: EnergyTextNodePath);
            resolver.TryResolve(SummaryTextKey, out bindings.SummaryText, nodePath: SummaryTextNodePath);
            resolver.TryResolve(PromotionButtonKey, out bindings.PromotionButton, ButtonClickEvent, PromotionButtonNodePath);
            resolver.TryResolve(AddEnergyButtonKey, out bindings.AddEnergyButton, ButtonClickEvent, AddEnergyButtonNodePath);
            resolver.TryResolve(HelpButtonKey, out bindings.HelpButton, ButtonClickEvent, HelpButtonNodePath);
            resolver.TryResolve(GmButtonKey, out bindings.GmButton, ButtonClickEvent, GmButtonNodePath);
            resolver.TryResolve(LeaderboardButtonKey, out bindings.LeaderboardButton, ButtonClickEvent, LeaderboardButtonNodePath);
            resolver.TryResolve(TutorialStepInputKey, out bindings.TutorialStepInput, nodePath: TutorialStepInputNodePath);
            resolver.TryResolve(MinesBgImageKey, out bindings.MinesBgImage, nodePath: MinesBgNodePath);
            resolver.TryResolve(MinesBgMaskKey, out bindings.MinesBgMask, nodePath: MinesBgNodePath);
            resolver.TryResolve(MinesBgFrameOverlayImageKey, out bindings.MinesBgFrameOverlayImage, nodePath: MinesBgFrameOverlayNodePath);
            resolver.TryResolve(BoardContentRectKey, out bindings.BoardContentRect, nodePath: BoardContentRectNodePath);
            resolver.TryResolve(MinesGroupKey, out bindings.MinesGroup, nodePath: MinesGroupNodePath);
            resolver.TryResolve(BoardContainerKey, out bindings.BoardContainer, nodePath: BoardContainerNodePath);
            resolver.TryResolve(TutorialBoardContainerKey, out bindings.TutorialBoardContainer, nodePath: TutorialBoardContainerNodePath);
            resolver.TryResolve(WalkToggleKey, out bindings.WalkToggle, ToggleChangedEvent, WalkToggleNodePath);
            resolver.TryResolve(FindToggleKey, out bindings.FindToggle, ToggleChangedEvent, FindToggleNodePath);
            for (int i = 0; i < TaskSlotCount; i++)
            {
                resolver.TryResolve(TaskSlotRootKeys[i], out bindings.TaskSlotRoots[i], nodePath: TaskSlotRootNodePaths[i]);
                resolver.TryResolve(TaskSlotButtonKeys[i], out bindings.TaskSlotButtons[i], ButtonClickEvent, TaskSlotRootNodePaths[i]);
                resolver.TryResolve(TaskSlotBackgroundImageKeys[i], out bindings.TaskSlotBackgroundImages[i], nodePath: TaskSlotRootNodePaths[i]);
                resolver.TryResolve(TaskProgressTextKeys[i], out bindings.TaskProgressTexts[i], nodePath: TaskProgressTextNodePaths[i]);
                resolver.TryResolve(TaskProgressSliderKeys[i], out bindings.TaskProgressSliders[i], nodePath: TaskProgressSliderNodePaths[i]);
                resolver.TryResolve(TaskRewardIconKeys[i], out bindings.TaskRewardIcons[i], nodePath: TaskRewardIconNodePaths[i]);
                resolver.TryResolve(TaskCatIconKeys[i], out bindings.TaskCatIcons[i], nodePath: TaskCatIconNodePaths[i]);
                resolver.TryResolve(TaskLockKeys[i], out bindings.TaskLocks[i], nodePath: TaskLockNodePaths[i]);
                resolver.TryResolve(TaskTitleTextKeys[i], out bindings.TaskTitleTexts[i], nodePath: TaskTitleTextNodePaths[i]);
                resolver.TryResolve(TaskRewardTextKeys[i], out bindings.TaskRewardTexts[i], nodePath: TaskRewardTextNodePaths[i]);
            }

            return bindings;
        }

        private bool HasAllTaskSlotBindings()
        {
            for (int i = 0; i < TaskSlotCount; i++)
            {
                if (TaskSlotRoots[i] == null ||
                    TaskSlotButtons[i] == null ||
                    TaskSlotBackgroundImages[i] == null ||
                    TaskProgressTexts[i] == null ||
                    TaskProgressSliders[i] == null ||
                    TaskRewardIcons[i] == null ||
                    TaskCatIcons[i] == null ||
                    TaskLocks[i] == null ||
                    TaskTitleTexts[i] == null ||
                    TaskRewardTexts[i] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
