using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Screens.FindCat
{
    public readonly struct BoardFrameLayout
    {
        public readonly bool IsValid;
        public readonly Vector2 ContainerOffsetMin;
        public readonly Vector2 ContainerOffsetMax;
        public readonly Vector2 CellSize;
        public readonly Vector2 Spacing;
        public readonly Vector2 ContentSize;

        public BoardFrameLayout(
            Vector2 containerOffsetMin,
            Vector2 containerOffsetMax,
            Vector2 cellSize,
            Vector2 spacing,
            Vector2 contentSize)
        {
            IsValid = true;
            ContainerOffsetMin = containerOffsetMin;
            ContainerOffsetMax = containerOffsetMax;
            CellSize = cellSize;
            Spacing = spacing;
            ContentSize = contentSize;
        }
    }

    public static class BoardFrameLayoutCalculator
    {
        public const float DefaultMinimumSpacing = 4f;

        public static BoardFrameLayout Calculate(
            Vector2 contentSize,
            int rows,
            int cols,
            float minimumSpacing = DefaultMinimumSpacing)
        {
            float availableWidth = Mathf.Max(0f, contentSize.x);
            float availableHeight = Mathf.Max(0f, contentSize.y);
            float cellSize = 0f;
            float spacingX = 0f;
            float spacingY = 0f;

            if (rows > 0 && cols > 0 && availableWidth > 0f && availableHeight > 0f)
            {
                float safeMinimumSpacing = Mathf.Max(0f, minimumSpacing);
                float cellWithMinimumSpacingX = cols > 1
                    ? (availableWidth - safeMinimumSpacing * (cols - 1)) / cols
                    : availableWidth / cols;
                float cellWithMinimumSpacingY = rows > 1
                    ? (availableHeight - safeMinimumSpacing * (rows - 1)) / rows
                    : availableHeight / rows;
                bool canKeepMinimumSpacing = cellWithMinimumSpacingX > 0f && cellWithMinimumSpacingY > 0f;
                cellSize = canKeepMinimumSpacing
                    ? Mathf.Min(cellWithMinimumSpacingX, cellWithMinimumSpacingY)
                    : Mathf.Min(availableWidth / cols, availableHeight / rows);
                cellSize = Mathf.Max(0f, cellSize);
                spacingX = cols > 1 ? Mathf.Max(0f, (availableWidth - cellSize * cols) / (cols - 1)) : 0f;
                spacingY = rows > 1 ? Mathf.Max(0f, (availableHeight - cellSize * rows) / (rows - 1)) : 0f;
            }

            return new BoardFrameLayout(
                Vector2.zero,
                Vector2.zero,
                new Vector2(cellSize, cellSize),
                new Vector2(spacingX, spacingY),
                new Vector2(availableWidth, availableHeight));
        }
    }
}
