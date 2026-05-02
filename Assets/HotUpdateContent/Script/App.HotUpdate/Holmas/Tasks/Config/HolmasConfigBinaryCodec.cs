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

                if (ContainsAnyTopLevelField(json, "MetaLevels", "PlayerLevels", "Maps", "Tasks", "AgencyBuildings"))
                {
                    error = "核心配置 JSON 使用了旧包装字段，请按 Holmas_*Table 表镜像协议重新导出。";
                    package = null;
                    return false;
                }

                if (package.Version != HolmasConfigBinaryFormat.CurrentVersion)
                {
                    error = $"核心配置 JSON 版本不支持: {package.Version}。当前仅支持版本 {HolmasConfigBinaryFormat.CurrentVersion}。";
                    package = null;
                    return false;
                }

                package.CodecVersion = HolmasConfigBinaryFormat.CurrentVersion;
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

                if (ContainsAnyTopLevelField(json, "Cats"))
                {
                    error = "猫元数据 JSON 使用了旧包装字段，请按 Holmas_CatTable 表镜像协议重新导出。";
                    package = null;
                    return false;
                }

                if (package.Version != HolmasConfigBinaryFormat.CurrentVersion)
                {
                    error = $"猫元数据 JSON 版本不支持: {package.Version}。当前仅支持版本 {HolmasConfigBinaryFormat.CurrentVersion}。";
                    package = null;
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
            WriteMapRows(writer, package.Holmas_MapTable);
            WriteTaskRows(writer, package.Holmas_TaskTable);
            WritePlayerLevelRows(writer, package.Holmas_PlayerLevelTable);
            WriteAgencyBuildingTableRows(writer, package.Holmas_AgencyBuildingTable);
            WriteLeaderboardRows(writer, package.Holmas_LeaderboardTable);
        }

        private static void WriteCatMetaPackage(BinaryWriter writer, HolmasCatMetaPackage package)
        {
            writer.Write(HolmasConfigBinaryFormat.CatMetaMagic);
            writer.Write(HolmasConfigBinaryFormat.CurrentVersion);
            writer.Write(package.Version);
            WriteCatRows(writer, package.Holmas_CatTable);
        }

        private static HolmasCoreConfigPackage ReadCorePackage(BinaryReader reader, out string error)
        {
            error = string.Empty;
            if (!ReadAndValidateHeader(
                    reader,
                    HolmasConfigBinaryFormat.CoreMagic,
                    out var codecVersion,
                    out var packageVersion,
                    out error))
            {
                return null;
            }

            HolmasMapTableRow[] maps = ReadMapRows(reader);
            HolmasTaskTableRow[] tasks = ReadTaskRows(reader);
            HolmasPlayerLevelTableRow[] playerLevels = ReadPlayerLevelRows(reader);
            HolmasAgencyBuildingTableRow[] agencyBuildingTable = ReadAgencyBuildingTableRows(reader);
            HolmasLeaderboardTableRow[] leaderboards = ReadLeaderboardRowsOrDefault(reader);

            var package = new HolmasCoreConfigPackage
            {
                Version = packageVersion,
                Holmas_MapTable = maps,
                Holmas_TaskTable = tasks,
                Holmas_PlayerLevelTable = playerLevels,
                Holmas_AgencyBuildingTable = agencyBuildingTable,
                Holmas_LeaderboardTable = leaderboards,
            };
            package.CodecVersion = codecVersion;

            return package;
        }

        private static HolmasCatMetaPackage ReadCatMetaPackage(BinaryReader reader, out string error)
        {
            error = string.Empty;
            if (!ReadAndValidateHeader(
                    reader,
                    HolmasConfigBinaryFormat.CatMetaMagic,
                    out _,
                    out var packageVersion,
                    out error))
            {
                return null;
            }

            var package = new HolmasCatMetaPackage
            {
                Version = packageVersion,
                Holmas_CatTable = ReadCatRows(reader),
            };

            return package;
        }

        private static bool ContainsAnyTopLevelField(string json, params string[] fieldNames)
        {
            if (string.IsNullOrWhiteSpace(json) || fieldNames == null || fieldNames.Length == 0)
            {
                return false;
            }

            var legacyFields = new HashSet<string>(fieldNames, StringComparer.Ordinal);
            foreach (string fieldName in EnumerateTopLevelFieldNames(json))
            {
                if (legacyFields.Contains(fieldName))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateTopLevelFieldNames(string json)
        {
            int index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{')
            {
                yield break;
            }

            index++;
            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] == '}')
                {
                    yield break;
                }

                if (json[index] != '"')
                {
                    yield break;
                }

                string fieldName = ReadJsonString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                {
                    yield break;
                }

                index++;
                yield return fieldName;
                SkipJsonValue(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',')
                {
                    index++;
                }
            }
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        private static string ReadJsonString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"')
            {
                return string.Empty;
            }

            index++;
            var chars = new List<char>();
            while (index < json.Length)
            {
                char current = json[index++];
                if (current == '"')
                {
                    break;
                }

                if (current == '\\' && index < json.Length)
                {
                    chars.Add(json[index++]);
                    continue;
                }

                chars.Add(current);
            }

            return new string(chars.ToArray());
        }

        private static void SkipJsonValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                return;
            }

            if (json[index] == '"')
            {
                ReadJsonString(json, ref index);
                return;
            }

            if (json[index] == '{' || json[index] == '[')
            {
                char open = json[index];
                char close = open == '{' ? '}' : ']';
                int depth = 1;
                index++;
                while (index < json.Length && depth > 0)
                {
                    if (json[index] == '"')
                    {
                        ReadJsonString(json, ref index);
                        continue;
                    }

                    if (json[index] == open)
                    {
                        depth++;
                    }
                    else if (json[index] == close)
                    {
                        depth--;
                    }

                    index++;
                }

                return;
            }

            while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']')
            {
                index++;
            }
        }

        private static bool ReadAndValidateHeader(
            BinaryReader reader,
            int expectedMagic,
            out int codecVersion,
            out int packageVersion,
            out string error)
        {
            codecVersion = 0;
            packageVersion = 0;
            error = string.Empty;

            int magic = reader.ReadInt32();
            if (magic != expectedMagic)
            {
                error = $"配置包魔数不匹配: 0x{magic:X8}。";
                return false;
            }

            codecVersion = reader.ReadInt32();
            if (codecVersion < HolmasConfigBinaryFormat.MinSupportedVersion ||
                codecVersion > HolmasConfigBinaryFormat.CurrentVersion)
            {
                error = $"配置包编解码版本不支持: {codecVersion}。";
                return false;
            }

            packageVersion = reader.ReadInt32();
            return true;
        }

        private static void WriteMapRows(BinaryWriter writer, HolmasMapTableRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasMapTableRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasMapTableRow row = rows[i] ?? new HolmasMapTableRow();
                WriteString(writer, row.mapId);
                WriteString(writer, row.terrainPath);
                writer.Write(row.catCountMin);
                writer.Write(row.catCountMax);
            }
        }

        private static HolmasMapTableRow[] ReadMapRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasMapTableRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasMapTableRow
                {
                    mapId = ReadString(reader),
                    terrainPath = ReadString(reader),
                    catCountMin = reader.ReadInt32(),
                    catCountMax = reader.ReadInt32(),
                };
            }

            return rows;
        }

        private static void WriteCatRows(BinaryWriter writer, HolmasCatTableRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasCatTableRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasCatTableRow row = rows[i] ?? new HolmasCatTableRow();
                WriteString(writer, row.catId);
                WriteString(writer, row.catName);
                WriteString(writer, row.iconPath);
                writer.Write(row.rarity);
                writer.Write(row.weight);
                writer.Write(row.price);
            }
        }

        private static HolmasCatTableRow[] ReadCatRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasCatTableRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasCatTableRow
                {
                    catId = ReadString(reader),
                    catName = ReadString(reader),
                    iconPath = ReadString(reader),
                    rarity = reader.ReadInt32(),
                    weight = reader.ReadInt32(),
                    price = reader.ReadInt32(),
                };
            }

            return rows;
        }

        private static void WriteTaskRows(BinaryWriter writer, HolmasTaskTableRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasTaskTableRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasTaskTableRow row = rows[i] ?? new HolmasTaskTableRow();
                WriteString(writer, row.taskTypeId);
                writer.Write((byte)row.taskKind);
                WriteStringArray(writer, row.catIdList);
                writer.Write(row.countMin);
                writer.Write(row.countMax);
                WriteIntArray(writer, row.rewardArray);
                writer.Write(row.levelRewardFactor);
            }
        }

        private static HolmasTaskTableRow[] ReadTaskRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasTaskTableRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasTaskTableRow
                {
                    taskTypeId = ReadString(reader),
                    taskKind = (HolmasTaskKind)reader.ReadByte(),
                    catIdList = ReadStringArray(reader),
                    countMin = reader.ReadInt32(),
                    countMax = reader.ReadInt32(),
                    rewardArray = ReadIntArray(reader),
                    levelRewardFactor = reader.ReadSingle(),
                };
            }

            return rows;
        }

        private static void WritePlayerLevelRows(BinaryWriter writer, HolmasPlayerLevelTableRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasPlayerLevelTableRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasPlayerLevelTableRow row = rows[i] ?? new HolmasPlayerLevelTableRow();
                writer.Write(row.playerLevel);
                writer.Write(row.minExperience);
                writer.Write(row.offlineRewardPerHour);
                writer.Write(row.adUnlockHours);
                WriteStringArray(writer, row.taskTypeIds);
                WriteIntArray(writer, row.taskTypeWeights);
                WriteStringArray(writer, row.mapIds);
                WriteIntArray(writer, row.mapWeights);
            }
        }

        private static HolmasPlayerLevelTableRow[] ReadPlayerLevelRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasPlayerLevelTableRow[count];
            for (int i = 0; i < count; i++)
            {
                int playerLevel = reader.ReadInt32();
                int minExperience = reader.ReadInt32();
                rows[i] = new HolmasPlayerLevelTableRow
                {
                    playerLevel = playerLevel,
                    minExperience = minExperience,
                    offlineRewardPerHour = reader.ReadInt32(),
                    adUnlockHours = reader.ReadInt32(),
                    taskTypeIds = ReadStringArray(reader),
                    taskTypeWeights = ReadIntArray(reader),
                    mapIds = ReadStringArray(reader),
                    mapWeights = ReadIntArray(reader),
                };
            }

            return rows;
        }

        private static void WriteAgencyBuildingTableRows(BinaryWriter writer, HolmasAgencyBuildingTableRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasAgencyBuildingTableRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasAgencyBuildingTableRow row = rows[i] ?? new HolmasAgencyBuildingTableRow();
                writer.Write(row.agencyStageId);
                WriteString(writer, row.stageName);
                WriteStringArray(writer, row.promotionIds);
                WriteIntArray(writer, row.promotionLevelCaps);
                WriteAgencyBuildingTableCostRows(writer, row.promotionUpgradeCosts);
                WriteString(writer, row.notes);
            }
        }

        private static HolmasAgencyBuildingTableRow[] ReadAgencyBuildingTableRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasAgencyBuildingTableRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasAgencyBuildingTableRow
                {
                    agencyStageId = reader.ReadInt32(),
                    stageName = ReadString(reader),
                    promotionIds = ReadStringArray(reader),
                    promotionLevelCaps = ReadIntArray(reader),
                    promotionUpgradeCosts = ReadAgencyBuildingTableCostRows(reader),
                    notes = ReadString(reader),
                };
            }

            return rows;
        }

        private static void WriteAgencyBuildingTableCostRows(BinaryWriter writer, HolmasAgencyBuildingTableCostRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasAgencyBuildingTableCostRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasAgencyBuildingTableCostRow row = rows[i] ?? new HolmasAgencyBuildingTableCostRow();
                WriteIntArray(writer, row.costs);
            }
        }

        private static HolmasAgencyBuildingTableCostRow[] ReadAgencyBuildingTableCostRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasAgencyBuildingTableCostRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasAgencyBuildingTableCostRow
                {
                    costs = ReadIntArray(reader),
                };
            }

            return rows;
        }

        private static void WriteLeaderboardRows(BinaryWriter writer, HolmasLeaderboardTableRow[] rows)
        {
            rows = rows ?? Array.Empty<HolmasLeaderboardTableRow>();
            writer.Write(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                HolmasLeaderboardTableRow row = rows[i] ?? new HolmasLeaderboardTableRow();
                WriteString(writer, row.leaderboardType);
                WriteString(writer, row.displayName);
                WriteString(writer, row.periodType);
                WriteString(writer, row.timeZoneId);
                writer.Write(row.resetDayOfWeek);
                writer.Write(row.resetHour);
                writer.Write(row.resetMinute);
                writer.Write(row.topEntryCount);
                writer.Write(row.mockEntryCount);
                writer.Write(row.isEnabled);
                WriteString(writer, row.notes);
            }
        }

        private static HolmasLeaderboardTableRow[] ReadLeaderboardRowsOrDefault(BinaryReader reader)
        {
            if (reader == null || reader.BaseStream == null || reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                return Array.Empty<HolmasLeaderboardTableRow>();
            }

            return ReadLeaderboardRows(reader);
        }

        private static HolmasLeaderboardTableRow[] ReadLeaderboardRows(BinaryReader reader)
        {
            int count = ReadNonNegativeCount(reader);
            var rows = new HolmasLeaderboardTableRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new HolmasLeaderboardTableRow
                {
                    leaderboardType = ReadString(reader),
                    displayName = ReadString(reader),
                    periodType = ReadString(reader),
                    timeZoneId = ReadString(reader),
                    resetDayOfWeek = reader.ReadInt32(),
                    resetHour = reader.ReadInt32(),
                    resetMinute = reader.ReadInt32(),
                    topEntryCount = reader.ReadInt32(),
                    mockEntryCount = reader.ReadInt32(),
                    isEnabled = reader.ReadBoolean(),
                    notes = ReadString(reader),
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
