using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using App.HotUpdate.Holmas.Tasks.Config;
using Holmas.EditorTools;
using NUnit.Framework;

namespace Holmas.EditorTests
{
    public sealed class HolmasConfigBinaryExporterTests
    {
        [Test]
        public void HolmasConfigBinaryExporter_ExportsReadablePackagesFromXlsx()
        {
            using (var fixture = CreateFixture(includeExtraMetaLevel: false))
            {
                HolmasConfigExportReport report = HolmasConfigBinaryExporter.ExportAll(
                    fixture.ConfigRoot,
                    fixture.JsonRoot,
                    fixture.BinaryRoot,
                    refreshAssetDatabase: false);

                Assert.That(report, Is.Not.Null);
                Assert.That(report.Success, Is.True, string.Join("\n", report.Errors));

                string corePath = Path.Combine(fixture.BinaryRoot, "holmas_core_config.bytes");
                string catPath = Path.Combine(fixture.BinaryRoot, "holmas_cat_meta.bytes");
                string reportPath = Path.Combine(fixture.JsonRoot, "holmas_export_report.json");

                Assert.That(File.Exists(corePath), Is.True, corePath);
                Assert.That(File.Exists(catPath), Is.True, catPath);
                Assert.That(File.Exists(reportPath), Is.True, reportPath);

                bool success = HolmasConfigCatalogFactory.TryCreateFromBinary(
                    File.ReadAllBytes(corePath),
                    File.ReadAllBytes(catPath),
                    out HolmasConfigCatalogBundle bundle,
                    out HolmasConfigReport runtimeReport);

                Assert.That(success, Is.True, runtimeReport == null ? "runtime report missing" : string.Join("\n", runtimeReport.Errors));
                Assert.That(bundle, Is.Not.Null);
                Assert.That(bundle.Report.Errors, Is.Empty);
                Assert.That(bundle.Cats.Count, Is.EqualTo(2));
                Assert.That(bundle.Maps.Count, Is.EqualTo(1));
                Assert.That(bundle.TaskTemplates.Count, Is.EqualTo(1));
                Assert.That(bundle.PlayerLevels.Count, Is.EqualTo(20));
                Assert.That(bundle.MetaLevels.Count, Is.EqualTo(20));
                Assert.That(bundle.PlayerLevels.Last().UpgradeExp, Is.EqualTo(2000));
                Assert.That(bundle.MetaLevels.Last().MinExperience, Is.EqualTo(2000));
            }
        }

        [Test]
        public void HolmasConfigBinaryExporter_RejectsMetaLevelsBeyondFormalRange()
        {
            using (var fixture = CreateFixture(includeExtraMetaLevel: true))
            {
                HolmasConfigExportReport report = HolmasConfigBinaryExporter.ExportAll(
                    fixture.ConfigRoot,
                    fixture.JsonRoot,
                    fixture.BinaryRoot,
                    refreshAssetDatabase: false);

                Assert.That(report, Is.Not.Null);
                Assert.That(report.Success, Is.False);
                Assert.That(report.Errors.Any(item => item.Contains("Holmas_MetaLevelTable 行数必须为 20")), Is.True, string.Join("\n", report.Errors));
            }
        }

        private static ExportFixture CreateFixture(bool includeExtraMetaLevel)
        {
            string root = Path.Combine(Path.GetTempPath(), "holmas_xlsx_export_test_" + Guid.NewGuid().ToString("N"));
            string configRoot = Path.Combine(root, "Config");
            string jsonRoot = Path.Combine(root, "json");
            string binaryRoot = Path.Combine(root, "binary");
            Directory.CreateDirectory(configRoot);
            Directory.CreateDirectory(jsonRoot);
            Directory.CreateDirectory(binaryRoot);

            WriteWorkbook(
                Path.Combine(configRoot, "Holmas_MapTable.xlsx"),
                "Sheet1",
                new[]
                {
                    new[] { "地图id", "地图对应的数据地址Path", "猫的最大数量max", "猫的最小数量mini" },
                    new[] { "mapId", "terrainPath", "catCountMax", "catCountMin" },
                    new[] { "map_001", "Assets/HotUpdateContent/Res/Map/1.asset", "15", "12" },
                });

            WriteWorkbook(
                Path.Combine(configRoot, "Holmas_CatTable.xlsx"),
                "Holmas_CatTable",
                new[]
                {
                    new[] { "猫的id", "猫的名称", "对应的猫的图片资源地址path", "稀有度", "出现的权重值", "价格" },
                    new[] { "catId", "catName", "iconPath", "rarity", "weight", "price" },
                    new[] { "1", "布偶猫", "Assets/HotUpdateContent/Res/Icons/cat_01.png", "1", "1000", "1000" },
                    new[] { "2", "短毛猫", "Assets/HotUpdateContent/Res/Icons/cat_02.png", "1", "900", "1200" },
                });

            WriteWorkbook(
                Path.Combine(configRoot, "Holmas_TaskTable.xlsx"),
                "Holmas_TaskTable",
                new[]
                {
                    new[] { "任务种类id", "任务类型", "对应的猫的id数组", "数量的最大值max", "数量的最小值mini", "奖励数组", "等级奖励系数" },
                    new[] { "taskTypeId", "taskKind", "catIdList", "countMax", "countMin", "rewardArray", "levelRewardFactor" },
                    new[] { "task_001", "Money", "1;2", "2", "1", "", "1" },
                });

            WriteWorkbook(
                Path.Combine(configRoot, "Holmas_PlayerLevelTable.xlsx"),
                "Holmas_PlayerLevelTable",
                BuildPlayerLevelRows());

            WriteWorkbook(
                Path.Combine(configRoot, "Holmas_MetaLevelTable.xlsx"),
                "Holmas_MetaLevelTable",
                BuildMetaLevelRows(includeExtraMetaLevel));

            WriteWorkbook(
                Path.Combine(configRoot, "Holmas_AgencyBuildingTable.xlsx"),
                "Holmas_AgencyBuildingTable",
                BuildAgencyRows());

            return new ExportFixture(root, configRoot, jsonRoot, binaryRoot);
        }

