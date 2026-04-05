using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Services;
using App.Shared.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI
{
    public sealed class HolmasUiRoot : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float CellSize = 42f;
        private const int OfflineSettlementHours = 2;

        private HolmasApplicationContext _context;
        private IHolmasLevelLaunchGateway _levelLaunchGateway;
        private IHolmasAgencyCatalog _agencyCatalog;
        private HolmasUiPresenter _presenter;

        private Text _summaryText;
        private Text _taskSummaryText;
        private Text _promotionSummaryText;
        private Text _boardSummaryText;
        private Text _statusText;
        private GridLayoutGroup _boardGrid;
        private RectTransform _taskListRoot;
        private readonly List<Text> _taskTexts = new List<Text>();
        private readonly List<Button> _taskButtons = new List<Button>();
        private readonly List<Text> _taskButtonTexts = new List<Text>();
        private readonly Dictionary<int, Button> _boardButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, Text> _boardButtonTexts = new Dictionary<int, Text>();
        private Button _startLevelButton;
        private Button _upgradePromotionButton;
        private bool _built;
        private bool _busy;

        public void Initialize(HolmasApplicationContext context, IHolmasLevelLaunchGateway levelLaunchGateway)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _levelLaunchGateway = levelLaunchGateway ?? throw new ArgumentNullException(nameof(levelLaunchGateway));
            _agencyCatalog = _context.ServiceContainer?.Get<IHolmasAgencyCatalog>();
            _presenter = new HolmasUiPresenter(_context, _agencyCatalog);

            if (!_built)
            {
                BuildUi();
                _built = true;
            }

            if (_context.GameplayRuntime != null && _context.GameplayRuntime.TaskBarState.Tasks.Count == 0)
            {
                _context.RefillAvailableTasks();
            }

            RefreshAll("Holmas UI ready");
        }

        private void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            gameObject.AddComponent<GraphicRaycaster>();

            var background = CreateImage(transform, "Background", new Color(0.07f, 0.09f, 0.12f, 0.88f));
            Stretch(background.rectTransform);

            var content = CreatePanel(background.transform, "Content", new Color(0.12f, 0.14f, 0.18f, 0.94f));
            content.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            content.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            content.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            content.rectTransform.sizeDelta = new Vector2(1420f, 820f);

            var rootLayout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootLayout.spacing = 18f;
            rootLayout.padding = new RectOffset(18, 18, 18, 18);
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = true;

            var leftPanel = CreatePanel(content.transform, "AgencyPanel", new Color(0.16f, 0.18f, 0.23f, 1f));
            var leftLayout = leftPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            leftLayout.spacing = 10f;
            leftLayout.padding = new RectOffset(14, 14, 14, 14);
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = false;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;
            var leftSize = leftPanel.gameObject.AddComponent<LayoutElement>();
            leftSize.preferredWidth = PanelWidth;
            leftSize.flexibleHeight = 1f;

            CreateText(leftPanel.transform, "Title", "Holmas Agency", 28, FontStyle.Bold, TextAnchor.MiddleLeft);
            _summaryText = CreateText(leftPanel.transform, "Summary", string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            _taskSummaryText = CreateText(leftPanel.transform, "TaskSummary", string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            _promotionSummaryText = CreateText(leftPanel.transform, "PromotionSummary", string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft);
            _promotionSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _promotionSummaryText.verticalOverflow = VerticalWrapMode.Overflow;

            var commandRow = CreatePanel(leftPanel.transform, "CommandRow", new Color(0.10f, 0.12f, 0.16f, 0.75f));
            var commandLayout = commandRow.gameObject.AddComponent<VerticalLayoutGroup>();
            commandLayout.spacing = 8f;
            commandLayout.padding = new RectOffset(10, 10, 10, 10);
            commandLayout.childControlWidth = true;
            commandLayout.childControlHeight = true;
            commandLayout.childForceExpandWidth = true;
            commandLayout.childForceExpandHeight = false;

            _startLevelButton = CreateButton(commandRow.transform, "StartLevelButton", "开图");
            _startLevelButton.onClick.AddListener(() => _ = StartLevelAsync());

            Button refillButton = CreateButton(commandRow.transform, "RefillButton", "补任务");
            refillButton.onClick.AddListener(OnRefillTasksClicked);

            Button offlineButton = CreateButton(commandRow.transform, "OfflineButton", "离线结算 2h");
            offlineButton.onClick.AddListener(OnOfflineSettlementClicked);

            _upgradePromotionButton = CreateButton(commandRow.transform, "UpgradeButton", "升级首个宣传");
            _upgradePromotionButton.onClick.AddListener(OnUpgradePromotionClicked);

            Button refreshButton = CreateButton(commandRow.transform, "RefreshButton", "刷新状态");
            refreshButton.onClick.AddListener(() => RefreshAll("状态已刷新"));

            var taskPanel = CreatePanel(leftPanel.transform, "TaskPanel", new Color(0.10f, 0.12f, 0.16f, 0.75f));
            var taskLayout = taskPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            taskLayout.spacing = 6f;
            taskLayout.padding = new RectOffset(10, 10, 10, 10);
            taskLayout.childControlWidth = true;
            taskLayout.childControlHeight = false;
            taskLayout.childForceExpandWidth = true;
            taskLayout.childForceExpandHeight = false;

            CreateText(taskPanel.transform, "TaskTitle", "任务栏", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            _taskListRoot = CreateRect(taskPanel.transform, "TaskList");
            var taskListLayout = _taskListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            taskListLayout.spacing = 6f;
            taskListLayout.childControlWidth = true;
            taskListLayout.childControlHeight = true;
            taskListLayout.childForceExpandWidth = true;
            taskListLayout.childForceExpandHeight = false;

            for (int i = 0; i < 5; i++)
            {
                RectTransform taskRow = CreateRect(_taskListRoot, $"TaskRow{i}");
                var rowLayout = taskRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 8f;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;

                Text taskText = CreateText(taskRow, $"TaskText{i}", string.Empty, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
                var taskTextLayout = taskText.gameObject.AddComponent<LayoutElement>();
                taskTextLayout.preferredWidth = 250f;
                taskTextLayout.flexibleWidth = 1f;
                Button taskButton = CreateButton(taskRow, $"TaskButton{i}", "...");
                int slotIndex = i;
                taskButton.onClick.AddListener(() => OnTaskSlotActionClicked(slotIndex));
                _taskTexts.Add(taskText);
                _taskButtons.Add(taskButton);
                _taskButtonTexts.Add(taskButton.GetComponentInChildren<Text>());
            }

            _statusText = CreateText(leftPanel.transform, "Status", "等待操作", 15, FontStyle.Italic, TextAnchor.MiddleLeft);
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;

            var boardPanel = CreatePanel(content.transform, "BoardPanel", new Color(0.16f, 0.18f, 0.23f, 1f));
            var boardPanelLayout = boardPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            boardPanelLayout.spacing = 10f;
            boardPanelLayout.padding = new RectOffset(14, 14, 14, 14);
            boardPanelLayout.childControlWidth = true;
            boardPanelLayout.childControlHeight = false;
            boardPanelLayout.childForceExpandWidth = true;
            boardPanelLayout.childForceExpandHeight = false;
            var boardSize = boardPanel.gameObject.AddComponent<LayoutElement>();
            boardSize.flexibleWidth = 1f;
            boardSize.flexibleHeight = 1f;

            CreateText(boardPanel.transform, "BoardTitle", "Find-Cat Board", 28, FontStyle.Bold, TextAnchor.MiddleLeft);
            _boardSummaryText = CreateText(boardPanel.transform, "BoardSummary", string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleLeft);

            RectTransform boardGridRoot = CreateRect(boardPanel.transform, "BoardGrid");
            var boardGridLayoutElement = boardGridRoot.gameObject.AddComponent<LayoutElement>();
            boardGridLayoutElement.preferredHeight = 620f;
            boardGridLayoutElement.flexibleHeight = 1f;
            _boardGrid = boardGridRoot.gameObject.AddComponent<GridLayoutGroup>();
            _boardGrid.cellSize = new Vector2(CellSize, CellSize);
            _boardGrid.spacing = new Vector2(4f, 4f);
            _boardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _boardGrid.constraintCount = 6;
            _boardGrid.childAlignment = TextAnchor.UpperLeft;
        }

        private async Task StartLevelAsync()
        {
            if (_busy || _levelLaunchGateway == null)
            {
                return;
            }

            _busy = true;
            SetActionButtonsInteractable(false);

            try
            {
                int seed = Environment.TickCount;
                await _levelLaunchGateway.StartLevelForCurrentPlayerAsync(seed);
                RefreshAll($"已开图，seed={seed}");
            }
            catch (Exception ex)
            {
                RefreshAll($"开图失败: {ex.Message}");
            }
            finally
            {
                _busy = false;
                SetActionButtonsInteractable(true);
            }
        }

        private void OnRefillTasksClicked()
        {
            try
            {
                HolmasTaskRefillResult result = _context.RefillAvailableTasks();
                RefreshAll($"补任务完成，生成 {result.GeneratedTasks.Count} 条");
            }
            catch (Exception ex)
            {
                RefreshAll($"补任务失败: {ex.Message}");
            }
        }

        private void OnOfflineSettlementClicked()
        {
            try
            {
                long offlineMilliseconds = OfflineSettlementHours * 60L * 60L * 1000L;
                var result = _context.ApplyOfflineSettlement(offlineMilliseconds);
                RefreshAll($"离线结算完成，金币 +{result.OfflineRewardGained}");
            }
            catch (Exception ex)
            {
                RefreshAll($"离线结算失败: {ex.Message}");
            }
        }

        private void OnUpgradePromotionClicked()
        {
            string promotionId = _presenter.GetFirstActionablePromotionId();
            if (string.IsNullOrWhiteSpace(promotionId))
            {
                RefreshAll("当前阶段没有可升级宣传");
                return;
            }

            try
            {
                HolmasAgencyUpgradeResult result = _context.TryUpgradePromotion(promotionId);
                if (!result.Success)
                {
                    RefreshAll($"升级失败: {result.FailureReason}");
                    return;
                }

                RefreshAll($"宣传 {result.PromotionId} -> {result.NewLevel}，金币 -{result.GoldSpent}");
            }
            catch (Exception ex)
            {
                RefreshAll($"升级失败: {ex.Message}");
            }
        }

        private void OnTaskSlotActionClicked(int slotIndex)
        {
            if (_context?.GameplayRuntime?.TaskBarState == null)
            {
                return;
            }

            var slot = _context.GameplayRuntime.TaskBarState.GetSlot(slotIndex);
            var task = _context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex);

            try
            {
                if (slot != null && !slot.IsUnlocked)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var unlockResult = _context.UnlockAdSlot(slotIndex, now);
                    if (!unlockResult.Success)
                    {
                        RefreshAll($"槽位 {slotIndex + 1} 解锁失败: {unlockResult.FailureReason}");
                        return;
                    }

                    _context.RefillAvailableTasks();
                    RefreshAll($"槽位 {slotIndex + 1} 已解锁");
                    return;
                }

                if (task != null && task.CanClaimReward)
                {
                    var claimResult = _context.ClaimTaskReward(slotIndex);
                    if (!claimResult.Success)
                    {
                        RefreshAll($"领取失败: {claimResult.FailureReason}");
                        return;
                    }

                    _context.RefillAvailableTasks();
                    RefreshAll($"槽位 {slotIndex + 1} 已领奖，金币 +{claimResult.Reward}");
                    return;
                }

                _context.RefillAvailableTasks();
                RefreshAll($"槽位 {slotIndex + 1} 已尝试补任务");
            }
            catch (Exception ex)
            {
                RefreshAll($"槽位 {slotIndex + 1} 操作失败: {ex.Message}");
            }
        }

        private void OnBoardCellClicked(int cellIndex)
        {
            if (_context?.GameplayRuntime?.CurrentBoardRuntime == null)
            {
                RefreshAll("当前没有运行中的棋盘");
                return;
            }

            try
            {
                BoardRevealResult revealResult = _context.GameplayRuntime.RevealCell(cellIndex, out var progressionResult);
                if (!revealResult.IsValidAction)
                {
                    RefreshAll($"格子 {cellIndex} 无效");
                    return;
                }

                if (revealResult.Completed)
                {
                    int progressedTasks = progressionResult != null ? progressionResult.ProgressedTaskIds.Count : 0;
                    RefreshAll($"地图完成，推进任务 {progressedTasks} 条");
                    return;
                }

                if (revealResult.FoundCat)
                {
                    RefreshAll($"发现猫，格子 {cellIndex}");
                    return;
                }

                RefreshAll($"已揭示格子 {cellIndex}");
            }
            catch (Exception ex)
            {
                RefreshAll($"翻格失败: {ex.Message}");
            }
        }

        private void RefreshAll(string status)
        {
            if (_presenter == null)
            {
                return;
            }

            HolmasUiScreenViewModel viewModel = _presenter.Build();
            _summaryText.text = viewModel.SummaryText;
            _taskSummaryText.text = viewModel.TaskSummaryText;
            _promotionSummaryText.text = viewModel.PromotionSummaryText;
            _boardSummaryText.text = viewModel.BoardSummaryText;
            _statusText.text = status;
            RenderTaskSlots(viewModel.TaskSlots);
            RenderBoard();
            _upgradePromotionButton.interactable = !string.IsNullOrWhiteSpace(_presenter.GetFirstActionablePromotionId());
        }

        private void RenderTaskSlots(IReadOnlyList<HolmasUiTaskSlotViewModel> taskSlots)
        {
            for (int i = 0; i < _taskTexts.Count; i++)
            {
                HolmasUiTaskSlotViewModel slot = taskSlots != null && i < taskSlots.Count ? taskSlots[i] : null;
                _taskTexts[i].text = slot != null ? slot.Title : $"槽位 {i + 1}: -";
                _taskButtons[i].interactable = slot != null && slot.ActionEnabled;
                _taskButtonTexts[i].text = slot != null ? slot.ActionLabel : "不可用";
            }
        }

        private void RenderBoard()
        {
            BoardRuntime boardRuntime = _context?.GameplayRuntime?.CurrentBoardRuntime;
            if (boardRuntime == null)
            {
                ClearBoard();
                return;
            }

            int rows = Math.Max(1, boardRuntime.Rows);
            int cols = Math.Max(1, boardRuntime.Cols);
            if (_boardButtons.Count != boardRuntime.CellCount)
            {
                RebuildBoardCells(boardRuntime.CellCount);
            }

            _boardGrid.constraintCount = cols;
            IReadOnlyList<BoardCellState> states = boardRuntime.GetAllCellStates();
            for (int i = 0; i < boardRuntime.CellCount; i++)
            {
                BoardCellState state = states[i];
                Button button = _boardButtons[i];
                Text label = _boardButtonTexts[i];
                Image image = button.GetComponent<Image>();
                ConfigureBoardCell(state, button, image, label);
            }
        }

        private void ClearBoard()
        {
            if (_boardButtons.Count == 0)
            {
                return;
            }

            foreach (var pair in _boardButtons)
            {
                pair.Value.gameObject.SetActive(false);
            }
        }

        private void RebuildBoardCells(int cellCount)
        {
            foreach (Transform child in _boardGrid.transform)
            {
                Destroy(child.gameObject);
            }

            _boardButtons.Clear();
            _boardButtonTexts.Clear();

            for (int i = 0; i < cellCount; i++)
            {
                Button button = CreateButton(_boardGrid.transform, $"Cell{i}", string.Empty);
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(CellSize, CellSize);
                int cellIndex = i;
                button.onClick.AddListener(() => OnBoardCellClicked(cellIndex));
                _boardButtons[i] = button;
                _boardButtonTexts[i] = button.GetComponentInChildren<Text>();
            }
        }

        private static void ConfigureBoardCell(BoardCellState state, Button button, Image image, Text label)
        {
            button.gameObject.SetActive(true);
            button.interactable = state.IsValid && !state.IsRevealed;

            if (!state.IsValid)
            {
                image.color = new Color(0f, 0f, 0f, 0.15f);
                label.text = string.Empty;
                return;
            }

            if (!state.IsRevealed)
            {
                image.color = new Color(state.BlockColor.r / 255f, state.BlockColor.g / 255f, state.BlockColor.b / 255f, 1f);
                label.text = string.Empty;
                return;
            }

            if (state.HasCat)
            {
                image.color = new Color(0.93f, 0.78f, 0.25f, 1f);
                label.text = "猫";
                return;
            }

            image.color = new Color(0.30f, 0.37f, 0.46f, 1f);
            label.text = state.AdjacentCatCount > 0 ? state.AdjacentCatCount.ToString() : string.Empty;
        }

        private void SetActionButtonsInteractable(bool interactable)
        {
            if (_startLevelButton != null)
            {
                _startLevelButton.interactable = interactable;
            }
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            return CreateImage(parent, name, color);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.localScale = Vector3.one;
            var image = gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.localScale = Vector3.one;
            var text = textObject.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            Image image = CreateImage(parent, name, new Color(0.24f, 0.42f, 0.62f, 1f));
            RectTransform rectTransform = image.rectTransform;
            rectTransform.sizeDelta = new Vector2(180f, 42f);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(image.transform, "Label", label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.localScale = Vector3.one;
            return rectTransform;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
