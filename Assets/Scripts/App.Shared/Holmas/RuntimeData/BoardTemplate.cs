using System;
using UnityEngine;

namespace App.Shared.Holmas.RuntimeData
{
    /// <summary>
    /// 棋盘模板数据。
    /// 这是从地形资产转换出来的运行时基础形状，只描述棋盘长什么样，不描述这一局发生了什么。
    /// 之所以放在 Shared，是为了让 HotUpdate 的地图线、任务线、UI 线都能依赖同一份基础结构。
    /// </summary>
    [Serializable]
    public sealed class BoardTemplate
    {
        /// <summary>
        /// 棋盘总行数。
        /// 后续所有一维数组长度都以 Rows * Cols 为基准。
        /// </summary>
        public int Rows;

        /// <summary>
        /// 棋盘总列数。
        /// 配合 Rows 一起决定一维格子索引的映射范围。
        /// </summary>
        public int Cols;

        /// <summary>
        /// 每个格子是否可用的布尔遮罩。
        /// true 表示该格可以参与本局生成和揭示，false 表示这是异形棋盘中的无效格。
        /// </summary>
        public bool[] ValidMask = Array.Empty<bool>();

        /// <summary>
        /// 每个格子的底板颜色。
        /// 这属于地图模板的视觉输入，不承载任务、奖励或运行时状态。
        /// </summary>
        public Color32[] BlockColors = Array.Empty<Color32>();
    }
}
