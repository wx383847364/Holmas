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
        public const int CurrentVersion = 6;
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
        public HolmasCatMetaRow[] Cats = Array.Empty<HolmasCatMetaRow>();
    }

    /// <summary>
    /// 猫元数据行。
    /// </summary>
    [Serializable]
    public sealed class HolmasCatMetaRow
    {
        public string CatId = string.Empty;
        public string CatName = string.Empty;
        public string IconPath = string.Empty;
        public int Rarity;
        public int Weight = 1;
        public int Price;
    }

    /// <summary>
    /// 宣传升级费用序列。
    /// 每个实例对应一个宣传功能的一条升级费用曲线。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyBuildingCostRow
    {
        public int[] Costs = Array.Empty<int>();
    }

    /// <summary>
    /// 侦探社宣传配置行。
    /// </summary>
    [Serializable]
    public sealed class HolmasAgencyBuildingRow
    {
        public int AgencyStageId;
        public string StageName = string.Empty;
        public string[] PromotionIds = Array.Empty<string>();
        public int[] PromotionLevelCaps = Array.Empty<int>();
        public HolmasAgencyBuildingCostRow[] PromotionUpgradeCosts = Array.Empty<HolmasAgencyBuildingCostRow>();
        public string Notes = string.Empty;
    }

    /// <summary>
    /// 核心配置包。
    /// 包含地图、任务、玩家等级与长期成长相关的正式核心表。
    /// </summary>
    [Serializable]
    public sealed class HolmasCoreConfigPackage
    {
        public int Version = HolmasConfigBinaryFormat.CurrentVersion;
        public HolmasMapRow[] Maps = Array.Empty<HolmasMapRow>();
        public HolmasTaskRow[] Tasks = Array.Empty<HolmasTaskRow>();
        public HolmasPlayerLevelRow[] PlayerLevels = Array.Empty<HolmasPlayerLevelRow>();
        public HolmasAgencyBuildingRow[] AgencyBuildings = Array.Empty<HolmasAgencyBuildingRow>();

        public int CodecVersion { get; internal set; } = HolmasConfigBinaryFormat.CurrentVersion;
    }

    /// <summary>
    /// 地图配置行。
    /// </summary>
    [Serializable]
    public sealed class HolmasMapRow
    {
        public string MapId = string.Empty;
        public string TerrainPath = string.Empty;
        public int CatCountMin;
        public int CatCountMax;
    }

    /// <summary>
    /// 任务模板配置行。
    /// </summary>
    [Serializable]
    public sealed class HolmasTaskRow
    {
        public string TaskTypeId = string.Empty;
        public HolmasTaskKind TaskKind = HolmasTaskKind.Money;
        public int[] CatIndices = Array.Empty<int>();
        public int CountMin;
        public int CountMax;
        public int[] RewardValues = Array.Empty<int>();
        public float LevelRewardFactor = 1f;
    }

    /// <summary>
    /// 玩家等级配置行。
    /// </summary>
    [Serializable]
    public sealed class HolmasPlayerLevelRow
    {
        public int PlayerLevel;
        public int UpgradeExp;
        public int OfflineRewardPerHour;
        public int AdUnlockHours = 24;
        public int[] TaskTypeIndices = Array.Empty<int>();
        public int[] TaskTypeWeights = Array.Empty<int>();
        public int[] MapIndices = Array.Empty<int>();
        public int[] MapWeights = Array.Empty<int>();
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
            IReadOnlyList<HolmasAgencyBuildingRow> agencyBuildings,
            HolmasConfigReport report)
        {
            MapCatalog = mapCatalog;
            TaskCatalog = taskCatalog;
            Cats = cats ?? Array.Empty<HolmasCatDefinition>();
            Maps = maps ?? Array.Empty<HolmasMapDefinition>();
            TaskTemplates = taskTemplates ?? Array.Empty<HolmasTaskTemplateDefinition>();
            PlayerLevels = playerLevels ?? Array.Empty<HolmasPlayerLevelDefinition>();
            AgencyBuildings = agencyBuildings ?? Array.Empty<HolmasAgencyBuildingRow>();
            Report = report ?? new HolmasConfigReport();
        }

        public HolmasMapCatalog MapCatalog { get; }
        public HolmasTaskCatalog TaskCatalog { get; }
        public IReadOnlyList<HolmasCatDefinition> Cats { get; }
        public IReadOnlyList<HolmasMapDefinition> Maps { get; }
        public IReadOnlyList<HolmasTaskTemplateDefinition> TaskTemplates { get; }
        public IReadOnlyList<HolmasPlayerLevelDefinition> PlayerLevels { get; }
        public IReadOnlyList<HolmasAgencyBuildingRow> AgencyBuildings { get; }
        public HolmasConfigReport Report { get; }
    }
}
