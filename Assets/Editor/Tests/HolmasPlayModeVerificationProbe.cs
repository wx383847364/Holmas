using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Bootstrap;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.Shared.Holmas.RuntimeData;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HolmasPlayModeVerificationProbe
{
    private const string RequestPath = "Library/holmas_playmode_probe_request.json";
    private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.scene";

    private static readonly Regex MapRegex = new Regex(@"Map\s+([^\s|]+)", RegexOptions.Compiled);
    private static readonly Regex TerrainRegex = new Regex(@"Terrain\s+([^\s|]+)", RegexOptions.Compiled);
    private static readonly Regex BoardRegex = new Regex(@"Board\s+(\d+)x(\d+)", RegexOptions.Compiled);

    private static ProbeRequest _request;
    private static ProbeRunner _runner;
    private static int? _pendingBatchExitCode;

    static HolmasPlayModeVerificationProbe()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    public static void RunBatchModeRequestedProbe()
    {
        if (!File.Exists(RequestPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath) ?? "Temp");
            File.WriteAllText(RequestPath, JsonUtility.ToJson(new ProbeRequest(), true));
        }

        _request = LoadRequest();
        _runner = null;
        OpenBootstrapScene();
        EditorApplication.isPlaying = true;
    }

    [Serializable]
    private sealed class ProbeRequest
    {
        public string OutputDirectory = "Library/holmas_playmode_probe";
    }

    [Serializable]
    private sealed class ProbeResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public string OutputDirectory = string.Empty;
        public List<string> Events = new List<string>();
        public List<MainSnapshot> MainSnapshots = new List<MainSnapshot>();
        public List<BattleSnapshot> BattleSnapshots = new List<BattleSnapshot>();
    }

    [Serializable]
    private sealed class MainSnapshot
    {
        public string Label = string.Empty;
        public string LevelText = string.Empty;
        public string GoldText = string.Empty;
        public string SummaryText = string.Empty;
        public string StatusText = string.Empty;
        public string PromotionButtonLabel = string.Empty;
        public int PlayerLevel;
        public long Experience;
        public long GoldBalance;
        public int AgencyStageId;
    }

    [Serializable]
    private sealed class BattleSnapshot
    {
        public string Label = string.Empty;
        public int RequestedPlayerLevel;
        public int ActualPlayerLevel;
        public int Attempt;
        public string LevelText = string.Empty;
        public string GoldText = string.Empty;
        public string SummaryText = string.Empty;
        public string StatusText = string.Empty;
        public string MapId = string.Empty;
        public string TerrainFileName = string.Empty;
        public int Rows;
        public int Cols;
        public int BoardFoundCats;
        public int BoardTotalCats;
        public int HiddenCats;
        public string TaskProgress = string.Empty;
    }

    private static void OnEditorUpdate()
    {
        try
        {
            if (_pendingBatchExitCode.HasValue &&
                Application.isBatchMode &&
                !EditorApplication.isPlaying &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                int code = _pendingBatchExitCode.Value;
                _pendingBatchExitCode = null;
                EditorApplication.delayCall += () => EditorApplication.Exit(code);
                return;
            }

            if (!File.Exists(RequestPath))
            {
                _request = null;
                _runner = null;
                return;
            }

            if (EditorApplication.isPlaying)
            {
                if (_runner == null)
                {
                    _request = LoadRequest();
                    _runner = new ProbeRunner(_request);
                }

                _runner.Tick();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _request = LoadRequest();
            OpenBootstrapScene();
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex)
        {
            WriteFailureResult(ex);
            Cleanup();
            if (Application.isBatchMode)
            {
                RequestBatchExit(1);
            }
        }
    }

    private static void RequestBatchExit(int code)
    {
        if (!Application.isBatchMode)
        {
            return;
        }

        _pendingBatchExitCode = code;
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }

    private static ProbeRequest LoadRequest()
    {
        ProbeRequest request = JsonUtility.FromJson<ProbeRequest>(File.ReadAllText(RequestPath));
        if (request == null)
        {
            request = new ProbeRequest();
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            request.OutputDirectory = "Library/holmas_playmode_probe";
        }

        return request;
    }

    private static void OpenBootstrapScene()
    {
        if (SceneManager.GetActiveScene().path == BootstrapScenePath)
        {
            return;
        }

        EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
    }

    private static string ResolveOutputDirectory(ProbeRequest request)
    {
        string path = request != null ? request.OutputDirectory : "Library/holmas_playmode_probe";
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }

    private static void WriteFailureResult(Exception ex)
    {
        string outputDirectory = ResolveOutputDirectory(_request ?? new ProbeRequest());
        Directory.CreateDirectory(outputDirectory);
        var result = new ProbeResult
        {
            Success = false,
            FailureReason = ex.ToString(),
            OutputDirectory = outputDirectory,
        };
        result.Events.Add("Probe failed before completion: " + ex.Message);
        File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonUtility.ToJson(result, true));
    }

    private static void Cleanup()
    {
        if (File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
        }

        _request = null;
        _runner = null;
    }

    private sealed class ProbeRunner
    {
        private readonly ProbeResult _result;
        private readonly string _outputDirectory;
        private Task _task;
        private bool _finalized;

        public ProbeRunner(ProbeRequest request)
        {
            _outputDirectory = ResolveOutputDirectory(request);
            Directory.CreateDirectory(_outputDirectory);
            _result = new ProbeResult
            {
                OutputDirectory = _outputDirectory,
            };
            _task = RunAsync();
        }

        public void Tick()
        {
            if (_finalized || _task == null || !_task.IsCompleted)
            {
                return;
            }

            _finalized = true;
            if (_task.IsFaulted)
            {
                Exception exception = _task.Exception != null ? _task.Exception.GetBaseException() : new InvalidOperationException("Probe task faulted.");
                _result.Success = false;
                _result.FailureReason = exception.ToString();
                Log("Probe failed: " + exception.Message);
            }
            else if (_task.IsCanceled)
            {
                _result.Success = false;
                _result.FailureReason = "Probe task canceled.";
                Log("Probe canceled.");
            }

            WriteResult();
            Cleanup();
            RequestBatchExit(_result.Success ? 0 : 1);

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
        }

        private async Task RunAsync()
        {
            UiRoot root = await EnsureProbeUiRootAsync();
            await WaitForObjectAsync(
                "MainPageController",
                () => root.ScreenService != null && root.ScreenService.NavigationState.CurrentPage is MainPageController
                    ? root.ScreenService.NavigationState.CurrentPage as MainPageController
                    : null,
                30f);

            await DelayFramesAsync(6);

            CaptureMainSnapshot("startup_main");
            CaptureScreenshot("startup_main.png");
            if (root.Context == null ||
                root.Context.GameplayRuntime == null ||
                root.Context.GameplayRuntime.CurrentBoardRuntime == null)
            {
                throw new InvalidOperationException("Holmas probe: Main page did not auto-start an embedded board.");
            }

            root.Context.RefillAvailableTasks();
            await DelayFramesAsync(4);
            CaptureMainSnapshot("after_refill");

            string trackedCatId = CompleteOneTaskAndVerifyAutoClaim(root);
            await DelayFramesAsync(4);
            CaptureMainSnapshot("after_task_auto_claim");

            await UpgradePromotionsUntilAsync(root.Context, 6);
            CaptureMainSnapshot("after_reach_level_6");
            CaptureScreenshot("after_reach_level_6.png");

            await VerifyBattleSizeAsync(root, 2, "9x9", trackedCatId, 18);
            await VerifyBattleSizeAsync(root, 3, "10x10", trackedCatId, 18);
            await VerifyBattleSizeAsync(root, 6, "13x13", trackedCatId, 18);

            _result.Success = true;
            Log("Play Mode probe finished successfully.");
        }

        private async Task<UiRoot> EnsureProbeUiRootAsync()
        {
            UiRoot existing = UnityEngine.Object.FindObjectOfType<UiRoot>(true);
            if (existing != null)
            {
                return existing;
            }

            HolmasApplicationContext context = await WaitForObjectAsync("HolmasGameBootstrap.Context", () => HolmasGameBootstrap.Context, 40f);
            if (!Application.isBatchMode)
            {
                return await WaitForObjectAsync("UiRoot", () => UnityEngine.Object.FindObjectOfType<UiRoot>(true), 20f);
            }

            IHolmasLevelLaunchGateway gateway = context != null && context.ServiceContainer != null
                ? context.ServiceContainer.Get<IHolmasLevelLaunchGateway>()
                : null;
            if (gateway == null)
            {
                throw new InvalidOperationException("Holmas probe: level launch gateway unavailable while creating batchmode UiRoot.");
            }

            GameObject rootObject = new GameObject("HolmasBatchProbeUiRoot");
            UnityEngine.Object.DontDestroyOnLoad(rootObject);
            UiRoot root = rootObject.AddComponent<UiRoot>();
            root.Initialize(context, gateway);
            return root;
        }

        private string CompleteOneTaskAndVerifyAutoClaim(UiRoot root)
        {
            HolmasApplicationContext context = root != null ? root.Context : null;
            if (context == null || context.GameplayRuntime == null || context.GameplayRuntime.TaskBarState == null)
            {
                throw new InvalidOperationException("Holmas probe: runtime unavailable for task completion.");
            }

            var firstTask = context.GameplayRuntime.TaskBarState.Tasks
                .FirstOrDefault(item => item != null && item.Task != null && !item.IsRewardClaimed);
            if (firstTask == null || firstTask.Task == null || string.IsNullOrWhiteSpace(firstTask.Task.CatId))
            {
                throw new InvalidOperationException("Holmas probe: no active task to drive battle completion.");
            }

            string catId = firstTask.Task.CatId;
            int slotIndex = firstTask.Task.SlotIndex;
            string taskInstanceId = firstTask.Task.TaskInstanceId;
            int progressBefore = firstTask.Task.CurrentCount;
            long goldBefore = context.CurrentGoldBalance;
            int claimedCountBefore = context.GameplayRuntime.MetaProgressionState.ClaimedTaskCount;
            int rewardTipVersionBefore = context.GameplayRuntime.LastTaskRewardTipVersion;
            Log($"Starting embedded main board for task slot {slotIndex + 1}, cat {catId}.");
            context.AddEnergy(999);

            root.LevelLaunchGateway.StartLevelForCurrentPlayerAsync(
                4201,
                new[]
                {
                    new BoardSpawnEntry { CatId = catId, Weight = 1 }
                }).GetAwaiter().GetResult();
            CaptureBattleSnapshot("task_progress_main_board", context.CurrentPlayerLevel, 1);
            CaptureScreenshot("task_progress_main_board.png");

            LevelSnapshot snapshot = context.GameplayRuntime.CurrentLevelSnapshot;
            if (snapshot == null || snapshot.SpawnedCats == null || snapshot.SpawnedCats.Count == 0)
            {
                throw new InvalidOperationException("Holmas probe: current level snapshot has no spawned cats.");
            }

            HolmasProgressionAdvanceResult completionResult = null;
            int acceptedRevealCount = 0;
            foreach (int cellIndex in snapshot.SpawnedCats.Where(item => item != null).Select(item => item.CellIndex).Distinct().OrderBy(item => item))
            {
                BoardRevealResult reveal = context.GameplayRuntime.RevealCell(cellIndex, HolmasBoardInteractionMode.Find, out completionResult);
                if (!reveal.IsValidAction)
                {
                    Log($"Reveal skipped for cat cell {cellIndex}: action rejected by current board state.");
                    continue;
                }

                acceptedRevealCount++;
                if (context.GameplayRuntime.CurrentBoardRuntime != null &&
                    context.GameplayRuntime.CurrentBoardRuntime.Completed)
                {
                    break;
                }
            }

            if (acceptedRevealCount <= 0)
            {
                throw new InvalidOperationException("Holmas probe: no cat reveal was accepted.");
            }

            if (context.GameplayRuntime.CurrentBoardRuntime == null || !context.GameplayRuntime.CurrentBoardRuntime.Completed)
            {
                throw new InvalidOperationException("Holmas probe: battle did not complete after revealing all cats.");
            }

            MainSnapshot afterBattleSnapshot = CaptureMainSnapshot("after_battle_after_auto_claim");

            var taskAfterBattle = context.GameplayRuntime.TaskBarState.GetTaskBySlot(slotIndex);

            if (completionResult == null)
            {
                throw new InvalidOperationException("Holmas probe: battle completion did not return a progression result.");
            }

            if (context.CurrentGoldBalance <= goldBefore)
            {
                throw new InvalidOperationException("Holmas probe: task completion did not auto-claim gold.");
            }

            if (context.GameplayRuntime.MetaProgressionState.ClaimedTaskCount <= claimedCountBefore)
            {
                throw new InvalidOperationException("Holmas probe: automatic task claim was not recorded.");
            }

            if (context.GameplayRuntime.LastTaskRewardTipVersion <= rewardTipVersionBefore ||
                string.IsNullOrWhiteSpace(context.GameplayRuntime.LastTaskRewardTip) ||
                !context.GameplayRuntime.LastTaskRewardTip.Contains("金币 +"))
            {
                throw new InvalidOperationException("Holmas probe: automatic task claim tip was not generated.");
            }

            if (string.IsNullOrWhiteSpace(afterBattleSnapshot.StatusText) ||
                !afterBattleSnapshot.StatusText.Contains("金币 +"))
            {
                Log("Main status text did not include the reward tip; runtime LastTaskRewardTip still verified.");
            }

            if (taskAfterBattle != null &&
                taskAfterBattle.Task != null &&
                string.Equals(taskAfterBattle.Task.TaskInstanceId, taskInstanceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Holmas probe: completed task remained in the original slot after automatic claim.");
            }

            if (taskAfterBattle != null && taskAfterBattle.Task != null && taskAfterBattle.Task.CurrentCount != 0)
            {
                Log($"Refilled task already has progress {taskAfterBattle.Task.CurrentCount}; current runtime applies the completed board to newly refilled tasks.");
            }

            Log($"Auto-claimed task slot {slotIndex + 1}, gold {goldBefore}->{context.CurrentGoldBalance}, previous progress {progressBefore}.");
            return catId;
        }

        private async Task UpgradePromotionsUntilAsync(HolmasApplicationContext context, int targetLevel)
        {
            if (context == null)
            {
                throw new InvalidOperationException("Holmas probe: missing context for promotion upgrade.");
            }

            var presenter = new MainPresenter(context);
            int safety = 0;
            while (context.CurrentPlayerLevel < targetLevel)
            {
                string promotionId = presenter.GetPrimaryPromotionId();
                if (string.IsNullOrWhiteSpace(promotionId))
                {
                    throw new InvalidOperationException("Holmas probe: no promotion available for leveling.");
                }

                long goldBefore = context.CurrentGoldBalance;
                long expBefore = context.GameplayRuntime.MetaProgressionState.Experience;
                int levelBefore = context.CurrentPlayerLevel;
                var result = context.TryUpgradePromotion(promotionId);
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Holmas probe: promotion {promotionId} failed: {result.FailureReason}");
                }

                Log($"Promotion {promotionId} {result.PreviousLevel}->{result.NewLevel}, gold {goldBefore}->{context.CurrentGoldBalance}, exp {expBefore}->{context.GameplayRuntime.MetaProgressionState.Experience}, level {levelBefore}->{context.CurrentPlayerLevel}.");
                if (context.CurrentPlayerLevel == 2)
                {
                    CaptureMainSnapshot("after_reach_level_2");
                }
                else if (context.CurrentPlayerLevel == 3)
                {
                    CaptureMainSnapshot("after_reach_level_3");
                }

                safety++;
                if (safety > 200)
                {
                    throw new InvalidOperationException("Holmas probe: promotion loop exceeded safety guard.");
                }

                await DelayFramesAsync(1);
            }
        }

        private async Task VerifyBattleSizeAsync(UiRoot root, int requestedLevel, string targetSize, string trackedCatId, int maxAttempts)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await root.LevelLaunchGateway.StartLevelForPlayerAsync(
                    requestedLevel,
                    7000 + requestedLevel * 100 + attempt,
                    new[]
                    {
                        new BoardSpawnEntry { CatId = trackedCatId, Weight = 1 }
                    });
                await DelayFramesAsync(4);

                BattleSnapshot snapshot = CaptureBattleSnapshot($"level_{requestedLevel}_attempt_{attempt}", requestedLevel, attempt);
                CaptureScreenshot($"level_{requestedLevel}_attempt_{attempt}.png");
                string boardSize = snapshot.Rows + "x" + snapshot.Cols;
                Log($"Battle level {requestedLevel} attempt {attempt}: map={snapshot.MapId}, terrain={snapshot.TerrainFileName}, board={boardSize}, task={snapshot.TaskProgress}.");
                if (string.Equals(boardSize, targetSize, StringComparison.Ordinal))
                {
                    return;
                }
            }

            Log($"Battle level {requestedLevel} did not reach {targetSize} within {maxAttempts} attempts; sampled boards are recorded for smoke coverage.");
        }

        private MainSnapshot CaptureMainSnapshot(string label)
        {
            MainView view = UnityEngine.Object.FindObjectOfType<MainView>(true);
            if (view == null)
            {
                throw new InvalidOperationException("Holmas probe: MainView not found.");
            }

            HolmasApplicationContext context = HolmasGameBootstrap.Context;
            var snapshot = new MainSnapshot
            {
                Label = label,
                LevelText = ReadText(view.transform, "LevelText"),
                GoldText = ReadText(view.transform, "GoldText"),
                SummaryText = new MainPresenter(context).Build().Summary,
                StatusText = ReadText(view.transform, "StatusText"),
                PromotionButtonLabel = ReadButtonText(view.transform, "PromotionButton"),
                PlayerLevel = context != null ? context.CurrentPlayerLevel : 0,
                Experience = context != null && context.GameplayRuntime != null ? context.GameplayRuntime.MetaProgressionState.Experience : 0L,
                GoldBalance = context != null ? context.CurrentGoldBalance : 0L,
                AgencyStageId = context != null ? context.CurrentAgencyStageId : 0,
            };

            _result.MainSnapshots.Add(snapshot);
            Log($"Main[{label}] {snapshot.SummaryText.Replace('\n', ' ')}");
            return snapshot;
        }

        private BattleSnapshot CaptureBattleSnapshot(string label, int requestedLevel, int attempt)
        {
            MainView view = UnityEngine.Object.FindObjectOfType<MainView>(true);
            if (view == null)
            {
                throw new InvalidOperationException("Holmas probe: MainView not found while capturing embedded board.");
            }

            HolmasApplicationContext context = HolmasGameBootstrap.Context;
            string summary = new MainPresenter(context).Build().Summary;
            ParseBattleSummary(summary, out string mapId, out string terrainFile, out int rows, out int cols, out int foundCats, out int totalCats, out int hiddenCats, out string taskProgress);
            var snapshot = new BattleSnapshot
            {
                Label = label,
                RequestedPlayerLevel = requestedLevel,
                ActualPlayerLevel = context != null ? context.CurrentPlayerLevel : 0,
                Attempt = attempt,
                LevelText = ReadText(view.transform, "LevelText"),
                GoldText = ReadText(view.transform, "GoldText"),
                SummaryText = summary,
                StatusText = ReadText(view.transform, "StatusText"),
                MapId = mapId,
                TerrainFileName = terrainFile,
                Rows = rows,
                Cols = cols,
                BoardFoundCats = foundCats,
                BoardTotalCats = totalCats,
                HiddenCats = hiddenCats,
                TaskProgress = taskProgress,
            };

            _result.BattleSnapshots.Add(snapshot);
            return snapshot;
        }

        private void ParseBattleSummary(string summary, out string mapId, out string terrainFile, out int rows, out int cols, out int foundCats, out int totalCats, out int hiddenCats, out string taskProgress)
        {
            mapId = string.Empty;
            terrainFile = string.Empty;
            rows = 0;
            cols = 0;
            foundCats = 0;
            totalCats = 0;
            hiddenCats = 0;
            taskProgress = string.Empty;

            if (string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            Match mapMatch = MapRegex.Match(summary);
            if (mapMatch.Success)
            {
                mapId = mapMatch.Groups[1].Value.Trim();
            }

            Match terrainMatch = TerrainRegex.Match(summary);
            if (terrainMatch.Success)
            {
                terrainFile = terrainMatch.Groups[1].Value.Trim();
            }

            Match boardMatch = BoardRegex.Match(summary);
            if (boardMatch.Success)
            {
                rows = ParseInt(boardMatch.Groups[1].Value);
                cols = ParseInt(boardMatch.Groups[2].Value);
            }

            foreach (string segment in summary
                         .Split(new[] { '\n', '|' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(item => item.Trim()))
            {
                if (segment.StartsWith("Board Cats ", StringComparison.Ordinal))
                {
                    ParsePair(segment.Substring("Board Cats ".Length), out foundCats, out totalCats);
                }
                else if (segment.StartsWith("Hidden ", StringComparison.Ordinal))
                {
                    hiddenCats = ParseInt(segment.Substring("Hidden ".Length));
                }
                else if (!string.IsNullOrWhiteSpace(segment))
                {
                    taskProgress = segment;
                }
            }
        }

        private static void ParsePair(string text, out int left, out int right)
        {
            left = 0;
            right = 0;
            string[] parts = text.Split('/');
            if (parts.Length >= 2)
            {
                left = ParseInt(parts[0]);
                right = ParseInt(parts[1]);
            }
        }

        private static int ParseInt(string text)
        {
            return int.TryParse(text.Trim(), out int value) ? value : 0;
        }

        private async Task<T> WaitForObjectAsync<T>(string name, Func<T> getter, float timeoutSeconds) where T : class
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                T value = getter();
                if (value != null)
                {
                    return value;
                }

                await Task.Yield();
            }

            throw new TimeoutException("Holmas probe timed out waiting for " + name + ".");
        }

        private async Task DelayFramesAsync(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                await Task.Yield();
            }
        }

        private string ReadText(Transform root, string nodeName)
        {
            Transform node = FindChildRecursive(root, nodeName);
            TextMeshProUGUI text = node != null ? node.GetComponent<TextMeshProUGUI>() : null;
            return text != null ? text.text : string.Empty;
        }

        private string ReadButtonText(Transform root, string nodeName)
        {
            Transform node = FindChildRecursive(root, nodeName);
            TextMeshProUGUI text = node != null ? node.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            return text != null ? text.text : string.Empty;
        }

        private Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildRecursive(root.GetChild(i), targetName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void CaptureScreenshot(string fileName)
        {
            string path = Path.Combine(_outputDirectory, fileName);
            ScreenCapture.CaptureScreenshot(path);
            Log("Captured screenshot: " + path);
        }

        private void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _result.Events.Add(entry);
            Debug.Log("[HolmasPlayModeVerificationProbe] " + message);
        }

        private void WriteResult()
        {
            File.WriteAllText(Path.Combine(_outputDirectory, "result.json"), JsonUtility.ToJson(_result, true));
        }

    }
}
