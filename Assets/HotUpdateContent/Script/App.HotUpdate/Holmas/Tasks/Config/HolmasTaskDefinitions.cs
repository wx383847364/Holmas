using System;
using System.Collections.Generic;
using System.Linq;

namespace App.HotUpdate.Holmas.Tasks.Config
{
    /// <summary>
    /// 任务种类枚举。
    /// 当前阶段只正式支持 Money，Gamble 先保留字段位，不接奖励逻辑。
    /// </summary>
    public enum HolmasTaskKind : byte
    {
        Money = 0,
        Gamble = 1,
    }

    /// <summary>
    /// 猫配置定义。
    /// 这里只描述静态输入，不承载任何运行时状态。
    /// </summary>
    [Serializable]
    public sealed class HolmasCatDefinition
    {
        public int CatIndex;
        public string CatId = string.Empty;
        public string CatName = string.Empty;
        public string IconPath = string.Empty;
        public int Rarity;
        public int Weight = 1;
        public int Price;
    }

    /// <summary>
    /// 任务模板配置定义。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskTemplateDefinition
    {
        public int TaskIndex;
        public string TaskTypeId = string.Empty;
        public HolmasTaskKind TaskKind = HolmasTaskKind.Money;
        public string[] CatIdList = Array.Empty<string>();
        public int[] CatIndices = Array.Empty<int>();
        public int CountMax;
        public int CountMin;
        public string[] RewardArray = Array.Empty<string>();
        public int[] RewardValues = Array.Empty<int>();
        public float LevelRewardFactor = 1f;
    }

    /// <summary>
    /// 玩家等级配置定义。
    /// </summary>
    [Serializable]
    public sealed class HolmasPlayerLevelDefinition
    {
        public int PlayerLevelIndex;
        public int PlayerLevel;
        public int UpgradeExp;
        public int OfflineRewardPerHour;
        public int AdUnlockHours = 24;
        public string[] TaskTypeIds = Array.Empty<string>();
        public int[] TaskTypeWeights = Array.Empty<int>();
        public int[] TaskTypeIndices = Array.Empty<int>();
        public string[] MapIds = Array.Empty<string>();
        public int[] MapWeights = Array.Empty<int>();
        public int[] MapIndices = Array.Empty<int>();
    }

    /// <summary>
    /// 配置仓库接口。
    /// 任务服务只通过这个接口读取配置，不直接依赖 JSON/表格/AssetObject。
    /// </summary>
    public interface IHolmasTaskCatalog
    {
        bool TryGetCat(string catId, out HolmasCatDefinition definition);
        bool TryGetTaskTemplate(string taskTypeId, out HolmasTaskTemplateDefinition definition);
        bool TryGetPlayerLevel(int playerLevel, out HolmasPlayerLevelDefinition definition);
    }

    /// <summary>
    /// 纯内存版配置仓库。
    /// 适合后续表导入器、测试夹具或手工拼装数据使用。
    /// </summary>
    public sealed class HolmasTaskCatalog : IHolmasTaskCatalog
    {
        private readonly Dictionary<string, HolmasCatDefinition> _cats = new Dictionary<string, HolmasCatDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HolmasTaskTemplateDefinition> _taskTemplates = new Dictionary<string, HolmasTaskTemplateDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<int, HolmasPlayerLevelDefinition> _playerLevels = new Dictionary<int, HolmasPlayerLevelDefinition>();

        public HolmasTaskCatalog()
        {
        }

        public HolmasTaskCatalog(
            IEnumerable<HolmasCatDefinition> cats,
            IEnumerable<HolmasTaskTemplateDefinition> taskTemplates,
            IEnumerable<HolmasPlayerLevelDefinition> playerLevels)
        {
            SetCats(cats);
            SetTaskTemplates(taskTemplates);
            SetPlayerLevels(playerLevels);
        }

        public void SetCats(IEnumerable<HolmasCatDefinition> cats)
        {
            _cats.Clear();
            if (cats == null)
            {
                return;
            }

            foreach (var cat in cats.Where(item => item != null && !string.IsNullOrEmpty(item.CatId)))
            {
                _cats[cat.CatId] = cat;
            }
        }

        public void SetTaskTemplates(IEnumerable<HolmasTaskTemplateDefinition> taskTemplates)
        {
            _taskTemplates.Clear();
            if (taskTemplates == null)
            {
                return;
            }

            foreach (var template in taskTemplates.Where(item => item != null && !string.IsNullOrEmpty(item.TaskTypeId)))
            {
                _taskTemplates[template.TaskTypeId] = template;
            }
        }

        public void SetPlayerLevels(IEnumerable<HolmasPlayerLevelDefinition> playerLevels)
        {
            _playerLevels.Clear();
            if (playerLevels == null)
            {
                return;
            }

            foreach (var level in playerLevels.Where(item => item != null))
            {
                _playerLevels[level.PlayerLevel] = level;
            }
        }

        public bool TryGetCat(string catId, out HolmasCatDefinition definition)
        {
            return _cats.TryGetValue(catId ?? string.Empty, out definition);
        }

        public bool TryGetTaskTemplate(string taskTypeId, out HolmasTaskTemplateDefinition definition)
        {
            return _taskTemplates.TryGetValue(taskTypeId ?? string.Empty, out definition);
        }

        public bool TryGetPlayerLevel(int playerLevel, out HolmasPlayerLevelDefinition definition)
        {
            return _playerLevels.TryGetValue(playerLevel, out definition);
        }
    }
}
