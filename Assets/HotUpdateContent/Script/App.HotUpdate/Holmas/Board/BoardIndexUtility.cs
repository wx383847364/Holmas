namespace App.HotUpdate.Holmas.Board
{
    /// <summary>
    /// 棋盘索引和邻接工具。
    /// </summary>
    public static class BoardIndexUtility
    {
        public static int ToCellIndex(int row, int col, int cols)
        {
            return row * cols + col;
        }

        public static bool InBounds(int row, int col, int rows, int cols)
        {
            return row >= 0 && row < rows && col >= 0 && col < cols;
        }

        public static int RowOf(int cellIndex, int cols)
        {
            return cellIndex / cols;
        }

        public static int ColOf(int cellIndex, int cols)
        {
            return cellIndex % cols;
        }
    }
}
