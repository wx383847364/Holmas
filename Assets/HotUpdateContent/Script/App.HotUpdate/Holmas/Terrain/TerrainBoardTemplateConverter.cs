using System;
using System.Reflection;
using App.Shared.Holmas.RuntimeData;
using UnityEngine;

namespace App.HotUpdate.Holmas.Terrain
{
    /// <summary>
    /// 通过反射把地形资产转换成运行时棋盘模板。
    /// 这样可以不直接依赖 Assembly-CSharp 里的扫雷地形类型，仍然接入现有地形资产。
    /// </summary>
    public static class TerrainBoardTemplateConverter
    {
        /// <summary>
        /// 将一个兼容的地形资产转换成 <see cref="BoardTemplate"/>。
        /// </summary>
        public static BoardTemplate Convert(UnityEngine.Object terrainAsset)
        {
            if (terrainAsset == null)
            {
                throw new ArgumentNullException(nameof(terrainAsset));
            }

            return TerrainReflectionAdapter.From(terrainAsset).ToBoardTemplate();
        }

        /// <summary>
        /// 尝试将地形资产转换成 <see cref="BoardTemplate"/>。
        /// </summary>
        public static bool TryConvert(UnityEngine.Object terrainAsset, out BoardTemplate template)
        {
            template = null;

            if (terrainAsset == null)
            {
                return false;
            }

            try
            {
                template = Convert(terrainAsset);
                return true;
            }
            catch
            {
                template = null;
                return false;
            }
        }

        private sealed class TerrainReflectionAdapter
        {
            private readonly UnityEngine.Object _terrainAsset;
            private readonly PropertyInfo _rowsProperty;
            private readonly PropertyInfo _colsProperty;
            private readonly MethodInfo _getValidMethod;
            private readonly MethodInfo _getColorMethod;

            private TerrainReflectionAdapter(
                UnityEngine.Object terrainAsset,
                PropertyInfo rowsProperty,
                PropertyInfo colsProperty,
                MethodInfo getValidMethod,
                MethodInfo getColorMethod)
            {
                _terrainAsset = terrainAsset;
                _rowsProperty = rowsProperty;
                _colsProperty = colsProperty;
                _getValidMethod = getValidMethod;
                _getColorMethod = getColorMethod;
            }

            public static TerrainReflectionAdapter From(UnityEngine.Object terrainAsset)
            {
                Type type = terrainAsset.GetType();

                PropertyInfo rowsProperty = type.GetProperty("Rows", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo colsProperty = type.GetProperty("Cols", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo getValidMethod = type.GetMethod("GetValid", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo getColorMethod = type.GetMethod("GetColor", BindingFlags.Instance | BindingFlags.Public);

                if (rowsProperty == null || colsProperty == null || getValidMethod == null || getColorMethod == null)
                {
                    throw new InvalidOperationException($"Terrain asset '{type.FullName}' does not expose the expected board template API.");
                }

                return new TerrainReflectionAdapter(terrainAsset, rowsProperty, colsProperty, getValidMethod, getColorMethod);
            }

            public BoardTemplate ToBoardTemplate()
            {
                int rows = (int)_rowsProperty.GetValue(_terrainAsset);
                int cols = (int)_colsProperty.GetValue(_terrainAsset);

                if (rows <= 0 || cols <= 0)
                {
                    throw new InvalidOperationException($"Terrain asset '{_terrainAsset.name}' contains invalid dimensions: {rows}x{cols}.");
                }

                int cellCount = rows * cols;
                var validMask = new bool[cellCount];
                var blockColors = new Color32[cellCount];

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        int cellIndex = row * cols + col;
                        validMask[cellIndex] = InvokeValid(row, col);
                        blockColors[cellIndex] = InvokeColor(row, col);
                    }
                }

                return new BoardTemplate
                {
                    Rows = rows,
                    Cols = cols,
                    ValidMask = validMask,
                    BlockColors = blockColors,
                };
            }

            private bool InvokeValid(int row, int col)
            {
                object value = _getValidMethod.Invoke(_terrainAsset, new object[] { row, col });
                return value is bool b && b;
            }

            private Color32 InvokeColor(int row, int col)
            {
                object value = _getColorMethod.Invoke(_terrainAsset, new object[] { row, col });
                if (value is Color32 color)
                {
                    return color;
                }

                return new Color32(128, 128, 128, 255);
            }
        }
    }
}
