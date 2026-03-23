using System;
using System.Collections.Generic;

/// <summary>
/// 扫雷核心逻辑：雷区数据、生成、翻开、插旗、胜负判定。
/// 不依赖 Unity，可在编辑器中测试。
/// </summary>
public class MinesweeperGame
{
    /// <summary>难度预设：行数, 列数, 雷数</summary>
    public static readonly (int rows, int cols, int mines)[] Difficulties =
    {
        (9, 9, 10),   // 初级
        (16, 16, 40), // 中级
        (30, 30, 99)  // 高级
    };

    public enum GameState
    {
        NotStarted, // 未开始（尚未第一次点击）
        Playing,
        Won,
        Lost
    }

    /// <summary>单个格子的对外状态（只读）</summary>
    public struct CellState
    {
        public bool IsMine;
        public int AdjacentCount; // 周围雷数 0~8
        public bool IsRevealed;
        public bool IsFlagged;
        /// <summary>失败时：此处插了旗但不是雷</summary>
        public bool IsWrongFlag;
        /// <summary>是否有效地块（来自地形时有效才可玩）</summary>
        public bool IsValid;
        /// <summary>地块颜色（来自地形时使用）</summary>
        public UnityEngine.Color32 BlockColor;
    }

    private struct Cell
    {
        public bool IsMine;
        public int AdjacentCount;
        public bool IsRevealed;
        public bool IsFlagged;
    }

    private Cell[,] _grid;
    private int _rows;
    private int _cols;
    private int _mineCount;
    private int _totalCells;
    private int _validCellCount; // 有效地块数（有地形时只计有效格）
    private MinesweeperTerrainData _terrain;
    private bool _minesPlaced;
    private int _revealedCount;
    private int _flagCount;

    public int Rows => _rows;
    public int Cols => _cols;
    public int MineCount => _mineCount;
    public GameState State { get; private set; }
    public int FlagCount => _flagCount;
    public int RemainingMines => _mineCount - _flagCount;

    /// <summary>某格状态变化（需要刷新显示）</summary>
    public event Action<int, int> OnCellChanged;
    /// <summary>游戏结束，参数为是否胜利</summary>
    public event Action<bool> OnGameOver;
    /// <summary>雷已布好（首次点击后），可用于开始计时</summary>
    public event Action OnMinesPlaced;
    /// <summary>插旗数变化，用于更新剩余雷数显示</summary>
    public event Action OnFlagCountChanged;

    /// <summary>按难度初始化棋盘（不布雷，等首次点击再布）</summary>
    public void Init(int difficultyIndex)
    {
        if (difficultyIndex < 0 || difficultyIndex >= Difficulties.Length)
            difficultyIndex = 0;
        var (r, c, m) = Difficulties[difficultyIndex];
        Init(r, c, m);
    }

    public void Init(int rows, int cols, int mineCount)
    {
        _terrain = null;
        _rows = rows;
        _cols = cols;
        _totalCells = rows * cols;
        _validCellCount = _totalCells;
        _mineCount = Math.Min(mineCount, _validCellCount - 1);
        _grid = new Cell[rows, cols];
        _minesPlaced = false;
        _revealedCount = 0;
        _flagCount = 0;
        State = GameState.NotStarted;
        for (int i = 0; i < rows; i++)
        for (int j = 0; j < cols; j++)
            _grid[i, j] = new Cell { IsMine = false, AdjacentCount = 0, IsRevealed = false, IsFlagged = false };
    }

    /// <summary>从地形数据初始化：仅有效地块可玩，雷只布在有效格上。</summary>
    public void InitFromTerrain(MinesweeperTerrainData terrain, int mineCount)
    {
        if (terrain == null) { Init(9, 9, 10); return; }
        _terrain = terrain;
        _rows = terrain.Rows;
        _cols = terrain.Cols;
        _totalCells = _rows * _cols;
        _validCellCount = 0;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (terrain.GetValid(r, c)) _validCellCount++;
        _mineCount = Math.Min(mineCount, Math.Max(0, _validCellCount - 1));
        _grid = new Cell[_rows, _cols];
        _minesPlaced = false;
        _revealedCount = 0;
        _flagCount = 0;
        State = GameState.NotStarted;
        for (int i = 0; i < _rows; i++)
        for (int j = 0; j < _cols; j++)
            _grid[i, j] = new Cell { IsMine = false, AdjacentCount = 0, IsRevealed = false, IsFlagged = false };
    }

