using System;
using System.Collections.Generic;
using App.Shared.Holmas.RuntimeData;
using UnityEngine;

namespace App.HotUpdate.Holmas.Board
{
    /// <summary>
    /// 单局棋盘运行时状态。
    /// 负责数字计算、揭示扩散、猫发现和通关判定。
    /// </summary>
    public sealed class BoardRuntime
    {
        private readonly BoardTemplate _template;
        private readonly LevelSnapshot _snapshot;
        private readonly bool[] _validMask;
        private readonly bool[] _revealedCells;
        private readonly bool[] _flaggedCells;
        private readonly bool[] _catCells;
        private readonly string[] _catIds;
        private readonly int[] _adjacentCatCounts;
        private readonly Color32[] _blockColors;
        private readonly int _rows;
        private readonly int _cols;
        private int _foundCatCount;

        public BoardRuntime(BoardTemplate template, LevelSnapshot snapshot)
        {
            _template = template ?? throw new ArgumentNullException(nameof(template));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

            _rows = Math.Max(0, template.Rows);
            _cols = Math.Max(0, template.Cols);

            int cellCount = _rows * _cols;
            _validMask = NormalizeBoolArray(template.ValidMask, cellCount);
            _blockColors = NormalizeColorArray(template.BlockColors, cellCount);
            _revealedCells = NormalizeBoolArray(snapshot.RevealedCells, cellCount);
            _flaggedCells = new bool[cellCount];
            _catCells = new bool[cellCount];
            _catIds = new string[cellCount];
            _adjacentCatCounts = new int[cellCount];

            if (snapshot.SpawnedCats != null)
            {
                for (int i = 0; i < snapshot.SpawnedCats.Count; i++)
                {
                    SpawnedCatData cat = snapshot.SpawnedCats[i];
                    if (cat == null)
                    {
                        continue;
                    }

                    if (cat.CellIndex < 0 || cat.CellIndex >= cellCount)
                    {
                        continue;
                    }

                    if (!_validMask[cat.CellIndex])
                    {
                        continue;
                    }

                    _catCells[cat.CellIndex] = true;
                    _catIds[cat.CellIndex] = cat.CatId ?? string.Empty;
                }
            }

            CalculateAdjacentCatCounts();
            RecountFoundCats();
            SyncSnapshot();
        }

        public BoardTemplate Template => _template;

        public LevelSnapshot Snapshot => _snapshot;

        public int Rows => _rows;

        public int Cols => _cols;

        public int CellCount => _rows * _cols;

