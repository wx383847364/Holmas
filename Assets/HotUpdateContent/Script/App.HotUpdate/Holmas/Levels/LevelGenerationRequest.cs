using System;
using System.Collections.Generic;

namespace App.HotUpdate.Holmas.Levels
{
    /// <summary>
    /// 生成一局地图快照所需的输入。
    /// </summary>
    [Serializable]
    public sealed class LevelGenerationRequest
    {
        /// <summary>
        /// 地图配置标识。
        /// </summary>
        public string MapId = string.Empty;

        /// <summary>
        /// 地形资源路径或 key。
        /// </summary>
        public string TerrainPath = string.Empty;

        /// <summary>
        /// 随机种子。
        /// </summary>
        public int Seed;

        /// <summary>
        /// 本局允许生成的猫总数下限。
        /// </summary>
        public int CatCountMin;

        /// <summary>
        /// 本局允许生成的猫总数上限。
        /// </summary>
        public int CatCountMax;

        /// <summary>
        /// 兼容旧调用的猫种权重池。
        /// 普通棋盘生成不再读取这个字段，猫种会在揭示猫格时从当前未完成任务池解析。
        /// </summary>
        public IReadOnlyList<BoardSpawnEntry> CatPool = Array.Empty<BoardSpawnEntry>();
    }
}
