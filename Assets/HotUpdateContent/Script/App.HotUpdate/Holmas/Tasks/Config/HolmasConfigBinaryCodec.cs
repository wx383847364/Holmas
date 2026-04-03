using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace App.HotUpdate.Holmas.Tasks.Config
{
    /// <summary>
    /// Holmas 二进制配置编解码器。
    /// 当前由导表工具和运行时导入器共享，保证导出内容与消费侧一致。
    /// </summary>
    public static class HolmasConfigBinaryCodec
    {
        public static byte[] WriteCorePackage(HolmasCoreConfigPackage package)
        {
            package = package ?? new HolmasCoreConfigPackage();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteCorePackage(writer, package);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static byte[] WriteCatMetaPackage(HolmasCatMetaPackage package)
        {
            package = package ?? new HolmasCatMetaPackage();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteCatMetaPackage(writer, package);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryReadCorePackage(byte[] bytes, out HolmasCoreConfigPackage package, out string error)
        {
            package = null;
            error = string.Empty;

            if (bytes == null || bytes.Length == 0)
            {
                error = "核心配置二进制为空。";
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    package = ReadCorePackage(reader, out error);
                    return package != null;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                package = null;
                return false;
            }
        }

        public static bool TryReadCatMetaPackage(byte[] bytes, out HolmasCatMetaPackage package, out string error)
        {
            package = null;
            error = string.Empty;

            if (bytes == null || bytes.Length == 0)
            {
                error = "猫元数据二进制为空。";
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    package = ReadCatMetaPackage(reader, out error);
                    return package != null;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                package = null;
                return false;
            }
        }

        public static string ToCoreJson(HolmasCoreConfigPackage package, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(package ?? new HolmasCoreConfigPackage(), prettyPrint);
        }

        public static string ToCatMetaJson(HolmasCatMetaPackage package, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(package ?? new HolmasCatMetaPackage(), prettyPrint);
        }

        public static bool TryReadCoreJson(string json, out HolmasCoreConfigPackage package, out string error)
        {
            package = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "核心配置 JSON 为空。";
                return false;
            }

            try
            {
                package = JsonUtility.FromJson<HolmasCoreConfigPackage>(json);
                if (package == null)
                {
                    error = "核心配置 JSON 解析失败。";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                package = null;
                error = ex.Message;
                return false;
            }
        }

        public static bool TryReadCatMetaJson(string json, out HolmasCatMetaPackage package, out string error)
        {
            package = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "猫元数据 JSON 为空。";
                return false;
            }

            try
            {
                package = JsonUtility.FromJson<HolmasCatMetaPackage>(json);
                if (package == null)
                {
                    error = "猫元数据 JSON 解析失败。";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                package = null;
                error = ex.Message;
                return false;
            }
        }

        private static void WriteCorePackage(BinaryWriter writer, HolmasCoreConfigPackage package)
        {
            writer.Write(HolmasConfigBinaryFormat.CoreMagic);
            writer.Write(HolmasConfigBinaryFormat.CurrentVersion);
            writer.Write(package.Version);
            WriteMapRows(writer, package.Maps);
            WriteTaskRows(writer, package.Tasks);
            WritePlayerLevelRows(writer, package.PlayerLevels);
            WriteMetaLevelRows(writer, package.MetaLevels);
            WriteAgencyBuildingRows(writer, package.AgencyBuildings);
        }

        private static void WriteCatMetaPackage(BinaryWriter writer, HolmasCatMetaPackage package)
        {
            writer.Write(HolmasConfigBinaryFormat.CatMetaMagic);
            writer.Write(HolmasConfigBinaryFormat.CurrentVersion);
            writer.Write(package.Version);
            WriteCatRows(writer, package.Cats);
        }

        private static HolmasCoreConfigPackage ReadCorePackage(BinaryReader reader, out string error)
        {
            error = string.Empty;
            if (!ReadAndValidateHeader(reader, HolmasConfigBinaryFormat.CoreMagic, out var version, out error))
            {
                return null;
            }

            var package = new HolmasCoreConfigPackage
            {
                Version = version,
                Maps = ReadMapRows(reader),
                Tasks = ReadTaskRows(reader),
                PlayerLevels = ReadPlayerLevelRows(reader),
                MetaLevels = ReadMetaLevelRows(reader),
                AgencyBuildings = ReadAgencyBuildingRows(reader),
            };

            return package;
        }

        private static HolmasCatMetaPackage ReadCatMetaPackage(BinaryReader reader, out string error)
        {
            error = string.Empty;
            if (!ReadAndValidateHeader(reader, HolmasConfigBinaryFormat.CatMetaMagic, out var version, out error))
            {
                return null;
            }

            var package = new HolmasCatMetaPackage
            {
                Version = version,
                Cats = ReadCatRows(reader),
            };

            return package;
        }

        private static bool ReadAndValidateHeader(BinaryReader reader, int expectedMagic, out int packageVersion, out string error)
        {
            packageVersion = 0;
            error = string.Empty;

            int magic = reader.ReadInt32();
            if (magic != expectedMagic)
            {
                error = $"配置包魔数不匹配: 0x{magic:X8}。";
                return false;
            }

            int codecVersion = reader.ReadInt32();
            if (codecVersion != HolmasConfigBinaryFormat.CurrentVersion)
            {
                error = $"配置包编解码版本不支持: {codecVersion}。";
                return false;
            }

            packageVersion = reader.ReadInt32();
            return true;
        }

        private static void WriteMapRows(BinaryWriter writer, HolmasMapRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasMapRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasMapRow row = rows[i] ?? new HolmasMapRow();
                WriteString(writer, row.MapId);
                WriteString(writer, row.TerrainPath);
                writer.Write(row.CatCountMin);
                writer.Write(row.CatCountMax);
            }
        }

        private static HolmasMapRow[] ReadMapRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasMapRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasMapRow
                {
                    MapId = ReadString(reader),
                    TerrainPath = ReadString(reader),
                    CatCountMin = reader.ReadInt32(),
                    CatCountMax = reader.ReadInt32(),
                };
            }

            return rows;
        }

        private static void WriteCatRows(BinaryWriter writer, HolmasCatMetaRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasCatMetaRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasCatMetaRow row = rows[i] ?? new HolmasCatMetaRow();
                WriteString(writer, row.CatId);
                WriteString(writer, row.CatName);
                WriteString(writer, row.IconPath);
                writer.Write(row.Rarity);
                writer.Write(row.Weight);
                writer.Write(row.Price);
            }
        }

        private static HolmasCatMetaRow[] ReadCatRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasCatMetaRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasCatMetaRow
                {
                    CatId = ReadString(reader),
                    CatName = ReadString(reader),
                    IconPath = ReadString(reader),
                    Rarity = reader.ReadInt32(),
                    Weight = reader.ReadInt32(),
                    Price = reader.ReadInt32(),
                };
            }

            return rows;
        }

        private static void WriteTaskRows(BinaryWriter writer, HolmasTaskRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasTaskRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasTaskRow row = rows[i] ?? new HolmasTaskRow();
                WriteString(writer, row.TaskTypeId);
                writer.Write((byte)row.TaskKind);
                WriteIntArray(writer, row.CatIndices);
                writer.Write(row.CountMin);
                writer.Write(row.CountMax);
                WriteIntArray(writer, row.RewardValues);
                writer.Write(row.LevelRewardFactor);
            }
        }

        private static HolmasTaskRow[] ReadTaskRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasTaskRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasTaskRow
                {
                    TaskTypeId = ReadString(reader),
                    TaskKind = (HolmasTaskKind)reader.ReadByte(),
                    CatIndices = ReadIntArray(reader),
                    CountMin = reader.ReadInt32(),
                    CountMax = reader.ReadInt32(),
                    RewardValues = ReadIntArray(reader),
                    LevelRewardFactor = reader.ReadSingle(),
                };
            }

            return rows;
        }

        private static void WritePlayerLevelRows(BinaryWriter writer, HolmasPlayerLevelRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasPlayerLevelRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasPlayerLevelRow row = rows[i] ?? new HolmasPlayerLevelRow();
                writer.Write(row.PlayerLevel);
                writer.Write(row.UpgradeExp);
                WriteIntArray(writer, row.TaskTypeIndices);
                WriteIntArray(writer, row.TaskTypeWeights);
                WriteIntArray(writer, row.MapIndices);
                WriteIntArray(writer, row.MapWeights);
            }
        }

        private static HolmasPlayerLevelRow[] ReadPlayerLevelRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasPlayerLevelRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasPlayerLevelRow
                {
                    PlayerLevel = reader.ReadInt32(),
                    UpgradeExp = reader.ReadInt32(),
                    TaskTypeIndices = ReadIntArray(reader),
                    TaskTypeWeights = ReadIntArray(reader),
                    MapIndices = ReadIntArray(reader),
                    MapWeights = ReadIntArray(reader),
                };
            }

            return rows;
        }

        private static void WriteMetaLevelRows(BinaryWriter writer, HolmasMetaLevelRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasMetaLevelRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasMetaLevelRow row = rows[i] ?? new HolmasMetaLevelRow();
                writer.Write(row.PlayerLevel);
                writer.Write(row.MinExperience);
                writer.Write(row.OfflineRewardPerHour);
                writer.Write(row.AdUnlockHours);
            }
        }

        private static HolmasMetaLevelRow[] ReadMetaLevelRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasMetaLevelRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasMetaLevelRow
                {
                    PlayerLevel = reader.ReadInt32(),
                    MinExperience = reader.ReadInt64(),
                    OfflineRewardPerHour = reader.ReadInt32(),
                    AdUnlockHours = reader.ReadInt32(),
                };
            }

            return rows;
        }

        private static void WriteAgencyBuildingRows(BinaryWriter writer, HolmasAgencyBuildingRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasAgencyBuildingRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasAgencyBuildingRow row = rows[i] ?? new HolmasAgencyBuildingRow();
                writer.Write(row.AgencyStageId);
                WriteString(writer, row.StageName);
                WriteStringArray(writer, row.PromotionIds);
                WriteIntArray(writer, row.PromotionLevelCaps);
                WriteBuildingCostRows(writer, row.PromotionUpgradeCosts);
            }
        }

        private static HolmasAgencyBuildingRow[] ReadAgencyBuildingRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasAgencyBuildingRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasAgencyBuildingRow
                {
                    AgencyStageId = reader.ReadInt32(),
                    StageName = ReadString(reader),
                    PromotionIds = ReadStringArray(reader),
                    PromotionLevelCaps = ReadIntArray(reader),
                    PromotionUpgradeCosts = ReadBuildingCostRows(reader),
                };
            }

            return rows;
        }

        private static void WriteBuildingCostRows(BinaryWriter writer, HolmasAgencyBuildingCostRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasAgencyBuildingCostRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasAgencyBuildingCostRow row = rows[i] ?? new HolmasAgencyBuildingCostRow();
                WriteIntArray(writer, row.Costs);
            }
        }

        private static HolmasAgencyBuildingCostRow[] ReadBuildingCostRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasAgencyBuildingCostRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasAgencyBuildingCostRow
                {
                    Costs = ReadIntArray(reader),
                };
            }

            return rows;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(value ?? string.Empty);
        }

        private static string ReadString(BinaryReader reader)
        {
            return reader.ReadString() ?? string.Empty;
        }

        private static void WriteStringArray(BinaryWriter writer, string[] values)
        {
            values = values ?? Array.Empty<string>();
            writer.Write(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                WriteString(writer, values[i]);
            }
        }

        private static string[] ReadStringArray(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var values = new string[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = ReadString(reader);
            }

            return values;
        }

        private static void WriteIntArray(BinaryWriter writer, int[] values)
        {
            values = values ?? Array.Empty<int>();
            writer.Write(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                writer.Write(values[i]);
            }
        }

        private static int[] ReadIntArray(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadInt32();
            }

            return values;
        }

        private static int ReadNonNegativeCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException($"配置包包含非法数组长度: {count}。");
            }

            return count;
        }
    }
}
