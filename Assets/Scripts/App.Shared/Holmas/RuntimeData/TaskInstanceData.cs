using System;

namespace App.Shared.Holmas.RuntimeData
{
    /// <summary>
    /// 单个任务实例的数据。
    /// 它表示任务栏里当前显示的一条任务长什么样，不负责任务如何抽取、如何奖励。
    /// </summary>
    [Serializable]
    public sealed class TaskInstanceData
    {
        /// <summary>
        /// 任务实例唯一标识。
        /// 同一个任务模板多次生成时，应该产生不同的实例 ID。
        /// </summary>
        public string TaskInstanceId = string.Empty;

        /// <summary>
        /// 任务来源模板标识。
        /// 对应任务模板表中的 taskTypeId。
        /// </summary>
        public string SourceTaskTypeId = string.Empty;

        /// <summary>
        /// 任务种类标识。
        /// v1 先保留为字符串，便于后续扩展普通任务、特殊任务等分类。
        /// </summary>
        public string TaskKind = string.Empty;

        /// <summary>
        /// 当前任务要求玩家寻找的猫种类标识。
        /// 对应配置表中的 catId。
        /// </summary>
        public string CatId = string.Empty;

        /// <summary>
        /// 当前任务目标数量。
        /// 表示这一条任务最终需要找多少只猫。
        /// </summary>
        public int TargetCount;

        /// <summary>
        /// 当前任务已完成数量。
        /// 地图结算或找到猫后，后续逻辑会基于它推进任务进度。
        /// </summary>
        public int CurrentCount;

        /// <summary>
        /// 当前任务可领取的奖励值。
        /// 这里只保存计算结果，不保存奖励公式。
        /// </summary>
        public int Reward;

        /// <summary>
        /// 当前任务所在的槽位索引。
        /// 任务实例与槽位状态通过这个索引关联。
        /// </summary>
        public int SlotIndex;

        /// <summary>
        /// 任务过期时间，使用 UTC Unix 毫秒。
        /// 对默认常驻槽位，0 表示没有过期时间。
        /// </summary>
        public long ExpireAt;
    }
}
