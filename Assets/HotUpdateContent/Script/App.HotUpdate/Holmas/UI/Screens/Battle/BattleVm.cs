using System.Collections.Generic;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.UI.Core;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleVm
    {
        public string LevelLabel = string.Empty;
        public string GoldLabel = string.Empty;
        public string EnergyLabel = string.Empty;
        public string Summary = string.Empty;
        public string Status = string.Empty;
        public int Rows;
        public int Cols;
        public IReadOnlyList<BoardCellState> Cells = new BoardCellState[0];
        public IReadOnlyDictionary<string, HolmasCatVisualVm> CatVisuals = HolmasCatVisualVm.EmptyLookup;
    }
}
