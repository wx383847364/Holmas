using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleView : MonoBehaviour
    {
        private static readonly Color StageLabelTextColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color StageLabelSelectedTextColor = new Color(0.58f, 0.34f, 0.03f, 1f);
        private static readonly Color StageLabelBackgroundColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color StageLabelSelectedBackgroundColor = new Color(1f, 0.92f, 0.58f, 0.94f);
        private readonly UnityAction[] _stageClickActions = new UnityAction[BattlePresenter.VisibleStageCount];
        private readonly UnityAction[] _buildStageClickActions = new UnityAction[BattlePresenter.VisibleStageCount];
        private BattleBindings _bindings;
        private UnityAction _currentBackAction;
        private UnityAction _currentBuildAction;
        private Action<int> _currentStageAction;
        private Action<int> _currentPromotionSlotAction;
        private HolmasCatSpriteLoader _stageSpriteLoader;

        public void EnsureBindingSurface()
        {
            gameObject.name = BattleBindings.RootNodePath;

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
            collector.RegisterOrReplace(BattleBindings.RootPanelKey, rootRect, nodePath: BattleBindings.RootNodePath);

            RectTransform overlay = GetOrCreateOverlayRoot();
            Button backButton = ResolveBackButton(overlay);
            Button buildButton = ResolveBuildButton(overlay);
            ConfigureBuildContainer(buildButton);
            HideLegacyBuildCardChildren(buildButton != null ? buildButton.transform : null);
            TextMeshProUGUI buildButtonText = ResolveButtonLabel(buildButton, "城市建设");
            TextMeshProUGUI levelText = ResolveLevelText(overlay);
            TextMeshProUGUI goldText = ResolveGoldText(overlay);
            TextMeshProUGUI energyText = ResolveEnergyText(overlay);
            TextMeshProUGUI summaryText = GetOrCreateRuntimeText(
                overlay,
                "SummaryText",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -112f),
                new Vector2(-260f, 112f),
                24f,
                TextAlignmentOptions.Center);
            TextMeshProUGUI statusText = GetOrCreateRuntimeText(
                overlay,
                "StatusText",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 60f),
                new Vector2(-200f, 100f),
                26f,
                TextAlignmentOptions.Center);

            collector.RegisterOrReplace(BattleBindings.BackButtonKey, backButton, BattleBindings.ButtonClickEvent, BattleBindings.BackButtonNodePath);
            collector.RegisterOrReplace(BattleBindings.BuildButtonKey, buildButton, BattleBindings.ButtonClickEvent, BattleBindings.BuildButtonNodePath);
            collector.RegisterOrReplace(BattleBindings.BuildButtonTextKey, buildButtonText, nodePath: BattleBindings.BuildButtonTextNodePath);
            collector.RegisterOrReplace(BattleBindings.LevelTextKey, levelText, nodePath: BattleBindings.LevelTextNodePath);
            collector.RegisterOrReplace(BattleBindings.GoldTextKey, goldText, nodePath: BattleBindings.GoldTextNodePath);
            collector.RegisterOrReplace(BattleBindings.EnergyTextKey, energyText, nodePath: BattleBindings.EnergyTextNodePath);
            collector.RegisterOrReplace(BattleBindings.SummaryTextKey, summaryText, nodePath: BattleBindings.SummaryTextNodePath);
            collector.RegisterOrReplace(BattleBindings.StatusTextKey, statusText, nodePath: BattleBindings.StatusTextNodePath);

            for (int i = 0; i < BattlePresenter.VisibleStageCount; i++)
            {
                StageSurface surface = ResolveStageSurface(i, overlay);
                collector.RegisterOrReplace(BattleBindings.StageButtonKeys[i], surface.Button, BattleBindings.ButtonClickEvent, BattleBindings.StageButtonNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.StageImageKeys[i], surface.Image, nodePath: BattleBindings.StageImageNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.StageNameTextKeys[i], surface.NameText, nodePath: BattleBindings.StageNameTextNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.StageLockKeys[i], surface.LockRect, nodePath: BattleBindings.StageLockNodePaths[i]);

                BuildStageSurface buildSurface = ResolveBuildStageSurface(i, buildButton != null ? buildButton.transform : overlay);
                collector.RegisterOrReplace(BattleBindings.BuildStageButtonKeys[i], buildSurface.Button, BattleBindings.ButtonClickEvent, BattleBindings.BuildStageButtonNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.BuildStageImageKeys[i], buildSurface.Image, nodePath: BattleBindings.BuildStageImageNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.BuildStageNameTextKeys[i], buildSurface.NameText, nodePath: BattleBindings.BuildStageNameTextNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.BuildStageLockKeys[i], buildSurface.LockRect, nodePath: BattleBindings.BuildStageLockNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.BuildStageBaseStarGroupKeys[i], buildSurface.BaseStarGroup, nodePath: BattleBindings.BuildStageBaseStarGroupNodePaths[i]);
                collector.RegisterOrReplace(BattleBindings.BuildStageActiveStarGroupKeys[i], buildSurface.ActiveStarGroup, nodePath: BattleBindings.BuildStageActiveStarGroupNodePaths[i]);
            }

            for (int i = 0; i < BattlePresenter.VisibleStageBarCount; i++)
            {
                Slider stageBar = ResolveStageBar(i, overlay);
                collector.RegisterOrReplace(BattleBindings.StageBarKeys[i], stageBar, nodePath: BattleBindings.StageBarNodePaths[i]);
            }
        }

        public void Bind(BattleBindings bindings)
        {
            _bindings = bindings ?? new BattleBindings();
        }

        public void SetBackAction(UnityAction action)
        {
            if (_bindings?.BackButton == null)
            {
                _currentBackAction = action;
                return;
            }

            if (_currentBackAction != null)
            {
                _bindings.BackButton.onClick.RemoveListener(_currentBackAction);
            }

            _currentBackAction = action;
            if (_currentBackAction != null)
            {
                _bindings.BackButton.onClick.AddListener(_currentBackAction);
            }
        }

        public void SetBuildAction(UnityAction action)
        {
            if (_bindings?.BuildButton == null)
            {
                _currentBuildAction = action;
                return;
            }

            if (_currentBuildAction != null)
            {
                _bindings.BuildButton.onClick.RemoveListener(_currentBuildAction);
            }

            _currentBuildAction = action;
            if (_currentBuildAction != null)
            {
                _bindings.BuildButton.onClick.AddListener(_currentBuildAction);
            }
        }

        public void SetStageAction(Action<int> action)
        {
            _currentStageAction = action;
            RebindStageActions();
        }

        public void SetPromotionSlotAction(Action<int> action)
        {
            _currentPromotionSlotAction = action;
            RebindBuildStageActions();
        }

        public void SetAssetsRuntime(App.Shared.Contracts.IAssetsRuntime assetsRuntime)
        {
            if (_stageSpriteLoader == null)
            {
                _stageSpriteLoader = new HolmasCatSpriteLoader(assetsRuntime);
            }
            else
            {
                _stageSpriteLoader.SetAssetsRuntime(assetsRuntime);
            }
        }

        public void Render(BattleVm viewModel)
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

            if (_bindings?.SummaryText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.SummaryText, viewModel.Summary);
            }

            if (_bindings?.StatusText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.StatusText, viewModel.Status);
            }

            if (_bindings?.BuildButton != null)
            {
                _bindings.BuildButton.interactable = true;
            }

            if (_bindings?.BuildButtonText != null)
            {
                TmpGlyphCoverageReporter.SetText(_bindings.BuildButtonText, viewModel.BuildButtonLabel);
                ConfigureBuildButtonText(_bindings.BuildButtonText, viewModel.BuildButtonEnabled);
            }

            RenderBuildCard(viewModel);
            RenderPromotionSlots(viewModel);
            RenderStages(viewModel);
            RenderStageBars(viewModel);
        }

        private void RenderBuildCard(BattleVm viewModel)
        {
            if (_bindings?.BuildButton == null)
            {
                return;
            }

            HideLegacyBuildCardChildren(_bindings.BuildButton.transform);
        }

        private void RenderPromotionSlots(BattleVm viewModel)
        {
            if (_bindings == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.BuildStageButtons.Length; i++)
            {
                BattlePromotionSlotVm promotionSlot = viewModel.PromotionSlots != null && i < viewModel.PromotionSlots.Length
                    ? viewModel.PromotionSlots[i]
                    : null;
                bool visible = promotionSlot != null && promotionSlot.Visible;
                Button button = _bindings.BuildStageButtons[i];
                if (button != null)
                {
                    button.gameObject.SetActive(visible);
                    button.interactable = visible;
                }

                if (!visible)
                {
                    if (_bindings.BuildStageImages[i] != null)
                    {
                        _stageSpriteLoader?.Clear(_bindings.BuildStageImages[i]);
                    }

                    continue;
                }

                Image image = _bindings.BuildStageImages[i];
                if (image != null)
                {
                    var visual = new HolmasCatVisualVm
                    {
                        CatId = $"promotion-{promotionSlot.AgencyStageId}-{promotionSlot.PromotionId}",
                        CatName = promotionSlot.PromotionId ?? string.Empty,
                        IconPath = promotionSlot.StageImage ?? string.Empty,
                    };
                    _stageSpriteLoader?.Bind(image, visual, !promotionSlot.Unlocked);
                    image.color = GetPromotionSlotImageTint(promotionSlot);
                }

                if (_bindings.BuildStageNameTexts[i] != null)
                {
                    string label = $"{promotionSlot.PromotionId}\n{promotionSlot.ProgressLabel}";
                    TmpGlyphCoverageReporter.SetText(_bindings.BuildStageNameTexts[i], label);
                    ConfigureBuildStageLabel(_bindings.BuildStageNameTexts[i], promotionSlot.Current && promotionSlot.CanBuild);
                }

                if (_bindings.BuildStageLocks[i] != null)
                {
                    _bindings.BuildStageLocks[i].gameObject.SetActive(!promotionSlot.Unlocked);
                }

                if (_bindings.BuildStageBaseStarGroups[i] != null)
                {
                    _bindings.BuildStageBaseStarGroups[i].gameObject.SetActive(true);
                    RenderStarGroup(_bindings.BuildStageBaseStarGroups[i], promotionSlot.StarCap, promotionSlot.StarCap, new Color(0.46f, 0.48f, 0.52f, 0.72f), keepInvisibleSlots: false);
                }

                if (_bindings.BuildStageActiveStarGroups[i] != null)
                {
                    _bindings.BuildStageActiveStarGroups[i].gameObject.SetActive(true);
                    _bindings.BuildStageActiveStarGroups[i].SetAsLastSibling();
                    RenderStarGroup(_bindings.BuildStageActiveStarGroups[i], promotionSlot.StarCap, promotionSlot.StarCount, Color.white, keepInvisibleSlots: true);
                }
            }
        }

        private void RenderStages(BattleVm viewModel)
        {
            if (_bindings == null || viewModel.Stages == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.StageButtons.Length; i++)
            {
                BattleStageVm stage = i < viewModel.Stages.Length ? viewModel.Stages[i] : null;
                bool visible = stage != null && stage.Visible;
                Button button = _bindings.StageButtons[i];
                if (button != null)
                {
                    button.gameObject.SetActive(visible);
                    button.interactable = visible;
                }

                if (!visible)
                {
                    if (_bindings.StageImages[i] != null)
                    {
                        _stageSpriteLoader?.Clear(_bindings.StageImages[i]);
                    }

                    continue;
                }

                Image image = _bindings.StageImages[i];
                if (image != null)
                {
                    if (string.IsNullOrWhiteSpace(stage.StageImage))
                    {
                        Debug.LogWarning($"BattleView: Stage {stage.AgencyStageId} 缺少 stageImage，已显示缺图状态。");
                    }

                    var visual = new HolmasCatVisualVm
                    {
                        CatId = $"stage-{stage.AgencyStageId}",
                        CatName = stage.StageName ?? string.Empty,
                        IconPath = stage.StageImage ?? string.Empty,
                    };
                    _stageSpriteLoader?.Bind(image, visual, !stage.Unlocked);
                    image.color = GetStageImageTint(stage);
                }

                if (_bindings.StageNameTexts[i] != null)
                {
                    string label = $"{stage.StageName}\n{stage.ProgressLabel}";
                    TmpGlyphCoverageReporter.SetText(_bindings.StageNameTexts[i], label);
                    ConfigureStageLabel(_bindings.StageNameTexts[i], stage.Selected);
                }

                if (_bindings.StageLocks[i] != null)
                {
                    _bindings.StageLocks[i].gameObject.SetActive(!stage.Unlocked);
                }
            }
        }

        private void RenderStageBars(BattleVm viewModel)
        {
            if (_bindings == null || viewModel.StageBars == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.StageBars.Length; i++)
            {
                BattleStageBarVm barVm = i < viewModel.StageBars.Length ? viewModel.StageBars[i] : null;
                Slider bar = _bindings.StageBars[i];
                if (bar == null)
                {
                    continue;
                }

                bool visible = barVm != null && barVm.Visible;
                bar.gameObject.SetActive(visible);
                bar.minValue = 0f;
                bar.maxValue = 1f;
                bar.wholeNumbers = false;
                bar.value = visible ? Mathf.Clamp01(barVm.Progress) : 0f;
                bar.interactable = false;
            }
        }

        private void RebindStageActions()
        {
            if (_bindings == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.StageButtons.Length; i++)
            {
                Button button = _bindings.StageButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (_stageClickActions[i] != null)
                {
                    button.onClick.RemoveListener(_stageClickActions[i]);
                    _stageClickActions[i] = null;
                }

                int stageSlotIndex = i;
                _stageClickActions[i] = () => _currentStageAction?.Invoke(stageSlotIndex);
                button.onClick.AddListener(_stageClickActions[i]);
            }
        }

        private void RebindBuildStageActions()
        {
            if (_bindings == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.BuildStageButtons.Length; i++)
            {
                Button button = _bindings.BuildStageButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (_buildStageClickActions[i] != null)
                {
                    button.onClick.RemoveListener(_buildStageClickActions[i]);
                    _buildStageClickActions[i] = null;
                }

                int stageSlotIndex = i;
                _buildStageClickActions[i] = () => _currentPromotionSlotAction?.Invoke(stageSlotIndex);
                button.onClick.AddListener(_buildStageClickActions[i]);
            }
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

            background.color = new Color(0.1f, 0.22f, 0.42f, 0.96f);
        }

        private RectTransform GetOrCreateOverlayRoot()
        {
            Transform existing = transform.Find(BattleBindings.RuntimeOverlayNodeName);
            GameObject overlayObject = existing != null ? existing.gameObject : new GameObject(BattleBindings.RuntimeOverlayNodeName, typeof(RectTransform));
            overlayObject.transform.SetParent(transform, false);

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect);
            return overlayRect;
        }

        private Button ResolveBackButton(Transform overlay)
        {
            Button existing = FindDescendantComponent<Button>("Back_btn");
            return existing ?? GetOrCreateRuntimeButton(
                overlay,
                "Back_btn",
                "返回",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -52f),
                new Vector2(180f, 84f),
                new Color(0.22f, 0.24f, 0.31f, 0.9f));
        }

        private Button ResolveBuildButton(Transform overlay)
        {
            Button existing = FindDescendantComponent<Button>("Build_btn");
            return existing ?? GetOrCreateRuntimeButton(
                overlay,
                "Build_btn",
                "城市建设",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 80f),
                new Vector2(300f, 96f),
                new Color(0.92f, 0.48f, 0.18f, 0.95f));
        }

        private Image ResolveBuildIconImage(Transform buildButtonRoot)
        {
            if (buildButtonRoot == null)
            {
                return null;
            }

            Transform iconTransform = buildButtonRoot.Find("Image");
            Image image = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if (image == null)
            {
                return null;
            }

            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private BuildStageSurface ResolveBuildStageSurface(int slotIndex, Transform buildButtonRoot)
        {
            string objectName = "BuildStage" + (slotIndex + 1);
            Transform slotTransform = buildButtonRoot != null ? buildButtonRoot.Find(objectName) : null;
            GameObject slotObject = slotTransform != null
                ? slotTransform.gameObject
                : GetOrCreateChild(buildButtonRoot, objectName);

            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            if (slotRect == null)
            {
                slotRect = slotObject.AddComponent<RectTransform>();
            }

            float anchorX = (slotIndex + 0.5f) / BattlePresenter.VisibleStageCount;
            ConfigureRect(
                slotRect,
                new Vector2(anchorX, 0.5f),
                new Vector2(anchorX, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(124f, 190f));

            Image buttonImage = slotObject.GetComponent<Image>();
            if (buttonImage == null)
            {
                buttonImage = slotObject.AddComponent<Image>();
            }

            buttonImage.color = new Color(1f, 1f, 1f, 0f);
            buttonImage.raycastTarget = true;

            Button button = slotObject.GetComponent<Button>();
            if (button == null)
            {
                button = slotObject.AddComponent<Button>();
            }

            button.targetGraphic = buttonImage;

            Image icon = ResolveBuildStageIcon(slotObject.transform);
            TextMeshProUGUI label = ResolveBuildStageLabel(slotObject.transform);
            RectTransform lockRect = ResolveBuildStageLock(slotObject.transform);
            RectTransform baseStars = ResolveBuildStageStarGroup(slotObject.transform, "stargroup", -20f, ResolveLegacyStarSprite(buildButtonRoot, "stargroup"));
            RectTransform activeStars = ResolveBuildStageStarGroup(slotObject.transform, "stargroup_1", -20f, ResolveLegacyStarSprite(buildButtonRoot, "stargroup_1"));

            return new BuildStageSurface
            {
                Button = button,
                Image = icon,
                NameText = label,
                LockRect = lockRect,
                BaseStarGroup = baseStars,
                ActiveStarGroup = activeStars,
            };
        }

        private static void ConfigureBuildContainer(Button buildButton)
        {
            if (buildButton == null)
            {
                return;
            }

            RectTransform rectTransform = buildButton.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                ConfigureRect(
                    rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 18f),
                    new Vector2(-64f, 220f));
            }

            Image image = buildButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
            }
        }

        private static void HideLegacyBuildCardChildren(Transform buildButtonRoot)
        {
            if (buildButtonRoot == null)
            {
                return;
            }

            HideDirectChild(buildButtonRoot, "Image");
            HideDirectChild(buildButtonRoot, "stargroup");
            HideDirectChild(buildButtonRoot, "stargroup_1");
            HideDirectChild(buildButtonRoot, "Money_image");
            HideDirectChild(buildButtonRoot, "CastCounts");
        }

        private static void HideDirectChild(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private Image ResolveBuildStageIcon(Transform slotRoot)
        {
            Transform iconTransform = slotRoot.Find("Image");
            GameObject iconObject = iconTransform != null
                ? iconTransform.gameObject
                : GetOrCreateChild(slotRoot, "Image");
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            if (iconRect == null)
            {
                iconRect = iconObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                iconRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 5f),
                new Vector2(114f, 114f));

            Image image = iconObject.GetComponent<Image>();
            if (image == null)
            {
                image = iconObject.AddComponent<Image>();
            }

            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI ResolveBuildStageLabel(Transform slotRoot)
        {
            Transform labelTransform = slotRoot.Find("Text (TMP)");
            if (labelTransform != null && labelTransform.TryGetComponent(out TextMeshProUGUI existing))
            {
                return existing;
            }

            TextMeshProUGUI label = GetOrCreateRuntimeText(
                slotRoot,
                "Text (TMP)",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0f),
                new Vector2(116f, 42f),
                14f,
                TextAlignmentOptions.Center);
            return label;
        }

        private RectTransform ResolveBuildStageLock(Transform slotRoot)
        {
            Transform lockTransform = slotRoot.Find("lock");
            GameObject lockObject = lockTransform != null
                ? lockTransform.gameObject
                : GetOrCreateChild(slotRoot, "lock");
            RectTransform lockRect = lockObject.GetComponent<RectTransform>();
            if (lockRect == null)
            {
                lockRect = lockObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                lockRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 5f),
                new Vector2(34f, 47f));

            Image image = lockObject.GetComponent<Image>();
            if (image == null)
            {
                image = lockObject.AddComponent<Image>();
            }

            image.sprite = image.sprite != null ? image.sprite : ResolveStageLockSprite();
            image.raycastTarget = false;
            return lockRect;
        }

        private RectTransform ResolveBuildStageStarGroup(Transform slotRoot, string groupName, float y, Sprite starSprite)
        {
            Transform groupTransform = slotRoot.Find(groupName);
            GameObject groupObject = groupTransform != null
                ? groupTransform.gameObject
                : GetOrCreateChild(slotRoot, groupName);
            RectTransform groupRect = groupObject.GetComponent<RectTransform>();
            if (groupRect == null)
            {
                groupRect = groupObject.AddComponent<RectTransform>();
            }

            ConfigureRect(
                groupRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, y),
                new Vector2(90f, 22f));

            HorizontalLayoutGroup layoutGroup = groupObject.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = groupObject.AddComponent<HorizontalLayoutGroup>();
            }

            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.spacing = 0f;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            for (int i = 0; i < 5; i++)
            {
                string starName = i == 0 ? "star" : $"star ({i})";
                GameObject starObject = GetOrCreateChild(groupObject.transform, starName);
                RectTransform starRect = starObject.GetComponent<RectTransform>();
                if (starRect == null)
                {
                    starRect = starObject.AddComponent<RectTransform>();
                }

                ConfigureRect(
                    starRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(18f, 18f));

                Image image = starObject.GetComponent<Image>();
                if (image == null)
                {
                    image = starObject.AddComponent<Image>();
                }

                image.sprite = image.sprite != null ? image.sprite : starSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            return groupRect;
        }

        private Sprite ResolveLegacyStarSprite(Transform buildButtonRoot, string groupName)
        {
            Transform starTransform = buildButtonRoot != null ? buildButtonRoot.Find(groupName + "/star") : null;
            Image image = starTransform != null ? starTransform.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        private Sprite ResolveStageLockSprite()
        {
            Transform lockTransform = transform.Find("Map_bg/Stage1/Image/lock");
            Image image = lockTransform != null ? lockTransform.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        private TextMeshProUGUI ResolveButtonLabel(Button button, string fallbackLabel)
        {
            Transform textTransform = button != null ? button.transform.Find("Text (TMP)") : null;
            TextMeshProUGUI existing = textTransform != null ? textTransform.GetComponent<TextMeshProUGUI>() : null;
            if (existing != null)
            {
                return existing;
            }

            return GetOrCreateRuntimeText(
                button.transform,
                "Text (TMP)",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                28f,
                TextAlignmentOptions.Center);
        }

        private TextMeshProUGUI ResolveLevelText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("Headicon_btn/Level");
            return existing ?? GetOrCreateRuntimeText(
                overlay,
                "LevelText",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(250f, -54f),
                new Vector2(180f, 72f),
                28f,
                TextAlignmentOptions.Left);
        }

        private TextMeshProUGUI ResolveGoldText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("Money_btn/Text (TMP)");
            return existing ?? GetOrCreateRuntimeText(
                overlay,
                "GoldText",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-48f, -54f),
                new Vector2(220f, 72f),
                28f,
                TextAlignmentOptions.Right);
        }

        private TextMeshProUGUI ResolveEnergyText(Transform overlay)
        {
            TextMeshProUGUI existing = FindDescendantComponent<TextMeshProUGUI>("Energy_btn/Text (TMP)");
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
                new Vector2(-64f, -96f),
                new Vector2(220f, 64f),
                28f,
                TextAlignmentOptions.Right);
        }

        private StageSurface ResolveStageSurface(int slotIndex, Transform overlay)
        {
            string objectName = "Stage" + (slotIndex + 1);
            string stagePath = "Map_bg/" + objectName;
            Button button = FindDescendantComponent<Button>(stagePath);
            if (button == null)
            {
                button = GetOrCreateRuntimeButton(
                    overlay,
                    objectName,
                    string.Empty,
                    new Vector2(0.18f + slotIndex * 0.16f, 0.42f),
                    new Vector2(0.18f + slotIndex * 0.16f, 0.42f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(130f, 150f),
                    new Color(0.28f, 0.54f, 0.67f, 0.96f));
            }

            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            image.preserveAspect = true;

            TextMeshProUGUI nameText = ResolveStageNameText(button.transform);
            if (nameText == null)
            {
                Image labelBackground = ResolveStageLabelBackground(button.transform);
                nameText = GetOrCreateRuntimeText(
                    labelBackground.transform,
                    "Text (TMP)",
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0f),
                    Vector2.zero,
                    Vector2.zero,
                    18f,
                    TextAlignmentOptions.Center);
            }

            ConfigureStageLabel(nameText, false);

            RectTransform lockRect = ResolveStageLockRect(button.transform);
            if (lockRect == null)
            {
                GameObject lockObject = GetOrCreateChild(button.transform, "lock");
                Image lockImage = lockObject.GetComponent<Image>();
                if (lockImage == null)
                {
                    lockImage = lockObject.AddComponent<Image>();
                }

                lockImage.color = new Color(0f, 0f, 0f, 0.46f);
                lockRect = lockObject.GetComponent<RectTransform>();
                if (lockRect == null)
                {
                    lockRect = lockObject.AddComponent<RectTransform>();
                }

                ConfigureRect(lockRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }

            return new StageSurface
            {
                Button = button,
                Image = image,
                NameText = nameText,
                LockRect = lockRect,
            };
        }

        private static RectTransform ResolveStageLockRect(Transform stageRoot)
        {
            if (stageRoot == null)
            {
                return null;
            }

            Transform lockTransform =
                stageRoot.Find("Image/lock") ??
                stageRoot.Find("Image/Lock") ??
                stageRoot.Find("lock") ??
                stageRoot.Find("Lock");
            if (lockTransform == null)
            {
                return null;
            }

            RectTransform lockRect = lockTransform as RectTransform;
            if (lockRect == null)
            {
                lockRect = lockTransform.gameObject.AddComponent<RectTransform>();
            }

            return lockRect;
        }

        private Slider ResolveStageBar(int slotIndex, Transform overlay)
        {
            string objectName = "StageBar" + (slotIndex + 1);
            Transform existingTransform = transform.Find("Map_bg/" + objectName);
            if (existingTransform != null)
            {
                Slider existingSlider = existingTransform.GetComponent<Slider>();
                if (existingSlider != null)
                {
                    return existingSlider;
                }
            }

            Transform mapRoot = transform.Find("Map_bg");
            Transform parent = mapRoot != null ? mapRoot : overlay;
            GameObject barObject = GetOrCreateChild(parent, objectName);
            RectTransform rectTransform = barObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = barObject.AddComponent<RectTransform>();
                ConfigureRect(
                    rectTransform,
                    new Vector2(0.26f + slotIndex * 0.16f, 0.42f),
                    new Vector2(0.26f + slotIndex * 0.16f, 0.42f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(100f, 16f));
            }

            Slider slider = barObject.GetComponent<Slider>();
            if (slider == null)
            {
                slider = barObject.AddComponent<Slider>();
            }

            return slider;
        }

        private TextMeshProUGUI ResolveStageNameText(Transform stageRoot)
        {
            Transform labelText = stageRoot.Find("Image/Text (TMP)");
            TextMeshProUGUI text;
            if (labelText != null && labelText.TryGetComponent(out text))
            {
                return text;
            }

            labelText = stageRoot.Find("Text (TMP)");
            if (labelText != null && labelText.TryGetComponent(out text))
            {
                return text;
            }

            return null;
        }

        private Image ResolveStageLabelBackground(Transform stageRoot)
        {
            Transform labelRoot = stageRoot.Find("Image");
            GameObject labelObject = labelRoot != null
                ? labelRoot.gameObject
                : GetOrCreateChild(stageRoot, "Image");

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            if (labelRect == null)
            {
                labelRect = labelObject.AddComponent<RectTransform>();
            }

            if (labelRoot == null)
            {
                ConfigureRect(
                    labelRect,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -64f),
                    new Vector2(176f, 48f));
            }

            Image labelBackground = labelObject.GetComponent<Image>();
            if (labelBackground == null)
            {
                labelBackground = labelObject.AddComponent<Image>();
            }

            return labelBackground;
        }

        private static void ConfigureStageLabel(TextMeshProUGUI text, bool selected)
        {
            if (text == null)
            {
                return;
            }

            text.color = selected ? StageLabelSelectedTextColor : StageLabelTextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(text.fontSizeMin <= 0f ? 14f : text.fontSizeMin, 14f);
            text.fontSizeMax = Mathf.Max(text.fontSizeMax, 22f);
            text.raycastTarget = false;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            textRect.localScale = Vector3.one;
            text.transform.SetAsLastSibling();

            Image labelBackground = text.transform.parent != null
                ? text.transform.parent.GetComponent<Image>()
                : null;
            if (labelBackground == null)
            {
                return;
            }

            labelBackground.color = selected ? StageLabelSelectedBackgroundColor : StageLabelBackgroundColor;
            labelBackground.raycastTarget = false;

            RectTransform labelRect = labelBackground.rectTransform;
            Vector2 sizeDelta = labelRect.sizeDelta;
            sizeDelta.x = Mathf.Max(sizeDelta.x, 176f);
            sizeDelta.y = Mathf.Max(sizeDelta.y, 48f);
            labelRect.sizeDelta = sizeDelta;
        }

        private static void ConfigureBuildStageLabel(TextMeshProUGUI text, bool selected)
        {
            if (text == null)
            {
                return;
            }

            text.color = selected ? StageLabelSelectedTextColor : Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 15f;
            text.raycastTarget = false;
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

        private static void ConfigureBuildButtonText(TextMeshProUGUI text, bool enabled)
        {
            if (text == null)
            {
                return;
            }

            text.color = enabled ? Color.white : new Color(0.86f, 0.88f, 0.92f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 15f;
            text.fontSizeMax = 22f;
            text.raycastTarget = false;

            RectTransform rectTransform = text.rectTransform;
            if (rectTransform.parent != null && rectTransform.parent.name == "Build_btn")
            {
                ConfigureRect(
                    rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 4f),
                    new Vector2(-32f, 34f));
                text.alignment = TextAlignmentOptions.Center;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 16f;
            }
        }

        private static void RenderBuildStars(Transform buildButtonRoot, int starCount)
        {
            if (buildButtonRoot == null)
            {
                return;
            }

            Transform baseGroup = buildButtonRoot.Find("stargroup");
            Transform activeGroup = buildButtonRoot.Find("stargroup_1");
            if (baseGroup == null && activeGroup == null)
            {
                return;
            }

            if (baseGroup != null)
            {
                baseGroup.gameObject.SetActive(true);
                RenderStarGroup(baseGroup, 5, 5, new Color(0.46f, 0.48f, 0.52f, 0.72f), keepInvisibleSlots: false);
            }

            if (activeGroup == null)
            {
                return;
            }

            activeGroup.gameObject.SetActive(true);
            activeGroup.SetAsLastSibling();

            Image[] activeStars = ResolveStars(activeGroup);
            int visibleCount = Mathf.Clamp(starCount, 0, Math.Min(5, activeStars.Length));
            RenderStarGroup(activeGroup, 5, visibleCount, Color.white, keepInvisibleSlots: true);
        }

        private static void RenderStarGroup(Transform starGroup, int slotCount, int visibleCount, Color visibleColor, bool keepInvisibleSlots)
        {
            EnsureStarPool(starGroup, slotCount);
            Image[] stars = ResolveStars(starGroup);
            int clampedVisibleCount = Mathf.Clamp(visibleCount, 0, Math.Max(0, slotCount));
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null)
                {
                    continue;
                }

                bool used = i < slotCount;
                stars[i].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                stars[i].raycastTarget = false;
                stars[i].color = i < clampedVisibleCount
                    ? visibleColor
                    : new Color(visibleColor.r, visibleColor.g, visibleColor.b, keepInvisibleSlots ? 0f : visibleColor.a);
            }
        }

        private static void EnsureStarPool(Transform starGroup, int slotCount)
        {
            if (starGroup == null || slotCount <= 0)
            {
                return;
            }

            Image[] existingStars = ResolveStars(starGroup);
            Sprite sprite = existingStars.Length > 0 && existingStars[0] != null ? existingStars[0].sprite : null;
            for (int i = existingStars.Length; i < slotCount; i++)
            {
                string starName = i == 0 ? "star" : $"star ({i})";
                GameObject starObject = GetOrCreateChild(starGroup, starName);
                RectTransform starRect = starObject.GetComponent<RectTransform>();
                if (starRect == null)
                {
                    starRect = starObject.AddComponent<RectTransform>();
                }

                ConfigureRect(
                    starRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(18f, 18f));

                Image image = starObject.GetComponent<Image>();
                if (image == null)
                {
                    image = starObject.AddComponent<Image>();
                }

                image.sprite = image.sprite != null ? image.sprite : sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
        }

        private static Image[] ResolveStars(Transform starGroup)
        {
            if (starGroup == null)
            {
                return Array.Empty<Image>();
            }

            var stars = new List<Image>();
            for (int i = 0; i < starGroup.childCount; i++)
            {
                Transform starTransform = starGroup.GetChild(i);
                if (starTransform == null || !starTransform.name.StartsWith("star", StringComparison.Ordinal))
                {
                    continue;
                }

                Image image = starTransform.GetComponent<Image>();
                if (image != null)
                {
                    stars.Add(image);
                }
            }

            return stars.ToArray();
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
                "Text (TMP)",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                30f,
                TextAlignmentOptions.Center);
            TmpGlyphCoverageReporter.SetText(labelText, label);
            return button;
        }

        private void OnDestroy()
        {
            _stageSpriteLoader?.Dispose();
            _stageSpriteLoader = null;
        }

        private T FindDescendantComponent<T>(string path) where T : Component
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private GameObject FindDescendantGameObject(string path)
        {
            Transform target = transform.Find(path);
            return target != null ? target.gameObject : null;
        }

        private static Color GetStageImageTint(BattleStageVm stage)
        {
            if (stage == null || !stage.Unlocked)
            {
                return new Color(0.55f, 0.55f, 0.55f, 0.58f);
            }

            if (stage.Selected)
            {
                return new Color(1f, 0.95f, 0.68f, 1f);
            }

            return Color.white;
        }

        private static Color GetPromotionSlotImageTint(BattlePromotionSlotVm stage)
        {
            if (stage == null || !stage.Unlocked)
            {
                return new Color(0.55f, 0.55f, 0.55f, 0.58f);
            }

            if (stage.CanBuild)
            {
                return new Color(1f, 0.95f, 0.68f, 1f);
            }

            return Color.white;
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

        private struct StageSurface
        {
            public Button Button;
            public Image Image;
            public TextMeshProUGUI NameText;
            public RectTransform LockRect;
        }

        private struct BuildStageSurface
        {
            public Button Button;
            public Image Image;
            public TextMeshProUGUI NameText;
            public RectTransform LockRect;
            public RectTransform BaseStarGroup;
            public RectTransform ActiveStarGroup;
        }
    }
}
