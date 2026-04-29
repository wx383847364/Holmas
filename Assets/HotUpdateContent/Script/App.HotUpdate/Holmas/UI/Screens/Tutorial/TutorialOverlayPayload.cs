using App.HotUpdate.Holmas.Tutorial;
using App.HotUpdate.Holmas.UI.Screens.Main;
using App.Shared.Contracts;
using System;
using System.Threading.Tasks;

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
        public bool TutorialBoardObjectiveSatisfied;
        public CoreFindCatTutorialSessionService TutorialSessionService;
        public TutorialVisualConfig VisualConfig;
        public IAssetsRuntime AssetsRuntime;
        public Func<Task> OnTutorialExitedAsync;
    }
}
