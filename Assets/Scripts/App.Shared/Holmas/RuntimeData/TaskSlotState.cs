using System;

namespace App.Shared.Holmas.RuntimeData
{
    /// <summary>
    /// 任务槽位的解锁与绑定状态。
    /// 它只表达某个槽位当前是否可用、何时到期、挂着哪条任务，不承载广告或补位逻辑。
    /// </summary>
    [Serializable]
    public sealed class TaskSlotState
    {
        /// <summary>
        /// 任务槽位索引。
        /// v1 固定总槽位为 5，这里用于标识第几个槽。
        /// </summary>
        public int SlotIndex;

        /// <summary>
        /// 当前槽位是否已解锁。
        /// 默认槽位为 true，广告槽位在观看广告后临时变为 true。
        /// </summary>
        public bool IsUnlocked;

        /// <summary>
        /// 槽位解锁到期时间，使用 UTC Unix 毫秒。
        /// 0 表示默认常驻槽位或当前没有到期时间。
        /// </summary>
        public long UnlockExpireAt;

        /// <summary>
        /// 当前槽位绑定的任务实例 ID。
        /// 为空字符串表示该槽位当前没有挂任务。
        /// </summary>
        public string TaskInstanceId = string.Empty;
    }
}
