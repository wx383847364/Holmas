using System;

namespace App.HotUpdate.Holmas.Levels
{
    /// <summary>
    /// 关卡生成时的猫种权重项。
    /// 这只描述生成输入，不描述运行时状态。
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
