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
        /// 当前可用于布点的猫种权重池。
        /// </summary>
        public IReadOnlyList<BoardSpawnEntry> CatPool = Array.Empty<BoardSpawnEntry>();
    }
}
