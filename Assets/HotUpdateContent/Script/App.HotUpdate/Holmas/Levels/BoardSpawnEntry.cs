using System;

namespace App.HotUpdate.Holmas.Levels
{
    /// <summary>
    /// 旧关卡生成入口兼容的猫种权重项。
    /// 普通棋盘不再用它预分配猫种，猫种会在揭示时从未完成任务池解析。
    /// </summary>
    [Serializable]
    public sealed class BoardSpawnEntry
    {
        /// <summary>
        /// 猫种标识。
        /// </summary>
        public string CatId = string.Empty;

        /// <summary>
        /// 该猫种的权重。
        /// </summary>
        public int Weight = 1;
    }
}
