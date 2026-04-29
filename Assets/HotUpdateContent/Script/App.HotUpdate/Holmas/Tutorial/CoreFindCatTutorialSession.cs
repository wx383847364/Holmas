using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Terrain;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.Tutorial
{
    public sealed class CoreFindCatTutorialSession
    {
        public CoreFindCatTutorialSession(BoardTemplate template, LevelSnapshot snapshot)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            BoardRuntime = new BoardRuntime(Template, Snapshot);
        }

        public BoardTemplate Template { get; }

        public LevelSnapshot Snapshot { get; }

        public BoardRuntime BoardRuntime { get; }

        public bool TutorialBoardObjectiveSatisfied { get; set; }

        public bool IsTutorialCatRevealed(int cellIndex)
        {
            return BoardRuntime != null && BoardRuntime.IsRevealed(cellIndex);
        }

        public BoardRevealResult RevealCell(int cellIndex)
        {
            BoardRevealResult result = BoardRuntime.Reveal(cellIndex, ignoreFlag: true);
            if (result != null && result.IsValidAction && result.Completed)
            {
                TutorialBoardObjectiveSatisfied = true;
            }

            return result;
        }
    }

    public sealed class CoreFindCatTutorialSessionService
    {
        private readonly IAppLogger _logger;

        public CoreFindCatTutorialSessionService(IAppLogger logger)
        {
            _logger = logger;
        }

        public CoreFindCatTutorialSession ActiveSession { get; private set; }

        public bool SuppressAutoStartOnce { get; private set; }

        public bool HasActiveSession => ActiveSession != null;

        public event Action StateChanged;

        public async Task<CoreFindCatTutorialSession> StartSessionAsync(IAssetsRuntime assetsRuntime)
        {
            ActiveSession = await CoreFindCatTutorialLevelService.CreateTutorialSessionAsync(assetsRuntime);
            _logger?.LogInfo("CoreFindCatTutorialSessionService: 已创建独立教程棋盘。");
            NotifyStateChanged();
            return ActiveSession;
        }

        public BoardRevealResult RevealCell(int cellIndex)
        {
            if (ActiveSession == null)
            {
                return null;
            }

            BoardRevealResult result = ActiveSession.RevealCell(cellIndex);
            if (result != null && result.IsValidAction)
            {
                NotifyStateChanged();
            }

            return result;
        }

        public void ClearSession()
        {
            if (ActiveSession == null)
            {
                return;
            }

            ActiveSession = null;
            _logger?.LogInfo("CoreFindCatTutorialSessionService: 已清理独立教程棋盘。");
            NotifyStateChanged();
        }

        public void SuppressNextAutoStart()
        {
            SuppressAutoStartOnce = true;
        }

        public bool ConsumeAutoStartSuppression()
        {
            if (!SuppressAutoStartOnce)
            {
                return false;
            }

            SuppressAutoStartOnce = false;
            return true;
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
