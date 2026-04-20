using System;
using UnityEngine;

namespace App.HotUpdate.Holmas.Board
{
    /// <summary>
    /// 单个格子的运行时可视状态。
    /// UI 只需要读这个状态，不需要知道内部生成细节。
    /// </summary>
    [Serializable]
    public readonly struct BoardCellState
    {
        public BoardCellState(
            int cellIndex,
            bool isValid,
            bool isRevealed,
            bool isFlagged,
            bool hasCat,
            string catId,
            int adjacentCatCount,
            Color32 blockColor)
        {
            CellIndex = cellIndex;
            IsValid = isValid;
            IsRevealed = isRevealed;
            IsFlagged = isFlagged;
            HasCat = hasCat;
            CatId = catId ?? string.Empty;
            AdjacentCatCount = adjacentCatCount;
            BlockColor = blockColor;
        }

        public int CellIndex { get; }

        public bool IsValid { get; }

        public bool IsRevealed { get; }

        public bool IsFlagged { get; }

        public bool HasCat { get; }

        public string CatId { get; }

        public int AdjacentCatCount { get; }

        public Color32 BlockColor { get; }

        public bool IsFoundCat => IsValid && IsRevealed && HasCat;
    }
}