        public int TotalCatCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _catCells.Length; i++)
                {
                    if (_catCells[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int FoundCatCount => _foundCatCount;

        public bool Completed => _snapshot.Completed;

        public BoardCellState GetCellState(int cellIndex)
        {
            if (!IsCellIndexValid(cellIndex))
            {
                return default;
            }

            return new BoardCellState(
                cellIndex,
                _validMask[cellIndex],
                _revealedCells[cellIndex],
                _flaggedCells[cellIndex],
                _catCells[cellIndex],
                _catIds[cellIndex],
                _adjacentCatCounts[cellIndex],
                _blockColors[cellIndex]);
        }

        public IReadOnlyList<BoardCellState> GetAllCellStates()
        {
            var states = new BoardCellState[CellCount];
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = GetCellState(i);
            }

            return states;
        }

        public bool HasCatAt(int cellIndex)
        {
            return IsCellIndexValid(cellIndex) && _catCells[cellIndex];
        }

        public bool TryGetCatIdAt(int cellIndex, out string catId)
        {
            if (!IsCellIndexValid(cellIndex) || !_catCells[cellIndex] || string.IsNullOrWhiteSpace(_catIds[cellIndex]))
            {
                catId = string.Empty;
                return false;
            }

            catId = _catIds[cellIndex];
            return true;
        }

        public IReadOnlyList<SpawnedCatData> GetFoundCats(IReadOnlyList<int> cellIndices)
        {
            if (cellIndices == null || cellIndices.Count == 0)
            {
                return Array.Empty<SpawnedCatData>();
            }

            var foundCats = new List<SpawnedCatData>(cellIndices.Count);
            for (int i = 0; i < cellIndices.Count; i++)
            {
                int cellIndex = cellIndices[i];
                if (!TryGetCatIdAt(cellIndex, out string catId))
                {
                    continue;
                }

                foundCats.Add(new SpawnedCatData
                {
                    CellIndex = cellIndex,
                    CatId = catId,
                });
            }

            return foundCats;
        }

        public bool IsRevealed(int cellIndex)
        {
            return IsCellIndexValid(cellIndex) && _revealedCells[cellIndex];
        }

        public bool IsFlagged(int cellIndex)
        {
            return IsCellIndexValid(cellIndex) && _flaggedCells[cellIndex];
        }

        public void ClearFlags()
        {
            bool changed = false;
            for (int i = 0; i < _flaggedCells.Length; i++)
            {
                if (!_flaggedCells[i])
                {
                    continue;
                }

                _flaggedCells[i] = false;
                changed = true;
            }

            if (changed)
            {
                SyncSnapshot();
            }
        }

        public BoardRevealResult ToggleFlag(int cellIndex)
        {
            var result = new BoardRevealResult(cellIndex)
            {
                IsValidAction = false,
                IsIgnored = true,
                IsFlagAction = true,
            };

            if (Completed)
            {
                return result;
            }

            if (!IsCellIndexValid(cellIndex) || !_validMask[cellIndex] || _revealedCells[cellIndex])
            {
                return result;
            }

            _flaggedCells[cellIndex] = !_flaggedCells[cellIndex];
            result.IsValidAction = true;
            result.IsIgnored = false;
            SyncSnapshot();
            return result;
        }

        public BoardRevealResult Reveal(int cellIndex)
        {
            return Reveal(cellIndex, ignoreFlag: false);
        }

        public BoardRevealResult Reveal(int cellIndex, bool ignoreFlag)
        {
            var result = new BoardRevealResult(cellIndex)
            {
                IsValidAction = false,
                IsIgnored = true,
            };

            if (Completed)
            {
                return result;
            }

            if (!IsCellIndexValid(cellIndex) ||
                !_validMask[cellIndex] ||
                _revealedCells[cellIndex] ||
                (!ignoreFlag && _flaggedCells[cellIndex]))
            {
                return result;
            }

            if (ignoreFlag && _flaggedCells[cellIndex])
            {
                _flaggedCells[cellIndex] = false;
            }

            result.IsValidAction = true;
            result.IsIgnored = false;
            result.IsCatCell = _catCells[cellIndex];

            if (_catCells[cellIndex])
            {
                RevealSingle(cellIndex, result);
                result.FoundCat = true;
                result.FoundCatCellIndices.Add(cellIndex);
            }
            else if (_adjacentCatCounts[cellIndex] == 0)
            {
                RevealFloodFill(cellIndex, result);
            }
            else
            {
                RevealSingle(cellIndex, result);
            }

            RecountFoundCats();
            UpdateCompletion(result);
            SyncSnapshot();
            return result;
        }

        public bool TryRevealAndGetCompleted(int cellIndex, out BoardRevealResult result)
        {
            result = Reveal(cellIndex);
            return result.IsValidAction;
        }

        public BoardCellState[] SnapshotCells()
        {
            var states = new BoardCellState[CellCount];
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = GetCellState(i);
            }

            return states;
        }

        private void RevealSingle(int cellIndex, BoardRevealResult result)
        {
            if (_revealedCells[cellIndex])
            {
                return;
            }

            _revealedCells[cellIndex] = true;
            result.ChangedCellIndices.Add(cellIndex);
        }

        private void RevealFloodFill(int startCellIndex, BoardRevealResult result)
        {
            var queue = new Queue<int>();
            queue.Enqueue(startCellIndex);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!IsCellIndexValid(current) || !_validMask[current] || _revealedCells[current] || _flaggedCells[current])
                {
                    continue;
                }

                if (_catCells[current])
                {
                    continue;
                }

                _revealedCells[current] = true;
                result.ChangedCellIndices.Add(current);

                if (_adjacentCatCounts[current] > 0)
                {
                    continue;
                }

                int row = BoardIndexUtility.RowOf(current, _cols);
                int col = BoardIndexUtility.ColOf(current, _cols);
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0)
                        {
                            continue;
                        }

