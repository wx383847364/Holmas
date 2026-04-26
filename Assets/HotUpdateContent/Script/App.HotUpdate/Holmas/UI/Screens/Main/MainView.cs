using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.FindCat;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainView : MonoBehaviour
    {
        private static readonly string[] TaskSlotNodeNames =
        {
            "Task1",
            "Task2",
            "Task3",
            "Task4",
            "Task5",
        };

        private readonly List<MainTaskSlotView> _taskSlotViews = new List<MainTaskSlotView>();

        private MainBindings _bindings;
        private FindCatBoardView _boardView;
        private FindCatBoardView _tutorialBoardView;
        private UnityAction _currentPromotionAction;
        private UnityAction _currentAddEnergyAction;
        private UnityAction _currentHelpAction;
        private UnityAction _currentGmAction;
        private UnityAction _currentStartTutorialAction;
        private UnityAction<bool> _currentWalkToggleAction;
        private UnityAction<bool> _currentFindToggleAction;
        private Action<int> _currentTaskSlotAction;
        private Action<int, bool> _currentCellAction;
        private bool _isSyncingToggles;
        private HolmasCatSpriteLoader _catSpriteLoader;

        public void EnsureBindingSurface()
        {
            gameObject.name = MainBindings.RootNodePath;

            UiReferenceCollector collector = gameObject.GetComponent<UiReferenceCollector>();
            if (collector == null)
            {
                collector = gameObject.AddComponent<UiReferenceCollector>();
            }

            RectTransform rootRect = gameObject.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            Stretch(rootRect);
            EnsureFallbackBackground();
            collector.RegisterOrReplace(MainBindings.RootPanelKey, rootRect, nodePath: MainBindings.RootNodePath);

            RectTransform overlay = GetOrCreateOverlayRoot();
            DestroyChildIfExists(overlay, "SummaryText");
            DestroyChildIfExists(overlay, "AddEnergyButton");
            DestroyChildIfExists(overlay, "TutorialStepInput");
            TextMeshProUGUI levelText = ResolveLevelText(overlay);
            TextMeshProUGUI goldText = ResolveGoldText(overlay);
            TextMeshProUGUI energyText = ResolveEnergyText(overlay);
            TextMeshProUGUI statusText = GetOrCreateRuntimeText(
                overlay,
                "StatusText",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 220f),
                new Vector2(-120f, 160f),
                28f,
                TextAlignmentOptions.TopLeft);
            RemoveStartButton(overlay);
            Button promotionButton = ResolvePromotionButton(overlay);
            RectTransform bottomTools = ResolveBottomToolsGroup(overlay);
            Button helpButton = ResolveHelpButton(bottomTools);
            ReparentToolButton(helpButton, bottomTools, 72f);
            Button gmButton = ResolveGmButton(bottomTools);
            ReparentToolButton(gmButton, bottomTools, 120f);
            gmButton.gameObject.SetActive(false);
            Button startTutorialButton = ResolveStartTutorialButton(bottomTools);
            ReparentToolButton(startTutorialButton, bottomTools, 180f);
            RectTransform minesGroup = ResolveMinesGroup(overlay);
            RectTransform boardContainer = GetOrCreateBoardContainer(minesGroup);
            RectTransform tutorialBoardContainer = GetOrCreateTutorialBoardContainer(minesGroup);
            Toggle walkToggle = ResolveModeToggle("WalkToggle", true);
            Toggle findToggle = ResolveModeToggle("FindToggle", false);
            EnsureExclusiveModeToggles(walkToggle, findToggle);

            collector.RegisterOrReplace(MainBindings.LevelTextKey, levelText, nodePath: MainBindings.LevelTextNodePath);
            collector.RegisterOrReplace(MainBindings.GoldTextKey, goldText, nodePath: MainBindings.GoldTextNodePath);
            collector.RegisterOrReplace(MainBindings.EnergyTextKey, energyText, nodePath: MainBindings.EnergyTextNodePath);
            collector.RegisterOrReplace(MainBindings.StatusTextKey, statusText, nodePath: MainBindings.StatusTextNodePath);
            collector.RegisterOrReplace(
                MainBindings.PromotionButtonKey,
                promotionButton,
                MainBindings.ButtonClickEvent,
                MainBindings.PromotionButtonNodePath);
            collector.RegisterOrReplace(
                MainBindings.HelpButtonKey,
                helpButton,
                MainBindings.ButtonClickEvent,
                MainBindings.HelpButtonNodePath);
            collector.RegisterOrReplace(
                MainBindings.GmButtonKey,
                gmButton,
                MainBindings.ButtonClickEvent,
                MainBindings.GmButtonNodePath);
            collector.RegisterOrReplace(
                MainBindings.StartTutorialButtonKey,
                startTutorialButton,
                MainBindings.ButtonClickEvent,
                MainBindings.StartTutorialButtonNodePath);
            collector.RegisterOrReplace(MainBindings.MinesGroupKey, minesGroup, nodePath: MainBindings.MinesGroupNodePath);
            collector.RegisterOrReplace(MainBindings.BoardContainerKey, boardContainer, nodePath: MainBindings.BoardContainerNodePath);
            collector.RegisterOrReplace(MainBindings.TutorialBoardContainerKey, tutorialBoardContainer, nodePath: MainBindings.TutorialBoardContainerNodePath);
            collector.RegisterOrReplace(
                MainBindings.WalkToggleKey,
                walkToggle,
                MainBindings.ToggleChangedEvent,
                MainBindings.WalkToggleNodePath);
            collector.RegisterOrReplace(
                MainBindings.FindToggleKey,
                findToggle,
                MainBindings.ToggleChangedEvent,
                MainBindings.FindToggleNodePath);

            EnsureTaskSlotViews();
        }

        public void Bind(MainBindings bindings)
        {
            _bindings = bindings ?? new MainBindings();
        }

        public void SetPromotionAction(UnityAction action)
        {
            if (_bindings?.PromotionButton == null)
            {
                _currentPromotionAction = action;
                return;
            }

            if (_currentPromotionAction != null)
            {
                _bindings.PromotionButton.onClick.RemoveListener(_currentPromotionAction);
            }

            _currentPromotionAction = action;
            if (_currentPromotionAction != null)
            {
                _bindings.PromotionButton.onClick.AddListener(_currentPromotionAction);
            }
        }

        public void SetAddEnergyAction(UnityAction action)
        {
            if (_bindings?.AddEnergyButton == null)
            {
                _currentAddEnergyAction = action;
                return;
            }

            if (_currentAddEnergyAction != null)
            {
                _bindings.AddEnergyButton.onClick.RemoveListener(_currentAddEnergyAction);
            }

            _currentAddEnergyAction = action;
            if (_currentAddEnergyAction != null)
            {
                _bindings.AddEnergyButton.onClick.AddListener(_currentAddEnergyAction);
            }
        }

        public void SetHelpAction(UnityAction action)
        {
            if (_bindings?.HelpButton == null)
            {
                _currentHelpAction = action;
                return;
            }

            if (_currentHelpAction != null)
            {
                _bindings.HelpButton.onClick.RemoveListener(_currentHelpAction);
            }

            _currentHelpAction = action;
            if (_currentHelpAction != null)
            {
                _bindings.HelpButton.onClick.AddListener(_currentHelpAction);
            }
        }

        public void SetGmAction(UnityAction action)
        {
            if (_bindings?.GmButton == null)
            {
                _currentGmAction = action;
                return;
            }

            if (_currentGmAction != null)
            {
                _bindings.GmButton.onClick.RemoveListener(_currentGmAction);
            }

            _currentGmAction = action;
            if (_currentGmAction != null)
            {
                _bindings.GmButton.onClick.AddListener(_currentGmAction);
            }
        }

        public void SetStartTutorialAction(UnityAction action)
        {
            if (_bindings?.StartTutorialButton == null)
            {
                _currentStartTutorialAction = action;
                return;
            }

            if (_currentStartTutorialAction != null)
            {
                _bindings.StartTutorialButton.onClick.RemoveListener(_currentStartTutorialAction);
            }

            _currentStartTutorialAction = action;
            if (_currentStartTutorialAction != null)
            {
                _bindings.StartTutorialButton.onClick.AddListener(_currentStartTutorialAction);
            }
        }

        public void SetModeToggleActions(UnityAction<bool> walkAction, UnityAction<bool> findAction)
        {
            if (_bindings?.WalkToggle == null || _bindings.FindToggle == null)
            {
                _currentWalkToggleAction = walkAction;
                _currentFindToggleAction = findAction;
                return;
            }

            if (_currentWalkToggleAction != null)
            {
                _bindings.WalkToggle.onValueChanged.RemoveListener(_currentWalkToggleAction);
            }

            if (_currentFindToggleAction != null)
            {
                _bindings.FindToggle.onValueChanged.RemoveListener(_currentFindToggleAction);
            }

            _currentWalkToggleAction = walkAction;
            _currentFindToggleAction = findAction;
            if (_currentWalkToggleAction != null)
            {
                _bindings.WalkToggle.onValueChanged.AddListener(_currentWalkToggleAction);
            }

            if (_currentFindToggleAction != null)
            {
                _bindings.FindToggle.onValueChanged.AddListener(_currentFindToggleAction);
            }
        }

        public void SetTaskSlotAction(Action<int> action)
        {
            _currentTaskSlotAction = action;
        }

        public void SetCellAction(Action<int, bool> action)
        {
            _currentCellAction = action;
        }

        public void SetAssetsRuntime(App.Shared.Contracts.IAssetsRuntime assetsRuntime)
        {
            if (_catSpriteLoader == null)
            {
                _catSpriteLoader = new HolmasCatSpriteLoader(assetsRuntime);
            }
            else
            {
                _catSpriteLoader.SetAssetsRuntime(assetsRuntime);
            }

            if (_boardView != null)
            {
                _boardView.SetCatSpriteLoader(_catSpriteLoader);
            }
        }

        public bool IsSyncingToggles => _isSyncingToggles;

        public void SetTutorialDebugControlsVisible(bool isVisible)
        {
            if (_bindings?.GmButton != null)
            {
                _bindings.GmButton.gameObject.SetActive(isVisible);
            }
        }

        public int GetTutorialStartStepIndex()
        {
            return 0;
        }

        public void Render(MainVm viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            if (_bindings?.LevelText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.LevelText, viewModel.LevelLabel);
            }

            if (_bindings?.GoldText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.GoldText, viewModel.GoldLabel);
            }

            if (_bindings?.EnergyText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.EnergyText, viewModel.EnergyLabel);
            }

            if (_bindings?.StatusText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.StatusText, viewModel.Status);
            }

            if (_bindings?.PromotionButton != null)
            {
                _bindings.PromotionButton.interactable = viewModel.PromotionButtonEnabled;
                SetButtonLabel(_bindings.PromotionButton, viewModel.PromotionButtonLabel);
            }

            if (_bindings?.HelpButton != null)
            {
                _bindings.HelpButton.interactable = true;
                SetButtonLabel(_bindings.HelpButton, "?");
            }

            if (_bindings?.GmButton != null)
            {
                _bindings.GmButton.interactable = true;
                SetButtonLabel(_bindings.GmButton, "GM");
            }

            if (_bindings?.StartTutorialButton != null)
            {
                _bindings.StartTutorialButton.interactable = true;
                SetButtonLabel(_bindings.StartTutorialButton, "开始引导");
            }

            SyncModeToggles(viewModel);
            RenderBoard(viewModel);
            RenderTaskSlots(viewModel.TaskItems ?? Array.Empty<MainTaskItemVm>());
        }

        private void EnsureFallbackBackground()
        {
            if (transform.Find("BackgroundImage") != null)
            {
                return;
            }

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.12f, 0.17f, 0.22f, 0.95f);
        }

        private static void RemoveStartButton(Transform overlay)
        {
            Transform startButton = overlay != null ? overlay.Find("StartButton") : null;
            if (startButton == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(startButton.gameObject);
            }
            else
            {
                DestroyImmediate(startButton.gameObject);
            }
        }

        private void EnsureTaskSlotViews()
        {
            if (_taskSlotViews.Count > 0)
            {
                return;
            }

            for (int i = 0; i < TaskSlotNodeNames.Length; i++)
            {
                Transform slotTransform = transform.Find(TaskSlotNodeNames[i]) ?? FindFirstTransformByName(TaskSlotNodeNames[i]);
                if (slotTransform == null)
                {
                    continue;
                }

                RectTransform slotRoot = slotTransform as RectTransform;
                if (slotRoot == null)
                {
                    continue;
                }

                Image background = slotRoot.GetComponent<Image>();
                if (background == null)
                {
                    background = slotRoot.gameObject.AddComponent<Image>();
                }

                background.enabled = true;
                Button button = slotRoot.GetComponent<Button>();
                if (button == null)
                {
                    button = slotRoot.gameObject.AddComponent<Button>();
                }

                int slotIndex = i;
                button.onClick.AddListener(() => _currentTaskSlotAction?.Invoke(slotIndex));

                var view = new MainTaskSlotView
                {
                    SlotIndex = slotIndex,
                    Root = slotRoot,
                    Background = background,
                    Button = button,
                    ProgressLabel = FindFirstDescendantByName<TextMeshProUGUI>(slotRoot, "Count")
                                    ?? FindFirstDescendantByName<TMP_Text>(slotRoot, "Count")
                                    ?? GetOrCreateRuntimeText(
                                        slotRoot,
                                        "RuntimeTaskProgress",
                                        new Vector2(0.18f, 0.5f),
                                        new Vector2(0.82f, 0.5f),
                                        new Vector2(0.5f, 0.5f),
                                        new Vector2(0f, 8f),
                                        new Vector2(0f, 28f),
                                        20f,
                                        TextAlignmentOptions.Center),
                    ProgressSlider = FindFirstDescendantByName<Slider>(slotRoot, "Slider"),
                    RewardIcon = FindFirstDescendantByName<Image>(slotRoot, "RewardIcon"),
                    CatIcon = FindFirstDescendantByName<Image>(slotRoot, "CatIcon"),
                    TitleLabel = GetOrCreateRuntimeText(
                        slotRoot,
                        "RuntimeTaskTitle",
                        new Vector2(0.08f, 1f),
                        new Vector2(0.92f, 1f),
                        new Vector2(0.5f, 1f),
                        new Vector2(0f, -10f),
                        new Vector2(0f, 34f),
                        18f,
                        TextAlignmentOptions.Center),
                    RewardLabel = GetOrCreateRuntimeText(
                        slotRoot,
                        "RuntimeTaskReward",
                        new Vector2(0.08f, 0f),
                        new Vector2(0.92f, 0f),
                        new Vector2(0.5f, 0f),
                        new Vector2(0f, 14f),
                        new Vector2(0f, 34f),
                        16f,
                        TextAlignmentOptions.Center),
                    AllTextLabels = slotRoot.GetComponentsInChildren<TMP_Text>(true),
                    AllSliders = slotRoot.GetComponentsInChildren<Slider>(true),
                };

                _taskSlotViews.Add(view);
            }
        }

        private void RenderTaskSlots(MainTaskItemVm[] items)
        {
            EnsureTaskSlotViews();
            for (int i = 0; i < _taskSlotViews.Count; i++)
            {
                MainTaskItemVm item = i < items.Length ? items[i] : null;
                RenderTaskSlot(_taskSlotViews[i], item);
            }
        }

        private void RenderTaskSlot(MainTaskSlotView slotView, MainTaskItemVm item)
        {
            if (slotView == null || slotView.Root == null)
            {
                return;
            }

            ResetTaskSlotResidualVisuals(slotView);

            if (item == null)
            {
                ApplyTaskSlotVisual(slotView, "未使用", string.Empty, "空位", 0f, false, false, false);
                _catSpriteLoader?.Clear(slotView.CatIcon);
                return;
            }

            ApplyTaskSlotVisual(
                slotView,
                item.Title,
                item.Progress,
                item.Reward,
                item.ProgressNormalized,
                item.IsLocked,
                item.IsClaimable,
                item.ButtonEnabled);

            if (slotView.CatIcon == null)
            {
                return;
            }

            if (item.IsLocked || item.IsEmpty)
            {
                _catSpriteLoader?.Clear(slotView.CatIcon);
                return;
            }

            _catSpriteLoader?.Bind(
                slotView.CatIcon,
                new HolmasCatVisualVm
                {
                    CatId = item.CatId,
                    CatName = item.CatName,
                    IconPath = item.CatIconPath,
                });
        }

        private static void ResetTaskSlotResidualVisuals(MainTaskSlotView slotView)
        {
            if (slotView == null)
            {
                return;
            }

            if (slotView.AllTextLabels != null)
            {
                for (int i = 0; i < slotView.AllTextLabels.Length; i++)
                {
                    TMP_Text label = slotView.AllTextLabels[i];
                    if (label == null ||
                        ReferenceEquals(label, slotView.TitleLabel) ||
                        ReferenceEquals(label, slotView.ProgressLabel) ||
                        ReferenceEquals(label, slotView.RewardLabel))
                    {
                        continue;
                    }

                    SetTmpText(label, string.Empty);
                }
            }

            if (slotView.AllSliders != null)
            {
                for (int i = 0; i < slotView.AllSliders.Length; i++)
                {
                    Slider slider = slotView.AllSliders[i];
                    if (slider == null || ReferenceEquals(slider, slotView.ProgressSlider))
                    {
                        continue;
                    }

                    slider.value = 0f;
                    slider.interactable = false;
                }
            }
        }

        private static void ApplyTaskSlotVisual(
            MainTaskSlotView slotView,
            string title,
            string progress,
            string reward,
            float progressValue,
            bool isLocked,
            bool isClaimable,
            bool buttonEnabled)
        {
            if (slotView.Background != null)
            {
                slotView.Background.color = isLocked
                    ? new Color(0.32f, 0.33f, 0.37f, 0.9f)
                    : (isClaimable
                        ? new Color(0.93f, 0.58f, 0.22f, 0.95f)
                        : new Color(0.22f, 0.24f, 0.31f, 0.88f));
            }

            if (slotView.Button != null)
            {
                slotView.Button.interactable = buttonEnabled;
            }

            SetTmpText(slotView.TitleLabel, title);
            SetTmpText(slotView.ProgressLabel, progress);
            SetTmpText(slotView.RewardLabel, reward);

            if (slotView.ProgressSlider != null)
            {
                slotView.ProgressSlider.value = Mathf.Clamp01(progressValue);
                slotView.ProgressSlider.interactable = false;
            }

            if (slotView.CatIcon != null)
            {
                slotView.CatIcon.color = isLocked
                    ? new Color(1f, 1f, 1f, 0.25f)
                    : new Color(1f, 1f, 1f, 1f);
            }

            if (slotView.RewardIcon != null)
            {
                slotView.RewardIcon.color = isClaimable
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.72f);
            }
        }

        private RectTransform GetOrCreateOverlayRoot()
        {
            Transform existing = transform.Find(MainBindings.RuntimeOverlayNodeName);
            GameObject overlayObject = existing != null ? existing.gameObject : new GameObject(MainBindings.RuntimeOverlayNodeName, typeof(RectTransform));
            overlayObject.transform.SetParent(transform, false);

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect);
            return overlayRect;
        }

        private TextMeshProUGUI ResolveLevelText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("BackgroundImage/Headicon_btn/Level")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("Level");
            if (existing != null)
            {
                existing.alignment = TextAlignmentOptions.Center;
                existing.raycastTarget = false;
                return existing;
            }

            return GetOrCreateRuntimeText(
                overlay,
                "LevelText",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(64f, -72f),
                new Vector2(180f, 72f),
                30f,
                TextAlignmentOptions.Left);
        }

        private TextMeshProUGUI ResolveGoldText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("BackgroundImage/Money_btn/Text (TMP)")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("MoneyCount")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("Text (TMP)");
            if (existing != null)
            {
                existing.raycastTarget = false;
                return existing;
            }

            return GetOrCreateRuntimeText(
                overlay,
                "GoldText",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-64f, -72f),
                new Vector2(240f, 72f),
                30f,
                TextAlignmentOptions.Right);
        }

        private TextMeshProUGUI ResolveEnergyText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("BackgroundImage/Energy_btn/Text (TMP)")
                ?? FindFirstDescendantByName<TextMeshProUGUI>("EnergyCount");
            if (existing != null)
            {
                existing.raycastTarget = false;
                return existing;
            }

            return GetOrCreateRuntimeText(
                overlay,
                "EnergyText",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-64f, -130f),
                new Vector2(220f, 64f),
                30f,
                TextAlignmentOptions.Right);
        }

        private Button ResolvePromotionButton(Transform overlay)
        {
            Button existing = FindDescendantComponent<Button>("BackgroundImage/Publicity_btn")
                ?? FindFirstDescendantByName<Button>("Publicity_btn");
            if (existing != null)
            {
                return existing;
            }

            return GetOrCreateRuntimeButton(
                overlay,
                "PromotionButton",
                "宣传升级",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-60f, 96f),
                new Vector2(240f, 96f),
                new Color(0.28f, 0.63f, 0.89f, 0.96f));
        }

        private RectTransform ResolveBottomToolsGroup(Transform overlay)
        {
            GameObject groupObject = GetOrCreateChild(overlay, "BottomTools");
            RectTransform rect = groupObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = groupObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                rect,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(40f, 96f),
                new Vector2(420f, 84f));

            HorizontalLayoutGroup layout = groupObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = groupObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 12f;

            ContentSizeFitter fitter = groupObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = groupObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            return rect;
        }

        private static void ReparentToolButton(Button button, RectTransform parent, float width)
        {
            if (button == null || parent == null)
            {
                return;
            }

            button.transform.SetParent(parent, false);
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                ConfigureRect(rect, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(width, 72f));
            }

            LayoutElement layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = button.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.minHeight = 72f;
            layoutElement.preferredHeight = 72f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }

        private Button ResolveGmButton(Transform overlay)
        {
            return GetOrCreateRuntimeButton(
                overlay,
                "GmButton",
                "GM",
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(120f, 72f),
                new Color(0.58f, 0.3f, 0.78f, 0.96f));
        }

        private Button ResolveHelpButton(Transform overlay)
        {
            return GetOrCreateRuntimeButton(
                overlay,
                "HelpButton",
                "?",
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(72f, 72f),
                new Color(0.26f, 0.34f, 0.42f, 0.95f));
        }

        private Button ResolveStartTutorialButton(Transform overlay)
        {
            return GetOrCreateRuntimeButton(
                overlay,
                "StartTutorialButton",
                "开始引导",
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(180f, 72f),
                new Color(0.22f, 0.56f, 0.36f, 0.95f));
        }

        private RectTransform ResolveMinesGroup(Transform overlay)
        {
            RectTransform existing = FindFirstDescendantByName<RectTransform>("MinesGroup");
            if (existing != null)
            {
                return existing;
            }

            GameObject minesGroupObject = GetOrCreateChild(transform, "MinesGroup");
            RectTransform minesGroup = minesGroupObject.GetComponent<RectTransform>();
            if (minesGroup == null)
            {
                minesGroup = minesGroupObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                minesGroup,
                new Vector2(0.08f, 0.17f),
                new Vector2(0.92f, 0.75f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            return minesGroup;
        }

        private RectTransform GetOrCreateBoardContainer(RectTransform minesGroup)
        {
            if (minesGroup == null)
            {
                return null;
            }

            GameObject boardObject = GetOrCreateChild(minesGroup, "BoardContainer");
            RectTransform boardContainer = boardObject.GetComponent<RectTransform>();
            if (boardContainer == null)
            {
                boardContainer = boardObject.AddComponent<RectTransform>();
            }

            Stretch(boardContainer);

            Image background = boardObject.GetComponent<Image>();
            if (background == null)
            {
                background = boardObject.AddComponent<Image>();
            }

            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = false;
            _boardView = boardObject.GetComponent<FindCatBoardView>() ?? boardObject.AddComponent<FindCatBoardView>();
            _boardView.SetCatSpriteLoader(_catSpriteLoader);
            return boardContainer;
        }

        private RectTransform GetOrCreateTutorialBoardContainer(RectTransform minesGroup)
        {
            if (minesGroup == null)
            {
                return null;
            }

            GameObject boardObject = GetOrCreateChild(minesGroup, "TutorialBoardContainer");
            RectTransform boardContainer = boardObject.GetComponent<RectTransform>();
            if (boardContainer == null)
            {
                boardContainer = boardObject.AddComponent<RectTransform>();
            }

            Stretch(boardContainer);

            Image background = boardObject.GetComponent<Image>();
            if (background == null)
            {
                background = boardObject.AddComponent<Image>();
            }

            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = false;
            boardObject.transform.SetAsLastSibling();
            _tutorialBoardView = boardObject.GetComponent<FindCatBoardView>() ?? boardObject.AddComponent<FindCatBoardView>();
            _tutorialBoardView.SetCatSpriteLoader(_catSpriteLoader);
            boardObject.SetActive(false);
            return boardContainer;
        }

        private Toggle ResolveModeToggle(string objectName, bool defaultOn)
        {
            Toggle toggle = FindFirstDescendantByName<Toggle>(objectName);
            if (toggle == null)
            {
                return null;
            }

            toggle.isOn = defaultOn;
            return toggle;
        }

        private ToggleGroup EnsureExclusiveModeToggles(Toggle walkToggle, Toggle findToggle)
        {
            if (walkToggle == null || findToggle == null)
            {
                return null;
            }

            Transform groupRoot = walkToggle.transform.parent != null &&
                                  walkToggle.transform.parent == findToggle.transform.parent
                ? walkToggle.transform.parent
                : walkToggle.transform.parent ?? findToggle.transform.parent;
            if (groupRoot == null)
            {
                return null;
            }

            ToggleGroup group = groupRoot.GetComponent<ToggleGroup>();
            if (group == null)
            {
                group = groupRoot.gameObject.AddComponent<ToggleGroup>();
            }

            group.allowSwitchOff = false;
            walkToggle.group = group;
            findToggle.group = group;
            walkToggle.SetIsOnWithoutNotify(true);
            findToggle.SetIsOnWithoutNotify(false);
            return group;
        }

        private void SyncModeToggles(MainVm viewModel)
        {
            if (_bindings?.WalkToggle == null || _bindings.FindToggle == null)
            {
                return;
            }

            _isSyncingToggles = true;
            _bindings.WalkToggle.SetIsOnWithoutNotify(viewModel.WalkToggleIsOn);
            _bindings.FindToggle.SetIsOnWithoutNotify(viewModel.FindToggleIsOn);
            _isSyncingToggles = false;
        }

        private void RenderBoard(MainVm viewModel)
        {
            if (_bindings?.BoardContainer == null)
            {
                return;
            }

            SetMinesGroupPlaceholderVisible(false);
            _bindings.BoardContainer.gameObject.SetActive(viewModel.BoardVisible);
            if (_bindings.TutorialBoardContainer != null)
            {
                _bindings.TutorialBoardContainer.gameObject.SetActive(viewModel.BoardVisible && viewModel.UseTutorialBoardLayer);
            }

            if (viewModel.UseTutorialBoardLayer && _bindings.TutorialBoardContainer != null)
            {
                _bindings.BoardContainer.gameObject.SetActive(false);
            }
            if (!viewModel.BoardVisible)
            {
                _boardView?.Render(0, 0, null, null, _currentCellAction);
                _tutorialBoardView?.Render(0, 0, null, null, _currentCellAction);
                return;
            }

            RectTransform activeContainer = viewModel.UseTutorialBoardLayer && _bindings.TutorialBoardContainer != null
                ? _bindings.TutorialBoardContainer
                : _bindings.BoardContainer;
            FindCatBoardView boardView = activeContainer.GetComponent<FindCatBoardView>() ?? activeContainer.gameObject.AddComponent<FindCatBoardView>();
            if (viewModel.UseTutorialBoardLayer)
            {
                _tutorialBoardView = boardView;
                _boardView?.Render(0, 0, null, null, _currentCellAction);
            }
            else
            {
                _boardView = boardView;
                _tutorialBoardView?.Render(0, 0, null, null, _currentCellAction);
            }

            boardView.SetCatSpriteLoader(_catSpriteLoader);
            boardView.Render(viewModel.Rows, viewModel.Cols, viewModel.Cells, viewModel.CatVisuals, _currentCellAction);
        }

        public RectTransform ResolveTutorialTarget(string targetKey)
        {
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                return null;
            }

            if (targetKey.StartsWith("BoardCell:", StringComparison.Ordinal))
            {
                string rawIndex = targetKey.Substring("BoardCell:".Length);
                FindCatBoardView activeBoard = _tutorialBoardView != null &&
                                               _tutorialBoardView.gameObject.activeInHierarchy
                    ? _tutorialBoardView
                    : _boardView;
                return int.TryParse(rawIndex, out int cellIndex) && activeBoard != null
                    ? activeBoard.GetCellRectTransform(cellIndex)
                    : _bindings?.BoardContainer;
            }

            switch (targetKey)
            {
                case "TaskBar":
                    EnsureTaskSlotViews();
                    return _taskSlotViews.Count > 0 ? _taskSlotViews[0].Root : null;
                case "WalkToggle":
                    return _bindings?.WalkToggle != null ? _bindings.WalkToggle.transform as RectTransform : null;
                case "FindToggle":
                    return _bindings?.FindToggle != null ? _bindings.FindToggle.transform as RectTransform : null;
                case "EnergyArea":
                    return _bindings?.EnergyText != null ? _bindings.EnergyText.transform as RectTransform : null;
                case "PromotionButton":
                    return _bindings?.PromotionButton != null ? _bindings.PromotionButton.transform as RectTransform : null;
                case "HelpButton":
                    return _bindings?.HelpButton != null ? _bindings.HelpButton.transform as RectTransform : null;
                default:
                    return null;
            }
        }

        private void OnDestroy()
        {
            _catSpriteLoader?.Dispose();
            _catSpriteLoader = null;
        }

        private void SetMinesGroupPlaceholderVisible(bool isVisible)
        {
            if (_bindings?.MinesGroup == null)
            {
                return;
            }

            GridLayoutGroup parentLayout = _bindings.MinesGroup.GetComponent<GridLayoutGroup>();
            if (parentLayout != null)
            {
                parentLayout.enabled = isVisible;
            }

            for (int i = 0; i < _bindings.MinesGroup.childCount; i++)
            {
                Transform child = _bindings.MinesGroup.GetChild(i);
                if (child != null && child.name != "BoardContainer" && child.name != "TutorialBoardContainer")
                {
                    child.gameObject.SetActive(isVisible);
                }
            }
        }

        private TextMeshProUGUI GetOrCreateRuntimeText(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = GetOrCreateChild(parent, objectName);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = textObject.AddComponent<RectTransform>();
            }

            ConfigureRect(rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private Button GetOrCreateRuntimeButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            GameObject buttonObject = GetOrCreateChild(parent, objectName);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = buttonObject.AddComponent<RectTransform>();
            }

            ConfigureRect(rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

            Image image = buttonObject.GetComponent<Image>();
            if (image == null)
            {
                image = buttonObject.AddComponent<Image>();
            }

            image.color = color;

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }

            TextMeshProUGUI labelText = GetOrCreateRuntimeText(
                buttonObject.transform,
                objectName + "_Label",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                34f,
                TextAlignmentOptions.Center);
            TmpGlyphCoverageReporter.SetText(labelText, label);
            return button;
        }

        private void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                string effectiveLabel = string.IsNullOrWhiteSpace(label) ? button.name : label;
                tmp.enableWordWrapping = true;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.fontSize = effectiveLabel.Length > 16 ? 24f : 34f;
                TmpGlyphCoverageReporter.SetText(tmp, effectiveLabel);
                return;
            }

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                legacyText.verticalOverflow = VerticalWrapMode.Truncate;
                legacyText.text = string.IsNullOrWhiteSpace(label) ? button.name : label;
            }
        }

        private T FindDescendantComponent<T>(string path) where T : Component
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private T FindFirstDescendantByName<T>(string objectName) where T : Component
        {
            return FindFirstDescendantByName<T>(transform, objectName);
        }

        private static T FindFirstDescendantByName<T>(Transform root, string objectName) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == objectName)
                {
                    T component = all[i].GetComponent<T>();
                    if (component != null)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private Transform FindFirstTransformByName(string objectName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == objectName)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void SetTmpText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            RuntimeTmpFontResolver.EnsureFontSupportsText(text, value);
            text.text = value ?? string.Empty;
        }

        private static GameObject GetOrCreateChild(Transform parent, string objectName)
        {
            Transform child = parent.Find(objectName);
            if (child != null)
            {
                return child.gameObject;
            }

            var childObject = new GameObject(objectName);
            childObject.transform.SetParent(parent, false);
            return childObject;
        }

        private static void DestroyChildIfExists(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return;
            }

            Transform child = parent.Find(objectName);
            if (child == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(child.gameObject);
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void ConfigureRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.localScale = Vector3.one;
        }

        private sealed class MainTaskSlotView
        {
            public int SlotIndex;
            public RectTransform Root;
            public Image Background;
            public Button Button;
            public TMP_Text TitleLabel;
            public TMP_Text ProgressLabel;
            public TMP_Text RewardLabel;
            public Slider ProgressSlider;
            public Image RewardIcon;
            public Image CatIcon;
            public TMP_Text[] AllTextLabels;
            public Slider[] AllSliders;
        }
    }
}
