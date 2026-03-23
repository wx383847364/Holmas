using System;
using UnityEngine;

/// <summary>
/// 扫雷地形数据：行列数、每格是否有效、每格颜色。
/// 可保存为 ScriptableObject 资产，供地形编辑器编辑、供游戏加载。
/// </summary>
[CreateAssetMenu(fileName = "Terrain", menuName = "Minesweeper/地形数据", order = 1)]
public class MinesweeperTerrainData : ScriptableObject
{
    [Serializable]
    public struct CellData
    {
        public bool isValid;
        public Color32 blockColor;
    }

    [SerializeField] private int rows = 9;
    [SerializeField] private int cols = 9;
    [SerializeField] private CellData[] cells = new CellData[0];

    public int Rows => rows;
    public int Cols => cols;

    public int GetValidCellCount()
    {
        int n = 0;
        for (int i = 0; i < rows * cols; i++)
            if (cells[i].isValid) n++;
        return n;
    }

    public void Resize(int newRows, int newCols)
    {
        newRows = Mathf.Clamp(newRows, 1, 64);
        newCols = Mathf.Clamp(newCols, 1, 64);
        var old = cells;
        int oldRows = rows, oldCols = cols;
        rows = newRows;
        cols = newCols;
        cells = new CellData[rows * cols];
        Color32 defaultColor = new Color32(180, 180, 180, 255);
        for (int i = 0; i < rows * cols; i++)
        {
            int r = i / cols, c = i % cols;
            if (r < oldRows && c < oldCols && old != null)
            {
                int oldIdx = r * oldCols + c;
                if (oldIdx < old.Length)
                    cells[i] = old[oldIdx];
                else
                    cells[i] = new CellData { isValid = true, blockColor = defaultColor };
            }
            else
                cells[i] = new CellData { isValid = true, blockColor = defaultColor };
        }
    }

    private int Index(int row, int col)
    {
        if (row < 0 || row >= rows || col < 0 || col >= cols) return -1;
        return row * cols + col;
    }

    public bool GetValid(int row, int col)
    {
        int i = Index(row, col);
        return i >= 0 && cells[i].isValid;
    }

    public void SetValid(int row, int col, bool valid)
    {
        int i = Index(row, col);
        if (i >= 0) cells[i].isValid = valid;
    }

    public Color32 GetColor(int row, int col)
    {
        int i = Index(row, col);
        return i >= 0 ? cells[i].blockColor : new Color32(128, 128, 128, 255);
    }

    public void SetColor(int row, int col, Color32 color)
    {
        int i = Index(row, col);
        if (i >= 0) cells[i].blockColor = color;
    }

    public CellData GetCell(int row, int col)
    {
        int i = Index(row, col);
        return i >= 0 ? cells[i] : new CellData { isValid = false, blockColor = new Color32(128, 128, 128, 255) };
    }

    public void SetCell(int row, int col, CellData data)
    {
        int i = Index(row, col);
        if (i >= 0) cells[i] = data;
    }

}
