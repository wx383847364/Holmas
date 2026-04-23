using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.Shared.Contracts;

namespace App.HotUpdate.Holmas.UI.Screens.Tutorial
{
    public sealed class TutorialOverlayPayload
    {
        public MainView MainView;
        public CoreFindCatTutorialProgressStore ProgressStore;
        public CoreFindCatTutorialProgressService ProgressService;
        public string InitialStepId;
        public int InitialStepIndex = -1;
        public bool ForceReplay;
        public TutorialRunMode RunMode = TutorialRunMode.FullTutorial;
        public bool CanWriteCompletion = true;
        public TutorialVisualConfig VisualConfig;
        public IAssetsRuntime AssetsRuntime;
    }
}
