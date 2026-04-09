#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace Holmas.EditorTools
{
    internal static class HolmasXlsxTableReader
    {
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string DocumentRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static bool TryReadFirstWorksheet(string path, out List<string[]> rows, out string error)
        {
            rows = new List<string[]>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "xlsx 路径为空。";
                return false;
            }

            if (!File.Exists(path))
            {
                error = $"找不到 xlsx 文件: {path}";
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(path))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                {
                    Dictionary<int, string> sharedStrings = ReadSharedStrings(archive);
                    string worksheetPath = ResolveFirstWorksheetPath(archive);
                    if (string.IsNullOrWhiteSpace(worksheetPath))
                    {
                        error = $"xlsx 缺少工作表: {path}";
                        return false;
                    }

                    ZipArchiveEntry worksheetEntry = archive.GetEntry(worksheetPath);
                    if (worksheetEntry == null)
                    {
                        error = $"xlsx 找不到工作表内容: {worksheetPath}";
                        return false;
                    }

                    rows = ReadWorksheetRows(worksheetEntry, sharedStrings);
                    return true;
                }
            }
            catch (Exception ex)
            {
                rows = new List<string[]>();
                error = ex.Message;
                return false;
            }
        }

        private static Dictionary<int, string> ReadSharedStrings(ZipArchive archive)
        {
            var lookup = new Dictionary<int, string>();
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return lookup;
            }

            XmlDocument document = LoadXml(entry);
            var manager = CreateNamespaceManager(document.NameTable);
            XmlNodeList items = document.SelectNodes("/a:sst/a:si", manager);
            if (items == null)
            {
                return lookup;
            }

            for (int index = 0; index < items.Count; index++)
            {
                XmlNode item = items[index];
                lookup[index] = ReadRichText(item, manager);
            }

            return lookup;
        }

        private static string ResolveFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relationsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry == null || relationsEntry == null)
            {
                return string.Empty;
            }

            XmlDocument workbookDocument = LoadXml(workbookEntry);
            XmlDocument relationsDocument = LoadXml(relationsEntry);
            var workbookManager = CreateNamespaceManager(workbookDocument.NameTable);
            var relationManager = CreateNamespaceManager(relationsDocument.NameTable);

            XmlNode firstSheet = workbookDocument.SelectSingleNode("/a:workbook/a:sheets/a:sheet", workbookManager);
            if (firstSheet == null || firstSheet.Attributes == null)
            {
                return string.Empty;
            }

            XmlAttribute relationAttribute = firstSheet.Attributes["id", DocumentRelationshipNamespace];
            if (relationAttribute == null || string.IsNullOrWhiteSpace(relationAttribute.Value))
            {
                return string.Empty;
            }

            string relationId = relationAttribute.Value;
            XmlNode relationNode = relationsDocument.SelectSingleNode(
                "/p:Relationships/p:Relationship[@Id='" + relationId + "']",
                relationManager);
            if (relationNode == null || relationNode.Attributes == null)
            {
                return string.Empty;
            }

            string target = relationNode.Attributes["Target"]?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            return target.StartsWith("/", StringComparison.Ordinal)
                ? target.TrimStart('/')
                : "xl/" + target.TrimStart('/');
        }

        private static List<string[]> ReadWorksheetRows(ZipArchiveEntry worksheetEntry, Dictionary<int, string> sharedStrings)
        {
            var rows = new List<string[]>();
            XmlDocument document = LoadXml(worksheetEntry);
            var manager = CreateNamespaceManager(document.NameTable);
            XmlNodeList rowNodes = document.SelectNodes("/a:worksheet/a:sheetData/a:row", manager);
            if (rowNodes == null)
            {
                return rows;
            }

            foreach (XmlNode rowNode in rowNodes)
            {
                XmlNodeList cellNodes = rowNode.SelectNodes("a:c", manager);
                if (cellNodes == null || cellNodes.Count == 0)
                {
                    rows.Add(Array.Empty<string>());
                    continue;
                }

                int maxColumnIndex = -1;
                var values = new Dictionary<int, string>();
                foreach (XmlNode cellNode in cellNodes)
                {
                    int columnIndex = GetColumnIndex(cellNode);
                    if (columnIndex < 0)
                    {
                        continue;
                    }

                    maxColumnIndex = Math.Max(maxColumnIndex, columnIndex);
                    values[columnIndex] = ReadCellValue(cellNode, manager, sharedStrings);
                }

                if (maxColumnIndex < 0)
                {
                    rows.Add(Array.Empty<string>());
                    continue;
                }

                var row = new string[maxColumnIndex + 1];
                foreach (KeyValuePair<int, string> pair in values)
                {
                    row[pair.Key] = pair.Value ?? string.Empty;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string ReadCellValue(XmlNode cellNode, XmlNamespaceManager manager, Dictionary<int, string> sharedStrings)
        {
            string cellType = cellNode.Attributes?["t"]?.Value ?? string.Empty;
            if (string.Equals(cellType, "inlineStr", StringComparison.Ordinal))
            {
                XmlNode inlineNode = cellNode.SelectSingleNode("a:is", manager);
                return inlineNode == null ? string.Empty : ReadRichText(inlineNode, manager);
            }

            XmlNode valueNode = cellNode.SelectSingleNode("a:v", manager);
            if (valueNode == null)
            {
                return string.Empty;
            }

            string rawValue = valueNode.InnerText ?? string.Empty;
            if (string.Equals(cellType, "s", StringComparison.Ordinal))
            {
                if (int.TryParse(rawValue, out int sharedIndex) && sharedStrings.TryGetValue(sharedIndex, out string sharedValue))
                {
                    return sharedValue ?? string.Empty;
                }

                return string.Empty;
            }

            return rawValue;
        }

        private static string ReadRichText(XmlNode node, XmlNamespaceManager manager)
        {
            if (node == null)
            {
                return string.Empty;
            }

            XmlNodeList textNodes = node.SelectNodes(".//a:t", manager);
            if (textNodes == null || textNodes.Count == 0)
            {
                return node.InnerText ?? string.Empty;
            }

            var buffer = new System.Text.StringBuilder();
            foreach (XmlNode textNode in textNodes)
            {
                buffer.Append(textNode.InnerText ?? string.Empty);
            }

            return buffer.ToString();
        }

        private static int GetColumnIndex(XmlNode cellNode)
        {
            string reference = cellNode?.Attributes?["r"]?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reference))
            {
                return -1;
            }

            int value = 0;
            for (int i = 0; i < reference.Length; i++)
            {
                char ch = reference[i];
                if (!char.IsLetter(ch))
                {
                    break;
                }

                value = (value * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
            }

            return value - 1;
        }

        private static XmlDocument LoadXml(ZipArchiveEntry entry)
        {
            var document = new XmlDocument();
            using (Stream stream = entry.Open())
            {
                document.Load(stream);
            }

            return document;
        }

        private static XmlNamespaceManager CreateNamespaceManager(XmlNameTable nameTable)
        {
            var manager = new XmlNamespaceManager(nameTable);
            manager.AddNamespace("a", SpreadsheetNamespace);
            manager.AddNamespace("p", PackageRelationshipNamespace);
            return manager;
        }
    }
}
#endif