    public CellState GetCellState(int row, int col)
    {
        if (!InBounds(row, col))
            return default;
        var c = _grid[row, col];
        bool valid = IsValidCell(row, col);
        bool wrongFlag = (State == GameState.Lost && c.IsFlagged && !c.IsMine);
        var state = new CellState
        {
            IsMine = c.IsMine,
            AdjacentCount = c.AdjacentCount,
            IsRevealed = c.IsRevealed,
            IsFlagged = c.IsFlagged,
            IsWrongFlag = wrongFlag,
            IsValid = valid,
            BlockColor = _terrain != null ? _terrain.GetColor(row, col) : new UnityEngine.Color32(180, 180, 180, 255)
        };
        return state;
    }

    private bool IsValidCell(int r, int c) => _terrain == null || _terrain.GetValid(r, c);

    /// <summary>左键翻开。首次点击会先布雷（排除该格），再执行翻开。</summary>
    public void Reveal(int row, int col)
    {
        if (!InBounds(row, col) || !IsValidCell(row, col) || State != GameState.NotStarted && State != GameState.Playing)
            return;
        ref Cell cell = ref _grid[row, col];
        if (cell.IsRevealed)
            return;
        if (cell.IsFlagged)
            return; // 插旗格不翻开

        if (!_minesPlaced)
        {
            PlaceMinesExcluding(row, col);
            _minesPlaced = true;
            State = GameState.Playing;
            OnMinesPlaced?.Invoke();
        }

        if (cell.IsMine)
        {
            cell.IsRevealed = true;
            State = GameState.Lost;
            for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (_grid[r, c].IsMine)
                    _grid[r, c].IsRevealed = true;
            RevealAllMinesForDisplay();
            OnGameOver?.Invoke(false);
            return;
        }

        var toUpdate = new List<(int r, int c)>();
        RevealBfs(row, col, toUpdate);
        foreach (var (r, c) in toUpdate)
            NotifyCellChanged(r, c);

        if (_revealedCount >= _validCellCount - _mineCount)
        {
            State = GameState.Won;
            OnGameOver?.Invoke(true);
        }
    }

    private void PlaceMinesExcluding(int excludeRow, int excludeCol)
    {
        var indices = new List<int>();
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (IsValidCell(r, c) && (r != excludeRow || c != excludeCol))
                    indices.Add(r * _cols + c);
        Shuffle(indices);
        int placeCount = Math.Min(_mineCount, indices.Count);
        for (int i = 0; i < placeCount; i++)
        {
            int idx = indices[i];
            int r = idx / _cols;
            int c = idx % _cols;
            _grid[r, c].IsMine = true;
        }
        for (int r = 0; r < _rows; r++)
        for (int c = 0; c < _cols; c++)
            if (!_grid[r, c].IsMine)
                _grid[r, c].AdjacentCount = CountAdjacentMines(r, c);
    }

    private int CountAdjacentMines(int row, int col)
    {
        int count = 0;
        for (int dr = -1; dr <= 1; dr++)
        for (int dc = -1; dc <= 1; dc++)
            if ((dr != 0 || dc != 0) && IsValidCell(row + dr, col + dc) && IsMineAt(row + dr, col + dc))
                count++;
        return count;
    }

    private bool IsMineAt(int r, int c) => InBounds(r, c) && _grid[r, c].IsMine;

    private void RevealBfs(int startRow, int startCol, List<(int r, int c)> toUpdate)
    {
        var q = new Queue<(int r, int c)>();
        q.Enqueue((startRow, startCol));
        while (q.Count > 0)
        {
            var (r, c) = q.Dequeue();
            if (!InBounds(r, c) || !IsValidCell(r, c))
                continue;
            ref Cell cell = ref _grid[r, c];
            if (cell.IsRevealed || cell.IsMine || cell.IsFlagged)
                continue;
            cell.IsRevealed = true;
            _revealedCount++;
            toUpdate.Add((r, c));
            if (cell.AdjacentCount == 0)
            {
                for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    q.Enqueue((r + dr, c + dc));
            }
        }
    }

    private void RevealAllMinesForDisplay()
    {
        for (int r = 0; r < _rows; r++)
        for (int c = 0; c < _cols; c++)
            if (_grid[r, c].IsMine)
                NotifyCellChanged(r, c);
    }

    /// <summary>右键插旗/取消插旗</summary>
    public void ToggleFlag(int row, int col)
    {
        if (!InBounds(row, col) || !IsValidCell(row, col) || State != GameState.NotStarted && State != GameState.Playing)
            return;
        ref Cell cell = ref _grid[row, col];
        if (cell.IsRevealed)
            return;
        cell.IsFlagged = !cell.IsFlagged;
        _flagCount += cell.IsFlagged ? 1 : -1;
        NotifyCellChanged(row, col);
        OnFlagCountChanged?.Invoke();
    }

    private bool InBounds(int r, int c) => r >= 0 && r < _rows && c >= 0 && c < _cols;

    private void NotifyCellChanged(int r, int c) => OnCellChanged?.Invoke(r, c);

    private static void Shuffle<T>(List<T> list)
    {
        var rng = new Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
