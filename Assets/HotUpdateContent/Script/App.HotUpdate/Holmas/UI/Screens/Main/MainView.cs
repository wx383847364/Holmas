using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.FindCat;
using App.HotUpdate.Holmas.UI.Tool;
using App.Shared.Contracts;
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
        private UnityAction _currentLeaderboardAction;
        private UnityAction<bool> _currentWalkToggleAction;
        private UnityAction<bool> _currentFindToggleAction;
        private Action<int> _currentTaskSlotAction;
        private Action<int, bool> _currentCellAction;
        private Toggle _exclusiveWalkToggle;
        private Toggle _exclusiveFindToggle;
        private bool _isSyncingToggles;
        private bool _isEnforcingModeToggles;
        private HolmasCatSpriteLoader _catSpriteLoader;
        private IAssetsRuntime _assetsRuntime;
        private IAssetHandle _boardBackgroundHandle;
        private IAssetHandle _boardFrameOverlayHandle;
        private Sprite _defaultBoardBackgroundSprite;
        private Sprite _defaultBoardFrameOverlaySprite;
        private string _activeBoardBackgroundPath = string.Empty;
        private string _activeBoardFrameOverlayPath = string.Empty;
        private string _requestedBoardBackgroundPath = string.Empty;
        private string _requestedBoardFrameOverlayPath = string.Empty;
        private int _boardBackgroundRequestGeneration;
        private int _lastBoardRows;
        private int _lastBoardCols;
        private float _lastMinCellSpacing = BoardFrameLayoutCalculator.DefaultMinimumSpacing;
        private Vector2 _lastAppliedBoardFrameSize = new Vector2(-1f, -1f);
        private Vector2 _lastAppliedBoardContentOffsetMin = new Vector2(float.NaN, float.NaN);
        private Vector2 _lastAppliedBoardContentOffsetMax = new Vector2(float.NaN, float.NaN);

        #if UNITY_EDITOR
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
            DestroyChildIfExists(overlay, "StatusText");
            RemoveStartButton(overlay);
            Button promotionButton = ResolvePromotionButton(overlay);
            RectTransform bottomTools = ResolveBottomToolsGroup(overlay);
            RectTransform topTools = ResolveTopToolsGroup(overlay);
            Button helpButton = ResolveHelpButton(topTools);
            ReparentToolButton(helpButton, topTools, 72f);
            Button gmButton = ResolveGmButton(topTools);
            ReparentToolButton(gmButton, topTools, 120f);
            gmButton.gameObject.SetActive(false);
            Button leaderboardButton = ResolveLeaderboardButton(overlay);
            DestroyChildIfExists(bottomTools, "StartTutorialButton");
            DestroyChildIfExists(bottomTools, "HelpButton");
            DestroyChildIfExists(bottomTools, "GmButton");
            Image minesBgImage = ResolveMinesBgImage();
            RectMask2D minesBgMask = ResolveMinesBgMask(minesBgImage);
            Image minesBgFrameOverlayImage = ResolveMinesBgFrameOverlayImage(minesBgImage);
            RectTransform boardContentRect = ResolveBoardContentRect(minesBgImage);
            RectTransform minesGroup = ResolveMinesGroup(overlay);
            RectTransform boardContainer = GetOrCreateBoardContainer(minesGroup);
            RectTransform tutorialBoardContainer = GetOrCreateTutorialBoardContainer(minesGroup);
            if (minesBgFrameOverlayImage != null)
            {
                minesBgFrameOverlayImage.transform.SetAsLastSibling();
            }
            Toggle walkToggle = ResolveModeToggle("WalkToggle", true);
            Toggle findToggle = ResolveModeToggle("FindToggle", false);
            EnsureExclusiveModeToggles(walkToggle, findToggle);

            collector.RegisterOrReplace(MainBindings.LevelTextKey, levelText, nodePath: MainBindings.LevelTextNodePath);
            collector.RegisterOrReplace(MainBindings.GoldTextKey, goldText, nodePath: MainBindings.GoldTextNodePath);
            collector.RegisterOrReplace(MainBindings.EnergyTextKey, energyText, nodePath: MainBindings.EnergyTextNodePath);
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
                MainBindings.LeaderboardButtonKey,
                leaderboardButton,
                MainBindings.ButtonClickEvent,
                MainBindings.LeaderboardButtonNodePath);
            collector.RegisterOrReplace(MainBindings.MinesBgImageKey, minesBgImage, nodePath: MainBindings.MinesBgNodePath);
            collector.RegisterOrReplace(MainBindings.MinesBgMaskKey, minesBgMask, nodePath: MainBindings.MinesBgNodePath);
            collector.RegisterOrReplace(MainBindings.MinesBgFrameOverlayImageKey, minesBgFrameOverlayImage, nodePath: MainBindings.MinesBgFrameOverlayNodePath);
            collector.RegisterOrReplace(MainBindings.BoardContentRectKey, boardContentRect, nodePath: MainBindings.BoardContentRectNodePath);
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

            EnsureTaskSlotViewsForAuthoring();
            RegisterTaskSlotBindings(collector);
        }
        #endif

        public void Bind(MainBindings bindings)
        {
            _bindings = bindings ?? new MainBindings();
            if (_bindings.MinesBgImage != null && _defaultBoardBackgroundSprite == null)
            {
                _defaultBoardBackgroundSprite = _bindings.MinesBgImage.sprite;
            }

            if (_bindings.MinesBgFrameOverlayImage != null)
            {
                ConfigureBoardFrameOverlayImage(_bindings.MinesBgFrameOverlayImage);
                if (_defaultBoardFrameOverlaySprite == null)
                {
                    _defaultBoardFrameOverlaySprite = _bindings.MinesBgFrameOverlayImage.sprite;
                }
            }

            _boardView = _bindings.BoardContainer != null ? _bindings.BoardContainer.GetComponent<FindCatBoardView>() : null;
            _tutorialBoardView = _bindings.TutorialBoardContainer != null ? _bindings.TutorialBoardContainer.GetComponent<FindCatBoardView>() : null;
            DisableButtonGraphicTint(_bindings.PromotionButton);
            DisableButtonGraphicTint(_bindings.HelpButton);
            DisableButtonGraphicTint(_bindings.GmButton);
            DisableButtonGraphicTint(_bindings.LeaderboardButton);
            DisableSelectableGraphicTint(_bindings.WalkToggle);
            DisableSelectableGraphicTint(_bindings.FindToggle);
            ApplyBoundTaskSlotTexts();
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

        public void SetLeaderboardAction(UnityAction action)
        {
            if (_bindings?.LeaderboardButton == null)
            {
                _currentLeaderboardAction = action;
                return;
            }

            if (_currentLeaderboardAction != null)
            {
                _bindings.LeaderboardButton.onClick.RemoveListener(_currentLeaderboardAction);
            }

            _currentLeaderboardAction = action;
            if (_currentLeaderboardAction != null)
            {
                _bindings.LeaderboardButton.onClick.AddListener(_currentLeaderboardAction);
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

        public void SetAssetsRuntime(IAssetsRuntime assetsRuntime)
        {
            if (!ReferenceEquals(_assetsRuntime, assetsRuntime))
            {
                _boardBackgroundRequestGeneration++;
                ReleaseBoardFrameHandles();
                _activeBoardBackgroundPath = string.Empty;
                _activeBoardFrameOverlayPath = string.Empty;
                _requestedBoardBackgroundPath = string.Empty;
                _requestedBoardFrameOverlayPath = string.Empty;
                _assetsRuntime = assetsRuntime;
            }

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

        public void HideTutorialBoardLayer()
        {
            if (_bindings?.TutorialBoardContainer != null)
            {
                _bindings.TutorialBoardContainer.gameObject.SetActive(false);
            }

            _tutorialBoardView?.Render(0, 0, null, null, _currentCellAction);
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

            if (_bindings?.LeaderboardButton != null)
            {
                _bindings.LeaderboardButton.interactable = true;
            }

            SyncModeToggles(viewModel);
            RenderBoard(viewModel);
            RenderTaskSlots(viewModel.TaskItems ?? Array.Empty<MainTaskItemVm>());
        }

        #if UNITY_EDITOR
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
        #endif

        private void EnsureTaskSlotViews()
        {
            if (_taskSlotViews.Count > 0)
            {
                return;
            }

            if (_bindings == null)
            {
                return;
            }

            for (int i = 0; i < MainBindings.TaskSlotCount; i++)
            {
                RectTransform slotRoot = _bindings.TaskSlotRoots[i];
                if (slotRoot == null)
                {
                    continue;
                }

                Button button = _bindings.TaskSlotButtons[i];
                if (button != null)
                {
                    button.transition = Selectable.Transition.None;
                    button.targetGraphic = null;
                    int slotIndex = i;
                    button.onClick.AddListener(() => _currentTaskSlotAction?.Invoke(slotIndex));
                }

                var view = new MainTaskSlotView
                {
                    SlotIndex = i,
                    Root = slotRoot,
                    Background = _bindings.TaskSlotBackgroundImages[i],
                    Button = button,
                    ProgressLegacyLabel = _bindings.TaskProgressTexts[i],
                    ProgressSlider = _bindings.TaskProgressSliders[i],
                    RewardIcon = _bindings.TaskRewardIcons[i],
                    CatIcon = _bindings.TaskCatIcons[i],
                    LockObject = _bindings.TaskLocks[i] != null ? _bindings.TaskLocks[i].gameObject : null,
                    TitleLabel = _bindings.TaskTitleTexts[i],
                    RewardLabel = _bindings.TaskRewardTexts[i],
                    AllTextLabels = new TMP_Text[]
                    {
                        _bindings.TaskTitleTexts[i],
                        _bindings.TaskRewardTexts[i],
                    },
                    AllSliders = new Slider[] { _bindings.TaskProgressSliders[i] },
                };

                _taskSlotViews.Add(view);
            }
        }

        #if UNITY_EDITOR
        private void EnsureTaskSlotViewsForAuthoring()
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

                button.transition = Selectable.Transition.None;
                button.targetGraphic = null;

                int slotIndex = i;
                var view = new MainTaskSlotView
                {
                    SlotIndex = slotIndex,
                    Root = slotRoot,
                    Background = background,
                    Button = button,
                    ProgressLabel = FindFirstDescendantByName<TextMeshProUGUI>(slotRoot, "Count")
                                    ?? FindFirstDescendantByName<TMP_Text>(slotRoot, "Count"),
                    ProgressLegacyLabel = FindFirstDescendantByName<Text>(slotRoot, "Count"),
                    ProgressSlider = FindFirstDescendantByName<Slider>(slotRoot, "Slider"),
                    RewardIcon = FindFirstDescendantByName<Image>(slotRoot, "RewardIcon"),
                    CatIcon = FindFirstDescendantByName<Image>(slotRoot, "CatIcon"),
                    LockObject = FindFirstDescendantByName<RectTransform>(slotRoot, "lock")?.gameObject,
                    TitleLabel = ResolveTaskSlotText(slotRoot, "TaskTitle"),
                    RewardLabel = ResolveTaskSlotText(slotRoot, "TaskReward"),
                    AllTextLabels = slotRoot.GetComponentsInChildren<TMP_Text>(true),
                    AllSliders = slotRoot.GetComponentsInChildren<Slider>(true),
                };

                _taskSlotViews.Add(view);
            }
        }

        private void RegisterTaskSlotBindings(UiReferenceCollector collector)
        {
            if (collector == null)
            {
                return;
            }

            for (int i = 0; i < _taskSlotViews.Count && i < MainBindings.TaskSlotCount; i++)
            {
                MainTaskSlotView slotView = _taskSlotViews[i];
                if (slotView == null)
                {
                    continue;
                }

                collector.RegisterOrReplace(
                    MainBindings.TaskSlotRootKeys[i],
                    slotView.Root,
                    nodePath: MainBindings.TaskSlotRootNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskSlotButtonKeys[i],
                    slotView.Button,
                    MainBindings.ButtonClickEvent,
                    MainBindings.TaskSlotRootNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskSlotBackgroundImageKeys[i],
                    slotView.Background,
                    nodePath: MainBindings.TaskSlotRootNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskProgressTextKeys[i],
                    slotView.ProgressLegacyLabel,
                    nodePath: MainBindings.TaskProgressTextNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskProgressSliderKeys[i],
                    slotView.ProgressSlider,
                    nodePath: MainBindings.TaskProgressSliderNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskRewardIconKeys[i],
                    slotView.RewardIcon,
                    nodePath: MainBindings.TaskRewardIconNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskCatIconKeys[i],
                    slotView.CatIcon,
                    nodePath: MainBindings.TaskCatIconNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskLockKeys[i],
                    slotView.LockObject != null ? slotView.LockObject.GetComponent<RectTransform>() : null,
                    nodePath: MainBindings.TaskLockNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskTitleTextKeys[i],
                    slotView.TitleLabel as TextMeshProUGUI,
                    nodePath: MainBindings.TaskTitleTextNodePaths[i]);
                collector.RegisterOrReplace(
                    MainBindings.TaskRewardTextKeys[i],
                    slotView.RewardLabel as TextMeshProUGUI,
                    nodePath: MainBindings.TaskRewardTextNodePaths[i]);
            }
        }
        #endif

        private void ApplyBoundTaskSlotTexts()
        {
            EnsureTaskSlotViews();
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
                _catSpriteLoader?.Clear(slotView.CatIcon, preserveImageColor: true);
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
                _catSpriteLoader?.Clear(slotView.CatIcon, preserveImageColor: true);
                return;
            }

            _catSpriteLoader?.Bind(
                slotView.CatIcon,
                new HolmasCatVisualVm
                {
                    CatId = item.CatId,
                    CatName = item.CatName,
                    IconPath = item.CatIconPath,
                },
                preserveImageColor: true);
        }

        #if UNITY_EDITOR
        private static TMP_Text ResolveTaskSlotText(Transform slotRoot, string staticName)
        {
            return FindFirstDescendantByName<TextMeshProUGUI>(slotRoot, staticName)
                   ?? FindFirstDescendantByName<TMP_Text>(slotRoot, staticName);
        }
        #endif

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
            if (slotView.Button != null)
            {
                slotView.Button.interactable = buttonEnabled;
            }

            SetTmpText(slotView.TitleLabel, title);
            SetTaskSlotProgressText(slotView, progress);
            SetTmpText(slotView.RewardLabel, reward);

            if (slotView.ProgressSlider != null)
            {
                slotView.ProgressSlider.value = Mathf.Clamp01(progressValue);
                slotView.ProgressSlider.interactable = false;
            }

            if (slotView.LockObject != null)
            {
                slotView.LockObject.SetActive(isLocked);
            }
        }

        #if UNITY_EDITOR
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
                DisableButtonGraphicTint(existing);
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

        private Button ResolveLeaderboardButton(Transform overlay)
        {
            Button existing = FindDescendantComponent<Button>("BackgroundImage/Leaderboard_btn")
                ?? FindFirstDescendantByName<Button>("Leaderboard_btn");
            if (existing != null)
            {
                DisableButtonGraphicTint(existing);
                return existing;
            }

            return GetOrCreateRuntimeButton(
                overlay,
                "Leaderboard_btn",
                "排行榜",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-220f, -40f),
                new Vector2(132f, 72f),
                new Color(0.18f, 0.43f, 0.78f, 0.96f));
        }

        #if UNITY_EDITOR
        private Image ResolveMinesBgImage()
        {
            Image image = FindDescendantComponent<Image>("BackgroundImage/MinesBg");
            if (image != null)
            {
                return image;
            }

            RectTransform minesGroup = FindFirstDescendantByName<RectTransform>("MinesGroup");
            Transform parent = minesGroup != null && minesGroup.parent != null ? minesGroup.parent : transform;
            image = parent.GetComponent<Image>();
            if (image == null)
            {
                image = parent.gameObject.AddComponent<Image>();
            }

            return image;
        }

        private static RectMask2D ResolveMinesBgMask(Image minesBgImage)
        {
            if (minesBgImage == null)
            {
                return null;
            }

            RectMask2D mask = minesBgImage.GetComponent<RectMask2D>();
            if (mask == null)
            {
                mask = minesBgImage.gameObject.AddComponent<RectMask2D>();
            }

            mask.padding = Vector4.zero;
            return mask;
        }

        private static Image ResolveMinesBgFrameOverlayImage(Image minesBgImage)
        {
            if (minesBgImage == null)
            {
                return null;
            }

            Transform existing = minesBgImage.transform.Find("MinesBgFrameOverlayImage");
            GameObject overlayObject = existing != null
                ? existing.gameObject
                : new GameObject("MinesBgFrameOverlayImage", typeof(RectTransform), typeof(Image));
            if (existing == null)
            {
                overlayObject.transform.SetParent(minesBgImage.transform, false);
            }

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>() ?? overlayObject.AddComponent<RectTransform>();
            Stretch(overlayRect);

            Image overlayImage = overlayObject.GetComponent<Image>() ?? overlayObject.AddComponent<Image>();
            overlayImage.sprite = overlayImage.sprite != null ? overlayImage.sprite : minesBgImage.sprite;
            overlayImage.type = Image.Type.Sliced;
            overlayImage.fillCenter = false;
            overlayImage.raycastTarget = false;
            return overlayImage;
        }

        private static RectTransform ResolveBoardContentRect(Image minesBgImage)
        {
            if (minesBgImage == null)
            {
                return null;
            }

            Transform existing = minesBgImage.transform.Find("BoardContentRect");
            GameObject contentObject = existing != null
                ? existing.gameObject
                : new GameObject("BoardContentRect", typeof(RectTransform));
            if (existing == null)
            {
                contentObject.transform.SetParent(minesBgImage.transform, false);
            }

            RectTransform contentRect = contentObject.GetComponent<RectTransform>() ?? contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = new Vector2(22f, 24f);
            contentRect.offsetMax = new Vector2(-22f, -19f);
            contentRect.localScale = Vector3.one;
            return contentRect;
        }
        #endif

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

        private RectTransform ResolveTopToolsGroup(Transform overlay)
        {
            GameObject groupObject = GetOrCreateChild(overlay, MainBindings.TopToolsNodeName);
            RectTransform rect = groupObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = groupObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                rect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-40f, -40f),
                new Vector2(420f, 84f));

            HorizontalLayoutGroup layout = groupObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = groupObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleRight;
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
            return ResolveToolButton(
                overlay,
                "GmButton",
                "GM",
                new Vector2(120f, 72f),
                new Color(0.58f, 0.3f, 0.78f, 0.96f));
        }

        private Button ResolveHelpButton(Transform overlay)
        {
            return ResolveToolButton(
                overlay,
                "HelpButton",
                "?",
                new Vector2(72f, 72f),
                new Color(0.26f, 0.34f, 0.42f, 0.95f));
        }

        private Button ResolveToolButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 sizeDelta,
            Color color)
        {
            Button existing = FindFirstDescendantByName<Button>(objectName);
            if (existing != null)
            {
                existing.transform.SetParent(parent, false);
                DisableButtonGraphicTint(existing);
                SetButtonLabel(existing, label);
                return existing;
            }

            return GetOrCreateRuntimeButton(
                parent,
                objectName,
                label,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 0.5f),
                Vector2.zero,
                sizeDelta,
                color);
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
        #endif

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
            ConfigureModeToggleExclusivity(walkToggle, findToggle);
            SetExclusiveMode(true);
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

        private void ConfigureModeToggleExclusivity(Toggle walkToggle, Toggle findToggle)
        {
            if (_exclusiveWalkToggle != null)
            {
                _exclusiveWalkToggle.onValueChanged.RemoveListener(OnExclusiveWalkToggleChanged);
            }

            if (_exclusiveFindToggle != null)
            {
                _exclusiveFindToggle.onValueChanged.RemoveListener(OnExclusiveFindToggleChanged);
            }

            _exclusiveWalkToggle = walkToggle;
            _exclusiveFindToggle = findToggle;

            if (_exclusiveWalkToggle != null)
            {
                _exclusiveWalkToggle.onValueChanged.AddListener(OnExclusiveWalkToggleChanged);
            }

            if (_exclusiveFindToggle != null)
            {
                _exclusiveFindToggle.onValueChanged.AddListener(OnExclusiveFindToggleChanged);
            }
        }

        private void OnExclusiveWalkToggleChanged(bool isOn)
        {
            if (_isEnforcingModeToggles)
            {
                return;
            }

            if (isOn || _exclusiveFindToggle == null || !_exclusiveFindToggle.isOn)
            {
                SetExclusiveMode(true);
            }
        }

        private void OnExclusiveFindToggleChanged(bool isOn)
        {
            if (_isEnforcingModeToggles)
            {
                return;
            }

            if (isOn)
            {
                SetExclusiveMode(false);
            }
            else if (_exclusiveWalkToggle == null || !_exclusiveWalkToggle.isOn)
            {
                SetExclusiveMode(true);
            }
        }

        private void SetExclusiveMode(bool useWalkMode)
        {
            _isEnforcingModeToggles = true;
            if (_exclusiveWalkToggle != null)
            {
                _exclusiveWalkToggle.SetIsOnWithoutNotify(useWalkMode);
            }

            if (_exclusiveFindToggle != null)
            {
                _exclusiveFindToggle.SetIsOnWithoutNotify(!useWalkMode);
            }

            _isEnforcingModeToggles = false;
        }

        private void ClearModeToggleExclusivity()
        {
            if (_exclusiveWalkToggle != null)
            {
                _exclusiveWalkToggle.onValueChanged.RemoveListener(OnExclusiveWalkToggleChanged);
            }

            if (_exclusiveFindToggle != null)
            {
                _exclusiveFindToggle.onValueChanged.RemoveListener(OnExclusiveFindToggleChanged);
            }

            _exclusiveWalkToggle = null;
            _exclusiveFindToggle = null;
        }

        private void RenderBoard(MainVm viewModel)
        {
            if (_bindings?.BoardContainer == null)
            {
                return;
            }

            _lastBoardRows = viewModel.Rows;
            _lastBoardCols = viewModel.Cols;
            _lastMinCellSpacing = viewModel.MinCellSpacing >= 0f
                ? viewModel.MinCellSpacing
                : BoardFrameLayoutCalculator.DefaultMinimumSpacing;
            UpdateBoardFrameSprites(viewModel.BoardBackgroundPath, viewModel.BoardFrameOverlayPath);

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

            BoardFrameLayout frameLayout = ApplyCurrentBoardFrameLayout(
                viewModel.Rows,
                viewModel.Cols,
                viewModel.MinCellSpacing);
            RectTransform activeContainer = viewModel.UseTutorialBoardLayer && _bindings.TutorialBoardContainer != null
                ? _bindings.TutorialBoardContainer
                : _bindings.BoardContainer;
            FindCatBoardView boardView = activeContainer.GetComponent<FindCatBoardView>();
            if (boardView == null)
            {
                throw new InvalidOperationException($"{activeContainer.name} 缺少 FindCatBoardView，请在 MainPanel prefab 静态挂载。");
            }

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
            boardView.Render(viewModel.Rows, viewModel.Cols, viewModel.Cells, viewModel.CatVisuals, frameLayout, _currentCellAction);
        }

        private void UpdateBoardFrameSprites(string boardBackgroundPath, string boardFrameOverlayPath)
        {
            if (_bindings?.MinesBgImage == null)
            {
                return;
            }

            string normalizedBackgroundPath = NormalizeAssetPath(boardBackgroundPath);
            if (string.IsNullOrEmpty(normalizedBackgroundPath))
            {
                if (IsDefaultBoardFrameSpritesApplied())
                {
                    return;
                }

                _boardBackgroundRequestGeneration++;
                RestoreDefaultBoardFrameSprites();
                return;
            }

            string normalizedOverlayPath = NormalizeAssetPath(boardFrameOverlayPath);
            if (string.IsNullOrEmpty(normalizedOverlayPath))
            {
                normalizedOverlayPath = normalizedBackgroundPath;
            }

            if (string.Equals(_activeBoardBackgroundPath, normalizedBackgroundPath, StringComparison.Ordinal) &&
                string.Equals(_activeBoardFrameOverlayPath, normalizedOverlayPath, StringComparison.Ordinal) &&
                _bindings.MinesBgImage.sprite != null &&
                (_bindings.MinesBgFrameOverlayImage == null || _bindings.MinesBgFrameOverlayImage.sprite != null))
            {
                return;
            }

            if (string.Equals(_requestedBoardBackgroundPath, normalizedBackgroundPath, StringComparison.Ordinal) &&
                string.Equals(_requestedBoardFrameOverlayPath, normalizedOverlayPath, StringComparison.Ordinal))
            {
                return;
            }

            int generation = ++_boardBackgroundRequestGeneration;
            _requestedBoardBackgroundPath = normalizedBackgroundPath;
            _requestedBoardFrameOverlayPath = normalizedOverlayPath;
            _ = LoadAndApplyBoardFrameSpritesAsync(normalizedBackgroundPath, normalizedOverlayPath, generation);
        }

        private async System.Threading.Tasks.Task LoadAndApplyBoardFrameSpritesAsync(
            string boardBackgroundPath,
            string boardFrameOverlayPath,
            int generation)
        {
            IAssetHandle backgroundHandle = null;
            IAssetHandle overlayHandle = null;
            try
            {
                IAssetsRuntime assetsRuntime = _assetsRuntime;
                if (assetsRuntime == null || string.IsNullOrWhiteSpace(boardBackgroundPath))
                {
                    if (generation == _boardBackgroundRequestGeneration &&
                        string.Equals(_requestedBoardBackgroundPath, boardBackgroundPath, StringComparison.Ordinal) &&
                        string.Equals(_requestedBoardFrameOverlayPath, boardFrameOverlayPath, StringComparison.Ordinal))
                    {
                        RestoreDefaultBoardFrameSprites();
                    }

                    return;
                }

                bool overlayReusesBackground = string.Equals(boardBackgroundPath, boardFrameOverlayPath, StringComparison.Ordinal);
                backgroundHandle = await assetsRuntime.LoadAssetAsync(boardBackgroundPath);
                overlayHandle = overlayReusesBackground
                    ? backgroundHandle
                    : await assetsRuntime.LoadAssetAsync(boardFrameOverlayPath);
                if (generation != _boardBackgroundRequestGeneration ||
                    !string.Equals(_requestedBoardBackgroundPath, boardBackgroundPath, StringComparison.Ordinal) ||
                    !string.Equals(_requestedBoardFrameOverlayPath, boardFrameOverlayPath, StringComparison.Ordinal) ||
                    _bindings?.MinesBgImage == null)
                {
                    ReleaseLoadedBoardFrameHandles(backgroundHandle, overlayHandle);
                    return;
                }

                Sprite backgroundSprite = ExtractSprite(backgroundHandle != null ? backgroundHandle.AssetObject : null);
                Sprite overlaySprite = ExtractSprite(overlayHandle != null ? overlayHandle.AssetObject : null);
                if (backgroundSprite == null || overlaySprite == null)
                {
                    ReleaseLoadedBoardFrameHandles(backgroundHandle, overlayHandle);
                    RestoreDefaultBoardFrameSprites();
                    Debug.LogWarning("MainView: 棋盘背景或边框资源不是 Sprite，已回退 prefab 默认背景 " + boardBackgroundPath, this);
                    return;
                }

                ReleaseBoardFrameHandles();
                _boardBackgroundHandle = backgroundHandle;
                _boardFrameOverlayHandle = overlayReusesBackground ? null : overlayHandle;
                _activeBoardBackgroundPath = boardBackgroundPath;
                _activeBoardFrameOverlayPath = boardFrameOverlayPath;
                _bindings.MinesBgImage.sprite = backgroundSprite;
                if (_bindings.MinesBgFrameOverlayImage != null)
                {
                    _bindings.MinesBgFrameOverlayImage.sprite = overlaySprite;
                }

                ApplyCurrentBoardFrameLayout(_lastBoardRows, _lastBoardCols, _lastMinCellSpacing);
            }
            catch (Exception exception)
            {
                ReleaseLoadedBoardFrameHandles(backgroundHandle, overlayHandle);
                if (generation == _boardBackgroundRequestGeneration &&
                    string.Equals(_requestedBoardBackgroundPath, boardBackgroundPath, StringComparison.Ordinal) &&
                    string.Equals(_requestedBoardFrameOverlayPath, boardFrameOverlayPath, StringComparison.Ordinal))
                {
                    RestoreDefaultBoardFrameSprites();
                }

                Debug.LogWarning("MainView: 棋盘背景或边框加载失败 " + boardBackgroundPath + "，" + exception.Message, this);
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath;
        }

        private static Sprite ExtractSprite(Object assetObject)
        {
            if (assetObject is Sprite sprite)
            {
                return sprite;
            }

            return null;
        }

        private static void ConfigureBoardFrameOverlayImage(Image overlayImage)
        {
            if (overlayImage == null)
            {
                return;
            }

            overlayImage.type = Image.Type.Sliced;
            overlayImage.fillCenter = false;
            overlayImage.raycastTarget = false;
        }

        private void RestoreDefaultBoardFrameSprites()
        {
            _requestedBoardBackgroundPath = string.Empty;
            _requestedBoardFrameOverlayPath = string.Empty;
            _activeBoardBackgroundPath = string.Empty;
            _activeBoardFrameOverlayPath = string.Empty;
            if (_bindings?.MinesBgImage != null)
            {
                _bindings.MinesBgImage.sprite = _defaultBoardBackgroundSprite;
            }

            if (_bindings?.MinesBgFrameOverlayImage != null)
            {
                _bindings.MinesBgFrameOverlayImage.sprite = _defaultBoardFrameOverlaySprite;
            }

            ReleaseBoardFrameHandles();
            ApplyCurrentBoardFrameLayout(_lastBoardRows, _lastBoardCols, _lastMinCellSpacing);
        }

        private bool IsDefaultBoardFrameSpritesApplied()
        {
            bool pathsAreDefault =
                string.IsNullOrEmpty(_requestedBoardBackgroundPath) &&
                string.IsNullOrEmpty(_requestedBoardFrameOverlayPath) &&
                string.IsNullOrEmpty(_activeBoardBackgroundPath) &&
                string.IsNullOrEmpty(_activeBoardFrameOverlayPath);
            bool handlesAreDefault = _boardBackgroundHandle == null && _boardFrameOverlayHandle == null;
            bool backgroundIsDefault = _bindings?.MinesBgImage == null ||
                                       ReferenceEquals(_bindings.MinesBgImage.sprite, _defaultBoardBackgroundSprite);
            bool overlayIsDefault = _bindings?.MinesBgFrameOverlayImage == null ||
                                    ReferenceEquals(_bindings.MinesBgFrameOverlayImage.sprite, _defaultBoardFrameOverlaySprite);
            return pathsAreDefault && handlesAreDefault && backgroundIsDefault && overlayIsDefault;
        }

        private BoardFrameLayout ApplyCurrentBoardFrameLayout(
            int rows,
            int cols,
            float minCellSpacing)
        {
            RectTransform frameRect = _bindings?.MinesBgImage != null
                ? _bindings.MinesBgImage.rectTransform
                : _bindings?.MinesGroup;
            RectTransform layoutSourceRect = _bindings?.BoardContentRect != null
                ? _bindings.BoardContentRect
                : frameRect;
            if (layoutSourceRect == null)
            {
                return default;
            }

            _lastAppliedBoardFrameSize = layoutSourceRect.rect.size;

            if (_bindings?.MinesGroup != null && _bindings.MinesBgImage != null && _bindings.MinesGroup.parent == _bindings.MinesBgImage.transform)
            {
                Stretch(_bindings.MinesGroup);
            }

            Vector2 containerOffsetMin = Vector2.zero;
            Vector2 containerOffsetMax = Vector2.zero;
            if (_bindings?.BoardContentRect != null && frameRect != null)
            {
                RectTransform containerParent = _bindings.BoardContainer != null
                    ? _bindings.BoardContainer.parent as RectTransform
                    : _bindings.MinesGroup;
                CalculateStretchOffsets(_bindings.BoardContentRect, containerParent, out containerOffsetMin, out containerOffsetMax);
            }
            _lastAppliedBoardContentOffsetMin = containerOffsetMin;
            _lastAppliedBoardContentOffsetMax = containerOffsetMax;

            BoardFrameLayout layout = BoardFrameLayoutCalculator.Calculate(
                layoutSourceRect.rect.size,
                rows,
                cols,
                minCellSpacing >= 0f ? minCellSpacing : BoardFrameLayoutCalculator.DefaultMinimumSpacing);
            if (_bindings?.BoardContentRect != null)
            {
                layout = new BoardFrameLayout(
                    containerOffsetMin,
                    containerOffsetMax,
                    layout.CellSize,
                    layout.Spacing,
                    layout.ContentSize);
            }
            ApplyBoardContainerLayout(_bindings?.BoardContainer, layout);
            ApplyBoardContainerLayout(_bindings?.TutorialBoardContainer, layout);

            if (_bindings?.MinesBgMask != null)
            {
                _bindings.MinesBgMask.padding = Vector4.zero;
            }

            return layout;
        }

        private static void CalculateStretchOffsets(
            RectTransform targetRect,
            RectTransform parentRect,
            out Vector2 offsetMin,
            out Vector2 offsetMax)
        {
            offsetMin = Vector2.zero;
            offsetMax = Vector2.zero;
            if (targetRect == null || parentRect == null)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);
            Vector2 localBottomLeft = parentRect.InverseTransformPoint(corners[0]);
            Vector2 localTopRight = parentRect.InverseTransformPoint(corners[2]);
            offsetMin = localBottomLeft - parentRect.rect.min;
            offsetMax = localTopRight - parentRect.rect.max;
        }

        private static void ApplyBoardContainerLayout(RectTransform container, BoardFrameLayout layout)
        {
            if (container == null || !layout.IsValid)
            {
                return;
            }

            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.pivot = new Vector2(0.5f, 0.5f);
            container.offsetMin = layout.ContainerOffsetMin;
            container.offsetMax = layout.ContainerOffsetMax;
            container.localScale = Vector3.one;
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

        private void OnRectTransformDimensionsChange()
        {
            BoardFrameLayout layout = ApplyCurrentBoardFrameLayout(_lastBoardRows, _lastBoardCols, _lastMinCellSpacing);
            ApplyFrameLayoutToBoardViews(layout);
        }

        private void LateUpdate()
        {
            RefreshBoardFrameLayoutIfFrameSizeChanged();
        }

        private void OnDestroy()
        {
            ClearModeToggleExclusivity();
            _boardBackgroundRequestGeneration++;
            ReleaseBoardFrameHandles();
            _catSpriteLoader?.Dispose();
            _catSpriteLoader = null;
        }

        private void ReleaseBoardFrameHandles()
        {
            _boardBackgroundHandle?.Release();
            _boardFrameOverlayHandle?.Release();
            _boardBackgroundHandle = null;
            _boardFrameOverlayHandle = null;
        }

        private static void ReleaseLoadedBoardFrameHandles(IAssetHandle backgroundHandle, IAssetHandle overlayHandle)
        {
            backgroundHandle?.Release();
            if (!ReferenceEquals(backgroundHandle, overlayHandle))
            {
                overlayHandle?.Release();
            }
        }

        private void ApplyFrameLayoutToBoardViews(BoardFrameLayout layout)
        {
            if (!layout.IsValid)
            {
                return;
            }

            _boardView?.ApplyFrameLayout(_lastBoardRows, _lastBoardCols, layout);
            _tutorialBoardView?.ApplyFrameLayout(_lastBoardRows, _lastBoardCols, layout);
        }

        private void RefreshBoardFrameLayoutIfFrameSizeChanged()
        {
            RectTransform frameRect = _bindings?.MinesBgImage != null
                ? _bindings.MinesBgImage.rectTransform
                : _bindings?.MinesGroup;
            RectTransform layoutSourceRect = _bindings?.BoardContentRect != null
                ? _bindings.BoardContentRect
                : frameRect;
            if (layoutSourceRect == null)
            {
                return;
            }

            Vector2 frameSize = layoutSourceRect.rect.size;
            Vector2 contentOffsetMin = Vector2.zero;
            Vector2 contentOffsetMax = Vector2.zero;
            if (_bindings?.BoardContentRect != null)
            {
                RectTransform containerParent = _bindings.BoardContainer != null
                    ? _bindings.BoardContainer.parent as RectTransform
                    : _bindings.MinesGroup;
                CalculateStretchOffsets(_bindings.BoardContentRect, containerParent, out contentOffsetMin, out contentOffsetMax);
            }

            if (Approximately(frameSize, _lastAppliedBoardFrameSize) &&
                Approximately(contentOffsetMin, _lastAppliedBoardContentOffsetMin) &&
                Approximately(contentOffsetMax, _lastAppliedBoardContentOffsetMax))
            {
                return;
            }

            BoardFrameLayout layout = ApplyCurrentBoardFrameLayout(_lastBoardRows, _lastBoardCols, _lastMinCellSpacing);
            ApplyFrameLayoutToBoardViews(layout);
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) <= 0.001f &&
                   Mathf.Abs(left.y - right.y) <= 0.001f;
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

        #if UNITY_EDITOR
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
            Transform existingTransform = parent != null ? parent.Find(objectName) : null;
            bool createdButtonObject = existingTransform == null;
            GameObject buttonObject = createdButtonObject ? new GameObject(objectName) : existingTransform.gameObject;
            if (createdButtonObject)
            {
                buttonObject.transform.SetParent(parent, false);
            }

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = buttonObject.AddComponent<RectTransform>();
            }

            ConfigureRect(rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

            Image image = buttonObject.GetComponent<Image>();
            bool createdImage = image == null;
            if (image == null)
            {
                image = buttonObject.AddComponent<Image>();
            }

            if (createdButtonObject || createdImage)
            {
                image.color = color;
            }

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }
            DisableButtonGraphicTint(button);

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
        #endif

        private static void DisableButtonGraphicTint(Button button)
        {
            DisableSelectableGraphicTint(button);
        }

        private static void DisableSelectableGraphicTint(Selectable selectable)
        {
            if (selectable == null)
            {
                return;
            }

            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = null;
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

        #if UNITY_EDITOR
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
        #endif

        private static void SetTmpText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            RuntimeTmpFontResolver.EnsureFontSupportsText(text, value);
            text.text = value ?? string.Empty;
        }

        private static void SetTaskSlotProgressText(MainTaskSlotView slotView, string value)
        {
            if (slotView == null)
            {
                return;
            }

            if (slotView.ProgressLabel != null)
            {
                SetTmpText(slotView.ProgressLabel, value);
                return;
            }

            if (slotView.ProgressLegacyLabel != null)
            {
                slotView.ProgressLegacyLabel.text = value ?? string.Empty;
            }
        }

        #if UNITY_EDITOR
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
        #endif

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        #if UNITY_EDITOR
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
        #endif

        private sealed class MainTaskSlotView
        {
            public int SlotIndex;
            public RectTransform Root;
            public Image Background;
            public Button Button;
            public TMP_Text TitleLabel;
            public TMP_Text ProgressLabel;
            public Text ProgressLegacyLabel;
            public TMP_Text RewardLabel;
            public Slider ProgressSlider;
            public Image RewardIcon;
            public Image CatIcon;
            public GameObject LockObject;
            public TMP_Text[] AllTextLabels;
            public Slider[] AllSliders;
        }
    }
}
