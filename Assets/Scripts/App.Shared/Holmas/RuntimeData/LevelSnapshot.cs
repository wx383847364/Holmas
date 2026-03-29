using System;
using System.Collections.Generic;

namespace App.Shared.Holmas.RuntimeData
{
    /// <summary>
    /// 单局关卡的运行时快照。
    /// 它记录地图模板来源、本局随机种子、生成结果和揭示进度，供玩法服务与 UI 共同查看。
    /// </summary>
    [Serializable]
    public sealed class LevelSnapshot
    {
        /// <summary>
        /// 当前关卡使用的地图配置标识。
        /// 对应地图表中的 mapId。
        /// </summary>
        public string MapId = string.Empty;

        /// <summary>
        /// 当前关卡使用的地形资源路径。
        /// 对应 YooAssets key 或资源地址，指向的是地图模板资产，不是场景路径。
        /// </summary>
        public string TerrainPath = string.Empty;

        /// <summary>
        /// 当前关卡的随机种子。
        /// 后续地图生成和复盘若要保持一致性，可以依赖这份种子。
        /// </summary>
        public int Seed;

        /// <summary>
        /// 本局实际生成出来的猫列表。
        /// 这里只保存生成结果，不保存生成算法。
        /// </summary>
        public List<SpawnedCatData> SpawnedCats = new List<SpawnedCatData>();

        /// <summary>
        /// 每个格子是否已经被玩家揭示。
        /// 长度与棋盘总格数保持一致，索引规则与 BoardTemplate 相同。
        /// </summary>
        public bool[] RevealedCells = Array.Empty<bool>();

        /// <summary>
        /// 当前关卡是否已经完成。
        /// 这里只表达结果状态，不承载通关判定逻辑。
        /// </summary>
        public bool Completed;
    }
}
