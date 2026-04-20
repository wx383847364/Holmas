using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.FindCat
{
    public sealed class FindCatBoardView : MonoBehaviour
    {
        private readonly List<BattleCellView> _cells = new List<BattleCellView>();
        private HolmasCatSpriteLoader _catSpriteLoader;

        public void SetCatSpriteLoader(HolmasCatSpriteLoader catSpriteLoader)
        {
            _catSpriteLoader = catSpriteLoader;
        }

        public void Render(
            int rows,
            int cols,
            IReadOnlyList<BoardCellState> cells,
            IReadOnlyDictionary<string, HolmasCatVisualVm> catVisuals,
            Action<int, bool> onInteract)
        {
            RectTransform boardRect = gameObject.GetComponent<RectTransform>();
            if (boardRect == null)
            {
                boardRect = gameObject.AddComponent<RectTransform>();
            }

            GridLayoutGroup layout = gameObject.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<GridLayoutGroup>();
            }

            rows = Mathf.Max(0, rows);
            cols = Mathf.Max(0, cols);
            if (rows <= 0 || cols <= 0 || cells == null)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    _cells[i].gameObject.SetActive(false);
                }
                return;
            }

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = cols;
            layout.spacing = new Vector2(4f, 4f);
            layout.childAlignment = TextAnchor.MiddleCenter;

            float width = boardRect.rect.width > 0f ? boardRect.rect.width : 720f;
            float height = boardRect.rect.height > 0f ? boardRect.rect.height : 960f;
            float cellWidth = (width - (cols - 1) * layout.spacing.x) / cols;
            float cellHeight = (height - (rows - 1) * layout.spacing.y) / rows;
            float cellSize = Mathf.Max(18f, Mathf.Min(cellWidth, cellHeight));
            layout.cellSize = new Vector2(cellSize, cellSize);

            SetCellCount(cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                if (i < cells.Count)
                {
                    _cells[i].gameObject.SetActive(true);
                    HolmasCatVisualVm visual = null;
                    if (catVisuals != null &&
                        !string.IsNullOrWhiteSpace(cells[i].CatId) &&
                        catVisuals.TryGetValue(cells[i].CatId, out HolmasCatVisualVm resolvedVisual))
                    {
                        visual = resolvedVisual;
                    }

                    _cells[i].Bind(cells[i], visual, _catSpriteLoader, onInteract);
                }
                else
                {
                    _cells[i].gameObject.SetActive(false);
                }
            }
        }

        private void SetCellCount(int requiredCount)
        {
            while (_cells.Count < requiredCount)
            {
                GameObject cellObject = new GameObject($"Cell_{_cells.Count}", typeof(RectTransform), typeof(Image), typeof(BattleCellView));
                cellObject.transform.SetParent(transform, false);
                BattleCellView cellView = cellObject.GetComponent<BattleCellView>();
                _cells.Add(cellView);
            }
        }
    }
}
