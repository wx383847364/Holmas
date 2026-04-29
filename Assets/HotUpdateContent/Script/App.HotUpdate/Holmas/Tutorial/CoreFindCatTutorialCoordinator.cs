using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.PlayerData;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.Tutorial
{
    public sealed class CoreFindCatTutorialCoordinator : IDisposable
    {
        private readonly CoreFindCatTutorialProgressService _progressService;
        private readonly CoreFindCatTutorialLevelService _levelService;
        private readonly CoreFindCatTutorialSessionService _sessionService;
        private IAssetHandle _visualConfigHandle;
        private TutorialVisualConfig _visualConfig;

        public CoreFindCatTutorialCoordinator(
            CoreFindCatTutorialProgressService progressService,
            CoreFindCatTutorialLevelService levelService,
            CoreFindCatTutorialSessionService sessionService = null)
        {
            _progressService = progressService;
            _levelService = levelService;
            _sessionService = sessionService;
        }

        public CoreFindCatTutorialProgressService ProgressService => _progressService;

        public async Task<CoreFindCatTutorialLaunchResult> PrepareAutoStartAsync(HolmasApplicationContext context)
        {
            CoreFindCatTutorialProgress progress = _progressService != null
                ? await _progressService.LoadAsync()
                : new CoreFindCatTutorialProgress();

            if (_sessionService != null && _sessionService.ConsumeAutoStartSuppression())
            {
                return CoreFindCatTutorialLaunchResult.AutoStartNormal(skipLoading: true);
            }

            if (progress.completed)
            {
                return CoreFindCatTutorialLaunchResult.AutoStartNormal(skipLoading: true);
            }

            CoreFindCatTutorialSessionService sessionService = ResolveSessionService(context);
            if (sessionService != null && sessionService.HasActiveSession)
            {
                int activeSessionStepIndex = ResolveProgressStepIndex(progress, fallback: 0);
                await MarkStartedAsync(activeSessionStepIndex, force: false);
                TutorialOverlayPayload payload = BuildPayload(
                    TutorialRunMode.FullTutorial,
                    activeSessionStepIndex,
                    canWriteCompletion: true,
                    await LoadVisualConfigAsync(context),
                    context != null ? context.AssetsRuntime : null);
                payload.TutorialBoardObjectiveSatisfied = sessionService.ActiveSession.TutorialBoardObjectiveSatisfied;
                return CoreFindCatTutorialLaunchResult.ShowOverlay(payload);
            }

            HolmasGameplayRuntime runtime = context != null ? context.GameplayRuntime : null;
            if (runtime != null && runtime.HasActiveUncompletedLevel && runtime.CurrentBoardRuntime != null)
            {
                if (CoreFindCatTutorialLevelService.IsTutorialLevel(runtime.CurrentLevelSnapshot))
                {
                    int legacyTutorialStepIndex = ResolveProgressStepIndex(progress, fallback: 0);
                    await MarkStartedAsync(legacyTutorialStepIndex, force: false);
                    return CoreFindCatTutorialLaunchResult.ShowOverlay(BuildPayload(
                        TutorialRunMode.FullTutorial,
                        legacyTutorialStepIndex,
                        canWriteCompletion: true,
                        await LoadVisualConfigAsync(context),
                        context != null ? context.AssetsRuntime : null));
                }

                if (ShouldResumePostTutorialBoardSteps(progress))
                {
                    int resumeStepIndex = ResolveProgressStepIndex(
                        progress,
                        CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId));
                    TutorialOverlayPayload payload = BuildPayload(
                        TutorialRunMode.FullTutorial,
                        resumeStepIndex,
                        canWriteCompletion: true,
                        await LoadVisualConfigAsync(context),
                        context != null ? context.AssetsRuntime : null);
                    payload.TutorialBoardObjectiveSatisfied = true;
                    return CoreFindCatTutorialLaunchResult.ShowOverlay(payload);
                }

                await StartTutorialBoardAsync(context);
                await MarkStartedAsync(0, force: false);
                return CoreFindCatTutorialLaunchResult.ShowOverlay(BuildPayload(
                    TutorialRunMode.FullTutorial,
                    0,
                    canWriteCompletion: true,
                    await LoadVisualConfigAsync(context),
                    context != null ? context.AssetsRuntime : null));
            }

            await StartTutorialBoardAsync(context);
            await MarkStartedAsync(0, force: false);
            return CoreFindCatTutorialLaunchResult.ShowOverlay(BuildPayload(
                TutorialRunMode.FullTutorial,
                0,
                canWriteCompletion: true,
                await LoadVisualConfigAsync(context),
                context != null ? context.AssetsRuntime : null));
        }

        public async Task<CoreFindCatTutorialLaunchResult> PrepareManualStartAsync(
            HolmasApplicationContext context,
            int requestedStepIndex,
            bool debugForceStep)
        {
            int stepIndex = CoreFindCatTutorialSteps.ClampIndex(requestedStepIndex);
            TutorialStepDefinition step = CoreFindCatTutorialSteps.Get(stepIndex);
            HolmasGameplayRuntime runtime = context != null ? context.GameplayRuntime : null;
            CoreFindCatTutorialSessionService sessionService = ResolveSessionService(context);
            bool hasTutorialSession = sessionService != null && sessionService.HasActiveSession;
            bool hasTutorialLevel = hasTutorialSession || CoreFindCatTutorialLevelService.IsTutorialLevel(runtime?.CurrentLevelSnapshot);

            if (!hasTutorialLevel && RequiresTutorialBoardSession(stepIndex, step))
            {
                await StartTutorialBoardAsync(context);
            }

            await MarkStartedAsync(stepIndex, force: true);
            return CoreFindCatTutorialLaunchResult.ShowOverlay(BuildPayload(
                debugForceStep ? TutorialRunMode.DebugStartAtStep : TutorialRunMode.FullTutorial,
                stepIndex,
                canWriteCompletion: true,
                await LoadVisualConfigAsync(context),
                context != null ? context.AssetsRuntime : null));
        }

        public async Task<TutorialOverlayPayload> CreateReplayPayloadAsync(HolmasApplicationContext context, int stepIndex)
        {
            return BuildPayload(
                TutorialRunMode.Replay,
                CoreFindCatTutorialSteps.ClampIndex(stepIndex),
                canWriteCompletion: false,
                await LoadVisualConfigAsync(context),
                context != null ? context.AssetsRuntime : null);
        }

        public async Task<TutorialOverlayPayload> CreateResumePayloadAsync(HolmasApplicationContext context, int minimumStepIndex)
        {
            CoreFindCatTutorialProgress progress = _progressService != null
                ? await _progressService.LoadAsync()
                : new CoreFindCatTutorialProgress();
            int stepIndex = Math.Max(
                CoreFindCatTutorialSteps.ClampIndex(minimumStepIndex),
                ResolveProgressStepIndex(progress, minimumStepIndex));

            TutorialOverlayPayload payload = BuildPayload(
                TutorialRunMode.FullTutorial,
                stepIndex,
                canWriteCompletion: true,
                await LoadVisualConfigAsync(context),
                context != null ? context.AssetsRuntime : null);
            payload.TutorialBoardObjectiveSatisfied = stepIndex > CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.ContinueFindStepId);
            return payload;
        }

        public static bool ShouldEndTutorialLevelAfterExit(HolmasGameplayRuntime runtime)
        {
            return runtime != null &&
                   IsActiveTutorialLevel(runtime);
        }

        private static bool IsActiveTutorialLevel(HolmasGameplayRuntime runtime)
        {
            return runtime.CurrentBoardRuntime != null &&
                   CoreFindCatTutorialLevelService.IsTutorialLevel(runtime.CurrentLevelSnapshot);
        }

        public void Dispose()
        {
            _visualConfigHandle?.Release();
            _visualConfigHandle = null;
            _visualConfig = null;
        }

        private async Task StartTutorialBoardAsync(HolmasApplicationContext context)
        {
            CoreFindCatTutorialSessionService sessionService = ResolveSessionService(context);
            if (sessionService == null)
            {
                throw new InvalidOperationException("CoreFindCatTutorialCoordinator: 教程会话服务不可用。");
            }

            await SaveSuspendedFormalSessionIfNeededAsync(context);
            if (!sessionService.HasActiveSession)
            {
                await sessionService.StartSessionAsync(context != null ? context.AssetsRuntime : null);
            }
        }

        private CoreFindCatTutorialSessionService ResolveSessionService(HolmasApplicationContext context)
        {
            return _sessionService ??
                   (context != null && context.ServiceContainer != null
                       ? context.ServiceContainer.Get<CoreFindCatTutorialSessionService>()
                       : null);
        }

        private async Task SaveSuspendedFormalSessionIfNeededAsync(HolmasApplicationContext context)
        {
            HolmasGameplayRuntime runtime = context != null ? context.GameplayRuntime : null;
            if (runtime == null ||
                !runtime.HasActiveUncompletedLevel ||
                CoreFindCatTutorialLevelService.IsTutorialLevel(runtime.CurrentLevelSnapshot))
            {
                return;
            }

            HolmasPlayerArchiveSyncService syncService = context.ServiceContainer?.Get<HolmasPlayerArchiveSyncService>();
            if (syncService == null || syncService.TutorialSuspendedSession != null)
            {
                return;
            }

            HolmasPlayerArchiveMapper mapper = context.ServiceContainer?.Get<HolmasPlayerArchiveMapper>() ?? new HolmasPlayerArchiveMapper();
            var suspendedSession = mapper.CreateTutorialSuspendedSession(
                runtime,
                HolmasLocalMockServerGateway.DefaultSchemaVersion,
                "start_core_find_cat_tutorial",
                "tutorial",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (suspendedSession == null)
            {
                return;
            }

            bool saved = await syncService.SaveTutorialSuspendedSessionAsync(suspendedSession, "start_core_find_cat_tutorial");
            if (!saved)
            {
                context.Logger?.LogWarning("CoreFindCatTutorialCoordinator: 保存教程挂起正式棋盘失败，异常重启时将回退到普通存档恢复。");
            }
        }

        private async Task MarkStartedAsync(int stepIndex, bool force)
        {
            TutorialStepDefinition step = CoreFindCatTutorialSteps.Get(stepIndex);
            if (_progressService != null && step != null)
            {
                await _progressService.MarkStartedAsync(step.StepIndex, step.StepId, force);
            }
        }

        private static int ResolveProgressStepIndex(CoreFindCatTutorialProgress progress, int fallback)
        {
            if (progress == null)
            {
                return CoreFindCatTutorialSteps.ClampIndex(fallback);
            }

            if (progress.currentStepIndex >= 0)
            {
                return CoreFindCatTutorialSteps.ClampIndex(progress.currentStepIndex);
            }

            int fromId = CoreFindCatTutorialSteps.IndexOf(progress.currentStepId);
            if (fromId < 0)
            {
                fromId = CoreFindCatTutorialSteps.IndexOf(progress.lastStepId);
            }

            return fromId >= 0 ? fromId : CoreFindCatTutorialSteps.ClampIndex(fallback);
        }

        private static bool ShouldResumePostTutorialBoardSteps(CoreFindCatTutorialProgress progress)
        {
            if (progress == null || progress.completed)
            {
                return false;
            }

            int energyStepIndex = CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId);
            int continueFindStepIndex = CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.ContinueFindStepId);
            int progressStepIndex = ResolveRawProgressStepIndex(progress);
            return progressStepIndex >= energyStepIndex ||
                   progress.completedStepIndex >= continueFindStepIndex;
        }

        private static bool RequiresTutorialBoardSession(int stepIndex, TutorialStepDefinition step)
        {
            if (step != null && step.RequiresTutorialBoard)
            {
                return true;
            }

            int continueFindStepIndex = CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.ContinueFindStepId);
            return stepIndex >= 0 && continueFindStepIndex >= 0 && stepIndex <= continueFindStepIndex;
        }

        private static int ResolveRawProgressStepIndex(CoreFindCatTutorialProgress progress)
        {
            if (progress == null)
            {
                return -1;
            }

            if (progress.currentStepIndex >= 0)
            {
                return progress.currentStepIndex;
            }

            int fromId = CoreFindCatTutorialSteps.IndexOf(progress.currentStepId);
            if (fromId < 0)
            {
                fromId = CoreFindCatTutorialSteps.IndexOf(progress.lastStepId);
            }

            return fromId;
        }

        private TutorialOverlayPayload BuildPayload(
            TutorialRunMode runMode,
            int initialStepIndex,
            bool canWriteCompletion,
            TutorialVisualConfig visualConfig,
            IAssetsRuntime assetsRuntime)
        {
            return new TutorialOverlayPayload
            {
                ProgressService = _progressService,
                RunMode = runMode,
                InitialStepIndex = CoreFindCatTutorialSteps.ClampIndex(initialStepIndex),
                CanWriteCompletion = canWriteCompletion,
                TutorialSessionService = _sessionService,
                VisualConfig = visualConfig,
                AssetsRuntime = assetsRuntime,
            };
        }

        private async Task<TutorialVisualConfig> LoadVisualConfigAsync(HolmasApplicationContext context)
        {
            if (_visualConfig != null)
            {
                return _visualConfig;
            }

            IAssetsRuntime assetsRuntime = context != null ? context.AssetsRuntime : null;
            if (assetsRuntime == null)
            {
                return null;
            }

            try
            {
                _visualConfigHandle = await assetsRuntime.LoadAssetAsync(TutorialVisualConfig.DefaultAssetPath);
                _visualConfig = _visualConfigHandle?.AssetObject as TutorialVisualConfig;
                if (_visualConfig == null)
                {
                    _visualConfigHandle?.Release();
                    _visualConfigHandle = null;
                }
            }
            catch
            {
                _visualConfigHandle?.Release();
                _visualConfigHandle = null;
                _visualConfig = null;
            }

            return _visualConfig;
        }
    }

    public sealed class CoreFindCatTutorialLaunchResult
    {
        public bool ShouldAutoStartNormal;
        public bool ShouldSkipLoadingForAutoStart;
        public bool ShouldShowOverlay;
        public TutorialOverlayPayload Payload;

        public static CoreFindCatTutorialLaunchResult AutoStartNormal(bool skipLoading = false)
        {
            return new CoreFindCatTutorialLaunchResult
            {
                ShouldAutoStartNormal = true,
                ShouldSkipLoadingForAutoStart = skipLoading,
            };
        }

        public static CoreFindCatTutorialLaunchResult ShowOverlay(TutorialOverlayPayload payload)
        {
            return new CoreFindCatTutorialLaunchResult
            {
                ShouldShowOverlay = true,
                Payload = payload,
            };
        }
    }
}
