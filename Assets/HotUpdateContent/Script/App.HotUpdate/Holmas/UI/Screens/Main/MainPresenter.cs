using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.UI.Core;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.UI.Screens.Main
{
    public sealed class MainPresenter
    {
        private readonly HolmasApplicationContext _context;
        private readonly CoreFindCatTutorialSessionService _tutorialSessionService;

        public MainPresenter(HolmasApplicationContext context, CoreFindCatTutorialSessionService tutorialSessionService = null)
        {
            _context = context;
            _tutorialSessionService = tutorialSessionService;
        }

        public MainVm Build(string status = null)
        {
            CoreFindCatTutorialSessionService tutorialSessionService = _tutorialSessionService ?? (_context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<CoreFindCatTutorialSessionService>()
                : null);
            CoreFindCatTutorialSession tutorialSession = tutorialSessionService?.ActiveSession;
            BoardRuntime board = tutorialSession != null
                ? tutorialSession.BoardRuntime
                : _context?.GameplayRuntime?.CurrentBoardRuntime;
            bool isTutorialSessionActive = tutorialSession != null;
            var visualResolver = CreateCatVisualResolver();
            IReadOnlyList<BoardCellState> cells = board != null ? board.GetAllCellStates() : new BoardCellState[0];
            MainTaskItemVm[] taskItems = BuildTaskItems(visualResolver, disableInteraction: isTutorialSessionActive);
            HolmasAgencyBuildingDefinition promotion = GetPrimaryPromotionDefinition();
            string promotionId = promotion != null ? promotion.PromotionId : string.Empty;
            BoardFrameConfig boardFrameConfig = ResolveBoardFrameConfig(board, isTutorialSessionActive);
            return new MainVm
            {
                LevelLabel = $"Lv {_context?.CurrentPlayerLevel ?? 1}",
                GoldLabel = $"Gold {_context?.CurrentGoldBalance ?? 0L}",
                EnergyLabel = _context?.EnergyLabel ?? "50/50",
                Summary = BuildSummary(),
                Status = string.IsNullOrWhiteSpace(status) ? "主界面已就绪。" : status,
                PromotionButtonLabel = "城市宣传",
                PromotionButtonEnabled = !isTutorialSessionActive,
                PromotionId = promotionId ?? string.Empty,
                AddEnergyButtonLabel = "+5体力",
                AddEnergyButtonEnabled = !isTutorialSessionActive && _context != null && _context.GameplayRuntime != null,
                BoardVisible = board != null,
                UseTutorialBoardLayer = isTutorialSessionActive,
                Rows = board != null ? board.Rows : 0,
                Cols = board != null ? board.Cols : 0,
                BoardBackgroundPath = boardFrameConfig.BoardBackgroundPath,
                BoardFrameOverlayPath = boardFrameConfig.BoardFrameOverlayPath,
                BoardContentInset = boardFrameConfig.BoardContentInset,
                MinCellSpacing = boardFrameConfig.MinCellSpacing,
                Cells = cells,
                CatVisuals = BuildCatVisualLookup(cells, taskItems, visualResolver),
                TaskItems = taskItems,
            };
        }

        private BoardFrameConfig ResolveBoardFrameConfig(BoardRuntime board, bool isTutorialSessionActive)
        {
            if (board == null || isTutorialSessionActive)
            {
                return BoardFrameConfig.Default;
            }

            string mapId = board.Snapshot != null ? board.Snapshot.MapId : string.Empty;
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return BoardFrameConfig.Default;
            }

            IHolmasMapCatalog catalog = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasMapCatalog>()
                : null;
            if (catalog == null ||
                !catalog.TryGetMap(mapId, out HolmasMapDefinition definition) ||
                definition == null)
            {
                return BoardFrameConfig.Default;
            }

            return new BoardFrameConfig
            {
                BoardBackgroundPath = definition.BoardBackgroundPath ?? string.Empty,
                BoardFrameOverlayPath = definition.BoardFrameOverlayPath ?? string.Empty,
                BoardContentInset = definition.BoardContentInset,
                MinCellSpacing = definition.MinCellSpacing >= 0f ? definition.MinCellSpacing : 4f,
            };
        }

        public string GetPrimaryPromotionId()
        {
            return GetPrimaryPromotionDefinition()?.PromotionId ?? string.Empty;
        }

        private HolmasAgencyBuildingDefinition GetPrimaryPromotionDefinition()
        {
            IHolmasAgencyCatalog catalog = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasAgencyCatalog>()
                : null;
            if (catalog == null)
            {
                return null;
            }

            var definitions = catalog.GetPromotionsForStage(_context.CurrentAgencyStageId);
            if (definitions == null || definitions.Count == 0)
            {
                return null;
            }

            var currentState = _context.GameplayRuntime?.MetaProgressionState;
            HolmasAgencyBuildingDefinition preferred = definitions
                .FirstOrDefault(item => item != null &&
                                        !string.IsNullOrWhiteSpace(item.PromotionId) &&
                                        currentState != null &&
                                        HolmasAgencyPromotionStateKey.GetLevel(currentState, item.AgencyStageId, item.PromotionId) < item.PromotionLevelCap);

            if (preferred != null)
            {
                return preferred;
            }

            return currentState == null ? definitions[0] : null;
        }

        private bool HasConfiguredPromotionsForCurrentStage()
        {
            IHolmasAgencyCatalog catalog = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasAgencyCatalog>()
                : null;
            if (catalog == null)
            {
                return false;
            }

            var definitions = catalog.GetPromotionsForStage(_context.CurrentAgencyStageId);
            return definitions != null && definitions.Any(item => item != null && !string.IsNullOrWhiteSpace(item.PromotionId));
        }

        private string BuildSummary()
        {
            if (_context == null || _context.GameplayRuntime == null)
            {
                return "Holmas gameplay runtime unavailable.";
            }

            HolmasTaskBarState taskBar = _context.GameplayRuntime.TaskBarState;
            int unlockedCount = taskBar != null && taskBar.Slots != null
                ? taskBar.Slots.Count(item => item != null && item.IsUnlocked)
                : 0;
            int activeTaskCount = taskBar != null && taskBar.Tasks != null
                ? taskBar.Tasks.Count(item => item != null && item.Task != null)
                : 0;
            int pendingRelockCount = taskBar != null ? taskBar.GetPendingRelockSlotCount() : 0;
            string boardSummary = _context.GameplayRuntime.CurrentBoardRuntime == null
                ? "未进入棋盘"
                : BuildBoardSummary(_context.GameplayRuntime.CurrentBoardRuntime);

            return $"{BuildProgressionSummary()} | 任务 {activeTaskCount}/{unlockedCount} | 待锁 {pendingRelockCount}\n{BuildPromotionSummary()} | {boardSummary}";
        }

        private string BuildBoardSummary(BoardRuntime board)
        {
            if (board == null)
            {
                return "未进入棋盘";
            }

            string mapId = _context?.GameplayRuntime?.CurrentLevelSnapshot != null
                ? _context.GameplayRuntime.CurrentLevelSnapshot.MapId
                : "unknown";
            string terrainName = _context?.GameplayRuntime?.CurrentLevelSnapshot != null
                ? GetTerrainFileName(_context.GameplayRuntime.CurrentLevelSnapshot.TerrainPath)
                : "unknown";
            int hiddenCatCount = System.Math.Max(0, board.TotalCatCount - board.FoundCatCount);
            return $"Map {mapId} | Terrain {terrainName} | Board {board.Rows}x{board.Cols} | Board Cats {board.FoundCatCount}/{board.TotalCatCount} | Hidden {hiddenCatCount} | {BuildTaskProgressSummary()}";
        }

        private static string GetTerrainFileName(string terrainPath)
        {
            if (string.IsNullOrWhiteSpace(terrainPath))
            {
                return "unknown";
            }

            int slashIndex = terrainPath.LastIndexOf('/');
            int backslashIndex = terrainPath.LastIndexOf('\\');
            int index = System.Math.Max(slashIndex, backslashIndex);
            return index >= 0 && index + 1 < terrainPath.Length
                ? terrainPath.Substring(index + 1)
                : terrainPath;
        }

        private string BuildProgressionSummary()
        {
            if (_context == null || _context.GameplayRuntime == null)
            {
                return "Lv 1 | Exp 0/0 | Gold 0 | Stage 1";
            }

            long experience = _context.GameplayRuntime.MetaProgressionState != null
                ? _context.GameplayRuntime.MetaProgressionState.Experience
                : 0L;
            int nextLevel = _context.CurrentPlayerLevel + 1;
            string nextLevelExp = "MAX";

            IHolmasTaskCatalog taskCatalog = _context.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasTaskCatalog>()
                : null;
            if (taskCatalog != null &&
                taskCatalog.TryGetPlayerLevel(nextLevel, out HolmasPlayerLevelDefinition nextDefinition) &&
                nextDefinition != null)
            {
                nextLevelExp = nextDefinition.UpgradeExp.ToString();
            }

            return $"Lv {_context.CurrentPlayerLevel} | Exp {experience}/{nextLevelExp} | Gold {_context.CurrentGoldBalance} | Stage {_context.CurrentAgencyStageId}";
        }

        private string BuildPromotionSummary()
        {
            HolmasAgencyBuildingDefinition promotion = GetPrimaryPromotionDefinition();
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.PromotionId))
            {
                return "宣传 暂无可升级项";
            }

            int currentLevel = _context?.GameplayRuntime?.MetaProgressionState != null
                ? HolmasAgencyPromotionStateKey.GetLevel(_context.GameplayRuntime.MetaProgressionState, promotion.AgencyStageId, promotion.PromotionId)
                : 0;
            int nextCost = GetPromotionUpgradeCost(promotion, currentLevel);
            return nextCost > 0
                ? $"宣传 {promotion.PromotionId} Lv {currentLevel}/{promotion.PromotionLevelCap} | Next Cost {nextCost} | +1 Exp"
                : $"宣传 {promotion.PromotionId} Lv {currentLevel}/{promotion.PromotionLevelCap} | 已满级";
        }

        private string BuildPromotionButtonLabel(HolmasAgencyBuildingDefinition promotion)
        {
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.PromotionId))
            {
                return "宣传待开放";
            }

            int currentLevel = _context?.GameplayRuntime?.MetaProgressionState != null
                ? HolmasAgencyPromotionStateKey.GetLevel(_context.GameplayRuntime.MetaProgressionState, promotion.AgencyStageId, promotion.PromotionId)
                : 0;
            int nextCost = GetPromotionUpgradeCost(promotion, currentLevel);
            return nextCost > 0
                ? $"升级 {promotion.PromotionId} Lv {currentLevel}->{currentLevel + 1} ({nextCost} Gold)"
                : $"{promotion.PromotionId} 已满级";
        }

        private static int GetPromotionUpgradeCost(HolmasAgencyBuildingDefinition promotion, int currentLevel)
        {
            if (promotion == null ||
                promotion.PromotionUpgradeCosts == null ||
                currentLevel < 0 ||
                currentLevel >= promotion.PromotionUpgradeCosts.Length)
            {
                return 0;
            }

            return System.Math.Max(0, promotion.PromotionUpgradeCosts[currentLevel]);
        }

        private HolmasCatVisualResolver CreateCatVisualResolver()
        {
            IHolmasTaskCatalog taskCatalog = _context?.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasTaskCatalog>()
                : null;
            return new HolmasCatVisualResolver(taskCatalog);
        }

        private MainTaskItemVm[] BuildTaskItems(HolmasCatVisualResolver visualResolver, bool disableInteraction)
        {
            if (_context == null || _context.GameplayRuntime == null || _context.GameplayRuntime.TaskBarState == null)
            {
                return System.Array.Empty<MainTaskItemVm>();
            }

            HolmasTaskBarState taskBar = _context.GameplayRuntime.TaskBarState;
            if (taskBar.Slots == null || taskBar.Slots.Count == 0)
            {
                return System.Array.Empty<MainTaskItemVm>();
            }

            int count = taskBar.Slots.Count;
            var items = new List<MainTaskItemVm>(count);
            for (int i = 0; i < count; i++)
            {
                MainTaskItemVm item = BuildTaskItem(taskBar.Slots[i], taskBar.GetTaskBySlot(i), i, visualResolver);
                if (disableInteraction)
                {
                    item.ButtonEnabled = false;
                }

                items.Add(item);
            }

            return items.ToArray();
        }

        private string BuildTaskProgressSummary()
        {
            HolmasTaskBarState taskBar = _context?.GameplayRuntime?.TaskBarState;
            if (taskBar == null || taskBar.Tasks == null || taskBar.Tasks.Count == 0)
            {
                return "Task none";
            }

            List<HolmasTaskRuntimeInstance> activeTasks = taskBar.Tasks
                .Where(item => item != null && item.Task != null && !item.IsRewardClaimed)
                .OrderBy(item => item.Task.SlotIndex)
                .ToList();
            if (activeTasks.Count == 0)
            {
                return "Task none";
            }

            if (activeTasks.Count == 1)
            {
                return BuildSingleTaskProgress("Task", activeTasks[0], includeCatId: false);
            }

            return "Tasks " + string.Join("; ", activeTasks.Select(BuildTaskProgressEntry));
        }

        private static string BuildTaskProgressEntry(HolmasTaskRuntimeInstance task)
        {
            if (task == null || task.Task == null)
            {
                return "unknown 0/0";
            }

            string catId = string.IsNullOrWhiteSpace(task.Task.CatId) ? "unknown" : task.Task.CatId;
            return $"{catId} {System.Math.Max(0, task.Task.CurrentCount)}/{System.Math.Max(0, task.Task.TargetCount)}";
        }

        private static string BuildSingleTaskProgress(string label, HolmasTaskRuntimeInstance task, bool includeCatId)
        {
            if (task == null || task.Task == null)
            {
                return $"{label} 0/0";
            }

            string prefix = includeCatId && !string.IsNullOrWhiteSpace(task.Task.CatId)
                ? $"{label} {task.Task.CatId}"
                : label;
            return $"{prefix} {System.Math.Max(0, task.Task.CurrentCount)}/{System.Math.Max(0, task.Task.TargetCount)}";
        }

        private static IReadOnlyDictionary<string, HolmasCatVisualVm> BuildCatVisualLookup(
            IReadOnlyList<BoardCellState> cells,
            IReadOnlyList<MainTaskItemVm> taskItems,
            HolmasCatVisualResolver visualResolver)
        {
            var lookup = new Dictionary<string, HolmasCatVisualVm>(System.StringComparer.Ordinal);
            if (visualResolver == null)
            {
                return lookup;
            }

            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    RegisterCatVisual(lookup, cells[i].CatId, visualResolver);
                }
            }

            if (taskItems != null)
            {
                for (int i = 0; i < taskItems.Count; i++)
                {
                    RegisterCatVisual(lookup, taskItems[i]?.CatId, visualResolver);
                }
            }

            return lookup;
        }

        private static void RegisterCatVisual(
            IDictionary<string, HolmasCatVisualVm> lookup,
            string catId,
            HolmasCatVisualResolver visualResolver)
        {
            if (lookup == null || visualResolver == null || string.IsNullOrWhiteSpace(catId) || lookup.ContainsKey(catId))
            {
                return;
            }

            lookup[catId] = visualResolver.Resolve(catId);
        }

        private static MainTaskItemVm BuildTaskItem(
            TaskSlotState slot,
            HolmasTaskRuntimeInstance runtimeTask,
            int slotIndex,
            HolmasCatVisualResolver visualResolver)
        {
            var item = new MainTaskItemVm
            {
                SlotIndex = slotIndex,
                Title = $"任务槽 {slotIndex + 1}",
                ButtonEnabled = true,
            };

            if (slot == null || !slot.IsUnlocked)
            {
                item.Progress = "未解锁";
                item.Reward = "需通过任务或广告解锁";
                item.ProgressNormalized = 0f;
                item.IsLocked = true;
                item.ButtonEnabled = false;
                return item;
            }

            if (runtimeTask == null || runtimeTask.Task == null)
            {
                item.Progress = "空槽";
                item.Reward = "当前等级暂无可补任务";
                item.ProgressNormalized = 0f;
                item.IsEmpty = true;
                return item;
            }

            HolmasCatVisualVm visual = visualResolver != null
                ? visualResolver.Resolve(runtimeTask.Task.CatId)
                : HolmasCatVisualVm.CreateFallback(runtimeTask.Task.CatId);
            item.CatId = visual.CatId;
            item.CatName = visual.CatName;
            item.CatIconPath = visual.IconPath;
            item.Title = string.IsNullOrWhiteSpace(visual.CatName) ? runtimeTask.Task.CatId : visual.CatName;
            item.Progress = $"{runtimeTask.Task.CurrentCount}/{runtimeTask.Task.TargetCount}";
            item.Reward = slot.PendingRelockAfterTaskCompletion
                ? $"奖励 {runtimeTask.Task.Reward} Gold | 完成后自动领奖并锁定"
                : $"奖励 {runtimeTask.Task.Reward} Gold | 完成后自动领奖";
            item.ProgressNormalized = runtimeTask.Task.TargetCount > 0
                ? UnityEngine.Mathf.Clamp01((float)runtimeTask.Task.CurrentCount / runtimeTask.Task.TargetCount)
                : 0f;
            item.IsClaimable = false;
            return item;
        }

        private sealed class BoardFrameConfig
        {
            public static readonly BoardFrameConfig Default = new BoardFrameConfig();

            public string BoardBackgroundPath = string.Empty;
            public string BoardFrameOverlayPath = string.Empty;
            public UnityEngine.Vector4 BoardContentInset = UnityEngine.Vector4.zero;
            public float MinCellSpacing = 4f;
        }
    }
}