        private static string[][] BuildPlayerLevelRows()
        {
            var rows = new string[22][];
            rows[0] = new[] { "玩家等级", "任务id组", "任务id组中任务出现权重", "地图id数组", "地图id权重数组" };
            rows[1] = new[] { "playerLevel", "taskTypeIds", "taskTypeWeights", "mapIds", "mapWeights" };
            for (int level = 1; level <= 20; level++)
            {
                rows[level + 1] = new[]
                {
                    level.ToString(),
                    "task_001",
                    "100",
                    "map_001",
                    "100",
                };
            }

            return rows;
        }

        private static string[][] BuildMetaLevelRows(bool includeExtraMetaLevel)
        {
            int rowCount = includeExtraMetaLevel ? 24 : 23;
            var rows = new string[rowCount][];
            rows[0] = new[] { "玩家等级", "达到该等级所需累计经验", "每小时离线奖励", "广告解锁时长", "备注" };
            rows[1] = new[] { "playerLevel", "minExperience", "offlineRewardPerHour", "adUnlockHours", "notes" };

            int[] minExperience =
            {
                0, 40, 85, 135, 190, 250, 320, 400, 490, 590,
                700, 825, 965, 1120, 1290, 1475, 1675, 1840, 1930, 2000
            };

            for (int level = 1; level <= 20; level++)
            {
                rows[level + 1] = new[]
                {
                    level.ToString(),
                    minExperience[level - 1].ToString(),
                    (6000 + (level - 1) * 600).ToString(),
                    "8",
                    string.Empty,
                };
            }

            if (includeExtraMetaLevel)
            {
                rows[22] = new[] { "21", "2101", "18000", "8", string.Empty };
                rows[23] = Array.Empty<string>();
            }

            return rows;
        }

        private static string[][] BuildAgencyRows()
        {
            var rows = new string[102][];
            rows[0] = new[] { "城市阶段id", "城市名", "宣传功能id数组", "宣传升级级数上限数组", "宣传升级费用数组", "备注" };
            rows[1] = new[] { "agencyStageId", "stageName", "promotionIds", "promotionLevelCaps", "promotionUpgradeCosts", "notes" };

            string promotionIds = "leaflet;radio;online;tv";
            string promotionCaps = "5;5;5;5";
            string promotionCosts = "100;200;300;400;500|120;240;360;480;600|140;280;420;560;700|160;320;480;640;800";
            for (int stage = 1; stage <= 100; stage++)
            {
                rows[stage + 1] = new[]
                {
                    stage.ToString(),
                    "城市" + stage.ToString("D3"),
                    promotionIds,
                    promotionCaps,
                    promotionCosts,
                    string.Empty,
                };
            }

            return rows;
        }

        private static void WriteWorkbook(string path, string sheetName, string[][] rows)
        {
            using (var stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteEntry(archive, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "</Types>");

                WriteEntry(archive, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                WriteEntry(archive, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"" + EscapeXml(sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                    "</workbook>");

                WriteEntry(archive, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "</Relationships>");

                WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
            }
        }

        private static string BuildWorksheetXml(string[][] rows)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            builder.Append("<sheetData>");

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                string[] row = rows[rowIndex] ?? Array.Empty<string>();
                builder.Append("<row r=\"").Append(rowIndex + 1).Append("\">");
                for (int columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    string value = row[columnIndex] ?? string.Empty;
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    builder.Append("<c r=\"")
                        .Append(GetColumnName(columnIndex))
                        .Append(rowIndex + 1)
                        .Append("\" t=\"inlineStr\"><is><t>")
                        .Append(EscapeXml(value))
                        .Append("</t></is></c>");
                }

                builder.Append("</row>");
            }

            builder.Append("</sheetData></worksheet>");
            return builder.ToString();
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static string GetColumnName(int columnIndex)
        {
            int value = columnIndex + 1;
            var builder = new StringBuilder();
            while (value > 0)
            {
                int remainder = (value - 1) % 26;
                builder.Insert(0, (char)('A' + remainder));
                value = (value - 1) / 26;
            }

            return builder.ToString();
        }

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private sealed class ExportFixture : IDisposable
        {
            public ExportFixture(string root, string configRoot, string jsonRoot, string binaryRoot)
            {
                Root = root;
                ConfigRoot = configRoot;
                JsonRoot = jsonRoot;
                BinaryRoot = binaryRoot;
            }

            public string Root { get; }
            public string ConfigRoot { get; }
            public string JsonRoot { get; }
            public string BinaryRoot { get; }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
        }
    }
}
