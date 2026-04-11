using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.Board;

namespace App.HotUpdate.Holmas.UI.Screens.Battle
{
    public sealed class BattleVm
    {
        public string LevelLabel = string.Empty;
        public string GoldLabel = string.Empty;
        public string Summary = string.Empty;
        public string Status = string.Empty;
        public int Rows;
        public int Cols;
        public IReadOnlyList<BoardCellState> Cells = Array.Empty<BoardCellState>();
    }
}
