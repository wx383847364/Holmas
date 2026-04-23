using System;

namespace App.Shared.Holmas.RuntimeData
{
    /// <summary>
    /// 单只已生成猫的数据。
    /// 它只表示“哪只猫被放到了哪个格子”，不负责决定为什么会生成这只猫。
    /// </summary>
    [Serializable]
    public sealed class SpawnedCatData
    {
        /// <summary>
        /// 已解析的猫种类标识，对应配置表里的 catId。
        /// 普通棋盘生成时可以为空，表示该猫位尚未揭示，猫种仍是盲盒。
        /// </summary>
        public string CatId = string.Empty;

        /// <summary>
        /// 猫所在的一维格子索引。
        /// 本项目跨层统一使用 cellIndex，不额外定义 row/col DTO。
        /// </summary>
        public int CellIndex;
    }
}
