using System;
using System.Collections.Generic;

namespace App.HotUpdate.Holmas.Tasks.Config
{
    /// <summary>
    /// Holmas 配置包格式常量。
    /// 二进制导表和运行时导入都必须按同一版本和魔数对齐。
    /// </summary>
    public static class HolmasConfigBinaryFormat
    {
        public const int CoreMagic = 0x48434F52; // HCOR
        public const int CatMetaMagic = 0x48434154; // HCAT
        public const int CurrentVersion = 7;
        public const int MinSupportedVersion = CurrentVersion;
    }

    /// <summary>
    /// 配置导入/导出报告。
    /// 用于二进制、JSON 预览和运行时导入的统一结果回传。
    /// </summary>
    [Serializable]
    public sealed class HolmasConfigReport
    {
        public bool Success;
        public string Summary = string.Empty;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();

        public void AddError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Errors.Add(message);
            Success = false;
        }

        public void AddWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Warnings.Add(message);
        }

        public void MarkSuccess(string summary = null)
        {
            Success = true;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                Summary = summary;
            }
        }
    }

    /// <summary>
    /// 猫元数据包。
    /// 保留猫的展示信息，供核心配置与 UI 共同消费。
    /// </summary>
    [Serializable]
    public sealed class HolmasCatMetaPackage
    {
        public int Version = HolmasConfigBinaryFormat.CurrentVersion;
        public HolmasCatTableRow[] Holmas_CatTable = Array.Empty<HolmasCatTableRow>();
    }

    /// <summary>
    /// 核心配置包。
    /// 包含地图、任务、玩家等级与长期成长相关的正式核心表。
    /// </summary>
    [Serializable]
    public sealed class HolmasCoreConfigPackage
    {
        public int Version = HolmasConfigBinaryFormat.CurrentVersion;
        public HolmasMapTableRow[] Holmas_MapTable = Array.Empty<HolmasMapTableRow>();
        public HolmasTaskTableRow[] Holmas_TaskTable = Array.Empty<HolmasTaskTableRow>();
        public HolmasPlayerLevelTableRow[] Holmas_PlayerLevelTable = Array.Empty<HolmasPlayerLevelTableRow>();
        public HolmasAgencyBuildingTableRow[] Holmas_AgencyBuildingTable = Array.Empty<HolmasAgencyBuildingTableRow>();
        public HolmasLeaderboardTableRow[] Holmas_LeaderboardTable = Array.Empty<HolmasLeaderboardTableRow>();

        public int CodecVersion { get; internal set; } = HolmasConfigBinaryFormat.CurrentVersion;
    }

    /// <summary>
    /// 地图配置行。
    /// </summary>
    [Serializable]
    public sealed class HolmasMapTableRow
    {
        public string mapId = string.Empty;
        public string terrainPath = string.Empty;
        public int catCountMin;
        public int catCountMax;
    }

    /// <summary>
    /// 猫表行，字段名严格匹配 Holmas_CatTable.xlsx 技术表头。
    /// </summary>
    [Serializable]
    public sealed class HolmasCatTableRow
    {
        public string catId = string.Empty;
        public string catName = string.Empty;
        public string iconPath = string.Empty;
        public int rarity;
        public int weight = 1;
        public int price;
    }

    /// <summary>
    /// 任务模板配置行。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskTableRow
    {
        public string taskTypeId = string.Empty;
        public HolmasTaskKind taskKind = HolmasTaskKind.Money;
        public string[] catIdList = Array.Empty<string>();
        public int countMin;
        public int countMax;
        public int[] rewardArray = Array.Empty<int>();
        public float levelRewardFactor = 1f;
    }

    /// <summary>
    /// 玩家等级配置行。
    /// </summary>
    [Serializable]
    public sealed class HolmasPlayerLevelTableRow
    {
        public int playerLevel;
        public int minExperience;
        public int offlineRewardPerHour;
        public int adUnlockHours = 24;
        public string[] taskTypeIds = Array.Empty<string>();
        public int[] taskTypeWeights = Array.Empty<int>();
        public string[] mapIds = Array.Empty<string>();
        public int[] mapWeights = Array.Empty<int>();
    }

    /// <summary>
    /// 宣传升级费用序列。
    /// 每个实例对应一个宣传功能的一条升级费用曲线。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyBuildingTableCostRow
    {
        public int[] costs = Array.Empty<int>();
    }

    /// <summary>
    /// Holmas_AgencyBuildingTable 表行，字段名严格匹配技术表头。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyBuildingTableRow
    {
        public int agencyStageId;
        public string stageName = string.Empty;
        public string[] promotionIds = Array.Empty<string>();
        public int[] promotionLevelCaps = Array.Empty<int>();
        public HolmasAgencyBuildingTableCostRow[] promotionUpgradeCosts = Array.Empty<HolmasAgencyBuildingTableCostRow>();
        public string notes = string.Empty;
    }

    [Serializable]
    public sealed class HolmasLeaderboardTableRow
    {
        public string leaderboardType = string.Empty;
        public string displayName = string.Empty;
        public string periodType = "AllTime";
        public string timeZoneId = "Asia/Shanghai";
        public int resetDayOfWeek;
        public int resetHour;
        public int resetMinute;
        public int topEntryCount = 20;
        public int mockEntryCount = 100;
        public bool isEnabled = true;
        public string notes = string.Empty;
    }

    /// <summary>
    /// 运行时配置恢复结果。
    /// 方便上层同时拿到两个 Catalog 和原始定义快照。
    /// </summary>
    public sealed class HolmasConfigCatalogBundle
    {
        public HolmasConfigCatalogBundle(
            HolmasMapCatalog mapCatalog,
            HolmasTaskCatalog taskCatalog,
            IReadOnlyList<HolmasCatDefinition> cats,
            IReadOnlyList<HolmasMapDefinition> maps,
            IReadOnlyList<HolmasTaskTemplateDefinition> taskTemplates,
            IReadOnlyList<HolmasPlayerLevelDefinition> playerLevels,
            IReadOnlyList<HolmasAgencyBuildingTableRow> holmasAgencyBuildingTable,
            IReadOnlyList<HolmasLeaderboardDefinition> leaderboards,
            HolmasConfigReport report)
        {
            MapCatalog = mapCatalog;
            TaskCatalog = taskCatalog;
            Cats = cats ?? Array.Empty<HolmasCatDefinition>();
            Maps = maps ?? Array.Empty<HolmasMapDefinition>();
            TaskTemplates = taskTemplates ?? Array.Empty<HolmasTaskTemplateDefinition>();
            PlayerLevels = playerLevels ?? Array.Empty<HolmasPlayerLevelDefinition>();
            Holmas_AgencyBuildingTable = holmasAgencyBuildingTable ?? Array.Empty<HolmasAgencyBuildingTableRow>();
            Leaderboards = leaderboards ?? Array.Empty<HolmasLeaderboardDefinition>();
            Report = report ?? new HolmasConfigReport();
        }

        public HolmasMapCatalog MapCatalog { get; }
        public HolmasTaskCatalog TaskCatalog { get; }
        public IReadOnlyList<HolmasCatDefinition> Cats { get; }
        public IReadOnlyList<HolmasMapDefinition> Maps { get; }
        public IReadOnlyList<HolmasTaskTemplateDefinition> TaskTemplates { get; }
        public IReadOnlyList<HolmasPlayerLevelDefinition> PlayerLevels { get; }
        public IReadOnlyList<HolmasAgencyBuildingTableRow> Holmas_AgencyBuildingTable { get; }
        public IReadOnlyList<HolmasLeaderboardDefinition> Leaderboards { get; }
        public HolmasConfigReport Report { get; }
    }
}
