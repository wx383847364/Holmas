using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.Tutorial
{
    public sealed class CoreFindCatTutorialCoordinator : IDisposable
    {
        private readonly CoreFindCatTutorialProgressService _progressService;
        private readonly CoreFindCatTutorialLevelService _levelService;
        private IAssetHandle _visualConfigHandle;
        private TutorialVisualConfig _visualConfig;

        public CoreFindCatTutorialCoordinator(
            CoreFindCatTutorialProgressService progressService,
            CoreFindCatTutorialLevelService levelService)
        {
            _progressService = progressService;
            _levelService = levelService;
        }

        public CoreFindCatTutorialProgressService ProgressService => _progressService;

        public async Task<CoreFindCatTutorialLaunchResult> PrepareAutoStartAsync(HolmasApplicationContext context)
        {
            CoreFindCatTutorialProgress progress = _progressService != null
                ? await _progressService.LoadAsync()
                : new CoreFindCatTutorialProgress();

            if (progress.completed)
            {
                return CoreFindCatTutorialLaunchResult.AutoStartNormal();
            }

            HolmasGameplayRuntime runtime = context != null ? context.GameplayRuntime : null;
            if (runtime != null && runtime.HasActiveUncompletedLevel && runtime.CurrentBoardRuntime != null)
            {
                if (CoreFindCatTutorialLevelService.IsTutorialLevel(runtime.CurrentLevelSnapshot))
                {
                    int stepIndex = ResolveProgressStepIndex(progress, fallback: 0);
                    await MarkStartedAsync(stepIndex, force: false);
                    return CoreFindCatTutorialLaunchResult.ShowOverlay(BuildPayload(
                        TutorialRunMode.FullTutorial,
                        stepIndex,
                        canWriteCompletion: true,
                        await LoadVisualConfigAsync(context),
                        context != null ? context.AssetsRuntime : null));
                }

                if (ShouldResumePostTutorialBoardSteps(progress))
                {
                    int stepIndex = ResolveProgressStepIndex(
                        progress,
                        CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.EnergyStepId));
                    TutorialOverlayPayload payload = BuildPayload(
                        TutorialRunMode.FullTutorial,
                        stepIndex,
                        canWriteCompletion: true,
                        await LoadVisualConfigAsync(context),
                        context != null ? context.AssetsRuntime : null);
                    payload.TutorialBoardObjectiveSatisfied = true;
                    return CoreFindCatTutorialLaunchResult.ShowOverlay(payload);
                }

                return CoreFindCatTutorialLaunchResult.ShowOverlay(BuildPayload(
                    TutorialRunMode.NormalBoardHint,
                    CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.TaskBarStepId),
                    canWriteCompletion: false,
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
            bool hasActiveLevel = runtime != null && runtime.HasActiveUncompletedLevel && runtime.CurrentBoardRuntime != null;
            bool hasTutorialLevel = CoreFindCatTutorialLevelService.IsTutorialLevel(runtime?.CurrentLevelSnapshot);

            if (hasActiveLevel && !hasTutorialLevel && step != null && step.RequiresTutorialBoard)
            {
                return CoreFindCatTutorialLaunchResult.ShowOverlay(BuildPayload(
                    TutorialRunMode.NormalBoardHint,
                    CoreFindCatTutorialSteps.IndexOf(CoreFindCatTutorialSteps.TaskBarStepId),
                    canWriteCompletion: false,
                    await LoadVisualConfigAsync(context),
                    context != null ? context.AssetsRuntime : null));
            }

            if ((!hasActiveLevel || !hasTutorialLevel) && step != null && step.RequiresTutorialBoard)
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
            if (_levelService == null)
            {
                throw new InvalidOperationException("CoreFindCatTutorialCoordinator: 教程关卡服务不可用。");
            }

            await _levelService.StartTutorialBoardAsync(context);
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
        public bool ShouldShowOverlay;
        public TutorialOverlayPayload Payload;

        public static CoreFindCatTutorialLaunchResult AutoStartNormal()
        {
            return new CoreFindCatTutorialLaunchResult
            {
                ShouldAutoStartNormal = true,
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
