using App.HotUpdate.Holmas.UI;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.GmTool;
using App.HotUpdate.Holmas.UI.Screens.Loading;
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
