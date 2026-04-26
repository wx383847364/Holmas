using App.HotUpdate.Holmas.UI;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.GmTool;
using App.HotUpdate.Holmas.UI.Screens.Loading;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using NUnit.Framework;

namespace Holmas.Tests
{
    public sealed class HolmasUiScreenFlowTests
    {
        [Test]
        public void HolmasUiScreenCatalog_DefaultStartupScreen_UsesStartupLoadingPage()
        {
            Assert.That(
                HolmasUiScreenCatalog.DefaultStartupScreenId,
                Is.EqualTo(LoadingScreenRegistration.StartupPageScreenId));
        }

        [Test]
        public void LoadingScreenRegistration_ExposesStartupPageAndTransitionOverlay()
        {
            UiScreenDefinition startupDefinition = LoadingScreenRegistration.CreateStartupPageDefinition();
            UiScreenDefinition transitionDefinition = LoadingScreenRegistration.CreateTransitionOverlayDefinition();

            Assert.That(startupDefinition.Id, Is.EqualTo(LoadingScreenRegistration.StartupPageScreenId));
            Assert.That(startupDefinition.Kind, Is.EqualTo(UiScreenKind.Page));
            Assert.That(startupDefinition.ControllerType, Is.EqualTo(typeof(LoadingPageController)));
            Assert.That(startupDefinition.PreloadOnBootstrap, Is.True);
            Assert.That(startupDefinition.CachePolicy, Is.EqualTo(UiCachePolicy.DestroyOnClose));
            Assert.That(startupDefinition.BindingManifest, Is.SameAs(LoadingGeneratedBindings.Manifest));

            Assert.That(transitionDefinition.Id, Is.EqualTo(LoadingScreenRegistration.TransitionOverlayScreenId));
            Assert.That(transitionDefinition.Kind, Is.EqualTo(UiScreenKind.Overlay));
            Assert.That(transitionDefinition.ControllerType, Is.EqualTo(typeof(LoadingOverlayController)));
            Assert.That(transitionDefinition.CachePolicy, Is.EqualTo(UiCachePolicy.KeepInstance));
            Assert.That(transitionDefinition.AssetAddress, Is.EqualTo(startupDefinition.AssetAddress));
            Assert.That(transitionDefinition.BindingManifest, Is.SameAs(LoadingGeneratedBindings.Manifest));
        }

        [Test]
        public void HolmasFlowCoordinator_CalculatesRemainingMinimumLoadingDuration()
        {
            Assert.That(
                HolmasFlowCoordinator.CalculateRemainingLoadingVisibleSeconds(0f),
                Is.EqualTo(HolmasFlowCoordinator.MinimumLoadingVisibleSeconds));
            Assert.That(
                HolmasFlowCoordinator.CalculateRemainingLoadingVisibleSeconds(1.2f),
                Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(
                HolmasFlowCoordinator.CalculateRemainingLoadingVisibleSeconds(2f),
                Is.EqualTo(0f));
            Assert.That(
                HolmasFlowCoordinator.CalculateRemainingLoadingVisibleSeconds(3.5f),
                Is.EqualTo(0f));
        }

        [Test]
        public void StartupPreloadDefinitions_KeepLoadingAndMainInBootstrapSet()
        {
            UiScreenDefinition loadingDefinition = LoadingScreenRegistration.CreateStartupPageDefinition();
            UiScreenDefinition mainDefinition = MainScreenRegistration.CreateDefinition();

            Assert.That(HolmasUiScreenCatalog.DefaultStartupScreenId, Is.EqualTo(loadingDefinition.Id));
            Assert.That(loadingDefinition.PreloadOnBootstrap, Is.True);
            Assert.That(mainDefinition.PreloadOnBootstrap, Is.True);
            Assert.That(mainDefinition.AssetAddress, Is.Not.EqualTo(loadingDefinition.AssetAddress));
        }

        [Test]
        public void LoadingVm_DefaultsAnimateTowardTwoSecondNearlyCompleteProgress()
        {
            var viewModel = new LoadingVm();

            Assert.That(viewModel.Animate, Is.True);
            Assert.That(viewModel.TargetProgress, Is.EqualTo(0.95f));
            Assert.That(viewModel.AnimationDurationSeconds, Is.EqualTo(HolmasFlowCoordinator.MinimumLoadingVisibleSeconds));
        }

        [Test]
        public void TutorialScreenRegistration_ExposesNonBlockingOverlay()
        {
            UiScreenDefinition definition = TutorialScreenRegistration.CreateDefinition();

            Assert.That(definition.Id, Is.EqualTo(TutorialScreenRegistration.ScreenId));
            Assert.That(definition.Kind, Is.EqualTo(UiScreenKind.Overlay));
            Assert.That(definition.ControllerType, Is.EqualTo(typeof(TutorialOverlayController)));
            Assert.That(definition.CachePolicy, Is.EqualTo(UiCachePolicy.KeepInstance));
            Assert.That(definition.BlockInputDuringTransition, Is.False);
            Assert.That(definition.AssetAddress, Is.Empty);
        }

        [Test]
        public void GmToolScreenRegistration_ExposesDebugPopup()
        {
            UiScreenDefinition definition = GmToolScreenRegistration.CreateDefinition();

            Assert.That(definition.Id, Is.EqualTo(GmToolScreenRegistration.ScreenId));
            Assert.That(definition.Kind, Is.EqualTo(UiScreenKind.Popup));
            Assert.That(definition.ControllerType, Is.EqualTo(typeof(GmToolPopupController)));
            Assert.That(definition.CachePolicy, Is.EqualTo(UiCachePolicy.KeepInstance));
            Assert.That(definition.ClickOutsideToClose, Is.True);
            Assert.That(definition.AssetAddress, Is.Empty);
        }
    }
}