                        int nextRow = row + dr;
                        int nextCol = col + dc;
                        if (!BoardIndexUtility.InBounds(nextRow, nextCol, _rows, _cols))
                        {
                            continue;
                        }

                        int nextCellIndex = BoardIndexUtility.ToCellIndex(nextRow, nextCol, _cols);
                        if (_validMask[nextCellIndex] && !_revealedCells[nextCellIndex] && !_flaggedCells[nextCellIndex])
                        {
                            queue.Enqueue(nextCellIndex);
                        }
                    }
                }
            }
        }

        private void CalculateAdjacentCatCounts()
        {
            for (int cellIndex = 0; cellIndex < CellCount; cellIndex++)
            {
                if (!_validMask[cellIndex] || _catCells[cellIndex])
                {
                    continue;
                }

                int row = BoardIndexUtility.RowOf(cellIndex, _cols);
                int col = BoardIndexUtility.ColOf(cellIndex, _cols);
                int count = 0;

                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0)
                        {
                            continue;
                        }

                        int nextRow = row + dr;
                        int nextCol = col + dc;
                        if (!BoardIndexUtility.InBounds(nextRow, nextCol, _rows, _cols))
                        {
                            continue;
                        }

                        int nextCellIndex = BoardIndexUtility.ToCellIndex(nextRow, nextCol, _cols);
                        if (_validMask[nextCellIndex] && _catCells[nextCellIndex])
                        {
                            count++;
                        }
                    }
                }

                _adjacentCatCounts[cellIndex] = count;
            }
        }

        private void RecountFoundCats()
        {
            int count = 0;
            for (int i = 0; i < CellCount; i++)
            {
                if (_catCells[i] && _revealedCells[i])
                {
                    count++;
                }
            }

            _foundCatCount = count;
        }

        private void UpdateCompletion(BoardRevealResult result)
        {
            bool completed = TotalCatCount == 0 || _foundCatCount >= TotalCatCount;
            _snapshot.Completed = completed;
            result.Completed = completed;
        }

        private void SyncSnapshot()
        {
            if (_snapshot.RevealedCells == null || _snapshot.RevealedCells.Length != CellCount)
            {
                _snapshot.RevealedCells = new bool[CellCount];
            }

            Array.Copy(_revealedCells, _snapshot.RevealedCells, CellCount);
            _snapshot.Completed = TotalCatCount == 0 || _foundCatCount >= TotalCatCount;
        }

        private bool IsCellIndexValid(int cellIndex)
        {
            return cellIndex >= 0 && cellIndex < CellCount;
        }

        private static bool[] NormalizeBoolArray(bool[] source, int length)
        {
            var result = new bool[length];
            if (source == null)
            {
                return result;
            }

            int count = Math.Min(source.Length, length);
            Array.Copy(source, result, count);
            return result;
        }

        private static Color32[] NormalizeColorArray(Color32[] source, int length)
        {
            var result = new Color32[length];
            Color32 defaultColor = new Color32(180, 180, 180, 255);
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = defaultColor;
            }

            if (source == null)
            {
                return result;
            }

            int count = Math.Min(source.Length, length);
            Array.Copy(source, result, count);
            return result;
        }
    }
}
