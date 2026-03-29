using System.Collections.Generic;

namespace App.HotUpdate.Holmas.Board
{
    /// <summary>
    /// 翻开或插旗后的结果。
    /// </summary>
    public sealed class BoardRevealResult
    {
        public BoardRevealResult(int cellIndex)
        {
            CellIndex = cellIndex;
        }

        public int CellIndex { get; }

        public bool IsValidAction { get; set; }

        public bool IsIgnored { get; set; }

        public bool IsFlagAction { get; set; }

        public bool IsCatCell { get; set; }

        public bool FoundCat { get; set; }

        public bool Completed { get; set; }

        public readonly List<int> ChangedCellIndices = new List<int>();

        public readonly List<int> FoundCatCellIndices = new List<int>();
    }
}
