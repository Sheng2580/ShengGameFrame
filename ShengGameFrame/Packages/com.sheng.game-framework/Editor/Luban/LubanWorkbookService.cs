using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// 创建和同步 Luban 使用的单工作表 Excel 文件
    /// </summary>
    public static class LubanWorkbookService
    {
        private static readonly XNamespace SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static LubanTableSyncResult SynchronizeTable(
            LubanTableDescriptor table,
            string dataDirectory,
            string backupRoot,
            bool removeObsoleteColumns)
        {
            LubanTableSyncResult result = AnalyzeTable(table, dataDirectory);
            if (result.Errors.Count > 0)
            {
                return result;
            }

            if (result.RemovedColumns.Count > 0 && !removeObsoleteColumns)
            {
                result.Success = false;
                result.RequiresRemovalConfirmation = true;
                return result;
            }

            string filePath = result.FilePath;
            List<List<string>> oldRows = File.Exists(filePath)
                ? ReadWorkbook(filePath)
                : new List<List<string>>();
            Dictionary<string, int> oldColumns = ReadColumnIndexes(oldRows);
            List<List<string>> newRows = BuildTableRows(table, oldRows, oldColumns);
            try
            {
                if (File.Exists(filePath))
                {
                    BackupWorkbook(filePath, backupRoot);
                }

                WriteWorkbook(
                    filePath,
                    table.TableName,
                    newRows,
                    4);
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
            }

            return result;
        }

        public static LubanTableSyncResult AnalyzeTable(
            LubanTableDescriptor table,
            string dataDirectory)
        {
            LubanTableSyncResult result = new LubanTableSyncResult
            {
                TableName = table?.TableName ?? string.Empty,
                FilePath = table == null
                    ? string.Empty
                    : Path.Combine(dataDirectory, table.FileName)
            };
            if (table == null)
            {
                result.Errors.Add("表描述不能为空");
                return result;
            }

            List<List<string>> oldRows;
            try
            {
                oldRows = File.Exists(result.FilePath)
                    ? ReadWorkbook(result.FilePath)
                    : new List<List<string>>();
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                return result;
            }

            Dictionary<string, int> oldColumns = ReadColumnIndexes(oldRows);
            HashSet<string> currentColumns = new HashSet<string>(
                table.Columns.Select(column => column.Name),
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < table.Columns.Count; i++)
            {
                string formerName = table.Columns[i].FormerName;
                if (!string.IsNullOrEmpty(formerName))
                {
                    currentColumns.Add(formerName);
                }
            }

            foreach (string oldColumn in oldColumns.Keys)
            {
                if (!currentColumns.Contains(oldColumn))
                {
                    result.RemovedColumns.Add(oldColumn);
                }
            }

            for (int i = 0; i < table.Columns.Count; i++)
            {
                LubanColumnDescriptor column = table.Columns[i];
                if (!oldColumns.ContainsKey(column.Name)
                    && (string.IsNullOrEmpty(column.FormerName)
                        || !oldColumns.ContainsKey(column.FormerName)))
                {
                    result.AddedColumns.Add(column.Name);
                }
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }

        public static void WriteTableDefinitions(
            IReadOnlyList<LubanTableDescriptor> tables,
            string path)
        {
            List<List<string>> rows = new List<List<string>>
            {
                new List<string>
                {
                    "##var",
                    "full_name",
                    "value_type",
                    "read_schema_from_file",
                    "input",
                    "index",
                    "mode",
                    "group",
                    "comment",
                    "tags",
                    "output"
                },
                new List<string>
                {
                    "##",
                    "完整表类型名",
                    "数据类型",
                    "从数据文件读取结构",
                    "输入文件",
                    "主键字段",
                    "表模式",
                    "分组",
                    "注释",
                    "标签",
                    "输出文件名"
                },
                new List<string>
                {
                    "##",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty
                }
            };

            if (tables != null)
            {
                for (int i = 0; i < tables.Count; i++)
                {
                    LubanTableDescriptor table = tables[i];
                    rows.Add(new List<string>
                    {
                        string.Empty,
                        "cfg." + table.TableTypeName,
                        table.ValueTypeName,
                        "true",
                        table.FileName,
                        table.KeyColumn.Name,
                        "map",
                        table.Group,
                        table.Comment,
                        string.Empty,
                        table.OutputName
                    });
                }
            }

            WriteWorkbook(path, "tables", rows, 3);
        }

        public static List<string> ValidateTable(
            LubanTableDescriptor table,
            string filePath)
        {
            List<string> errors = new List<string>();
            if (!File.Exists(filePath))
            {
                errors.Add($"缺少表文件 {filePath}");
                return errors;
            }

            List<List<string>> rows;
            try
            {
                rows = ReadWorkbook(filePath);
            }
            catch (Exception exception)
            {
                errors.Add($"无法读取 {filePath} {exception.Message}");
                return errors;
            }

            Dictionary<string, int> columns = ReadColumnIndexes(rows);
            HashSet<string> expectedColumns = new HashSet<string>(
                table.Columns.Select(column => column.Name),
                StringComparer.OrdinalIgnoreCase);
            foreach (string existingColumn in columns.Keys)
            {
                if (!expectedColumns.Contains(existingColumn))
                {
                    errors.Add($"{table.TableName} 存在已经从 C# 删除的字段 {existingColumn}");
                }
            }

            for (int i = 0; i < table.Columns.Count; i++)
            {
                LubanColumnDescriptor column = table.Columns[i];
                if (!columns.TryGetValue(column.Name, out int columnIndex))
                {
                    errors.Add($"{table.TableName} 缺少字段 {column.Name}");
                    continue;
                }

                string actualType = GetValue(rows, 1, columnIndex);
                if (!string.Equals(
                        actualType,
                        column.LubanType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"{table.TableName}.{column.Name} 类型应为 {column.LubanType} 当前为 {actualType}");
                }
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            for (int rowIndex = 4; rowIndex < rows.Count; rowIndex++)
            {
                if (IsDataRowEmpty(rows[rowIndex]))
                {
                    continue;
                }

                for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    LubanColumnDescriptor column = table.Columns[columnIndex];
                    if (!columns.TryGetValue(column.Name, out int sourceColumnIndex))
                    {
                        continue;
                    }

                    string value = GetValue(rows, rowIndex, sourceColumnIndex);
                    if (column.Required && string.IsNullOrWhiteSpace(value))
                    {
                        errors.Add(
                            $"{table.TableName} 第 {rowIndex + 1} 行字段 {column.Name} 不能为空");
                    }

                    if (column.IsEnum
                        && !string.IsNullOrWhiteSpace(value)
                        && !column.LubanType.StartsWith("list", StringComparison.Ordinal)
                        && !Enum.TryParse(column.EnumType, value, true, out _))
                    {
                        errors.Add(
                            $"{table.TableName} 第 {rowIndex + 1} 行字段 {column.Name} 枚举值无效 {value}");
                    }
                }

                if (columns.TryGetValue(table.KeyColumn.Name, out int keyColumnIndex))
                {
                    string key = GetValue(rows, rowIndex, keyColumnIndex);
                    if (!string.IsNullOrWhiteSpace(key) && !keys.Add(key))
                    {
                        errors.Add(
                            $"{table.TableName} 第 {rowIndex + 1} 行主键重复 {key}");
                    }
                }
            }

            return errors;
        }

        public static List<List<string>> ReadWorkbook(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (ZipArchive archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Read,
                       false,
                       Utf8WithoutBom))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                ZipArchiveEntry sheetEntry = archive.GetEntry(
                    "xl/worksheets/sheet1.xml");
                if (sheetEntry == null)
                {
                    throw new InvalidDataException("Excel 缺少第一个工作表");
                }

                XDocument document;
                using (Stream sheetStream = sheetEntry.Open())
                {
                    document = XDocument.Load(sheetStream);
                }

                List<List<string>> rows = new List<List<string>>();
                IEnumerable<XElement> rowElements = document
                    .Descendants(SpreadsheetNamespace + "row");
                foreach (XElement rowElement in rowElements)
                {
                    int rowIndex = ParseIntAttribute(rowElement, "r", rows.Count + 1) - 1;
                    while (rows.Count <= rowIndex)
                    {
                        rows.Add(new List<string>());
                    }

                    foreach (XElement cell in rowElement.Elements(
                                 SpreadsheetNamespace + "c"))
                    {
                        string reference = cell.Attribute("r")?.Value ?? string.Empty;
                        int columnIndex = ParseColumnIndex(reference);
                        EnsureColumnCount(rows[rowIndex], columnIndex + 1);
                        rows[rowIndex][columnIndex] = ReadCellValue(cell, sharedStrings);
                    }
                }

                return rows;
            }
        }

        public static void WriteWorkbook(
            string path,
            string sheetName,
            IReadOnlyList<List<string>> rows,
            int freezeRows)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = File.Create(temporaryPath))
                using (ZipArchive archive = new ZipArchive(
                           stream,
                           ZipArchiveMode.Create,
                           false,
                           Utf8WithoutBom))
                {
                    WriteEntry(archive, "[Content_Types].xml", CreateContentTypes());
                    WriteEntry(archive, "_rels/.rels", CreateRootRelationships());
                    WriteEntry(archive, "docProps/app.xml", CreateAppProperties());
                    WriteEntry(archive, "docProps/core.xml", CreateCoreProperties());
                    WriteEntry(archive, "xl/workbook.xml", CreateWorkbook(sheetName));
                    WriteEntry(
                        archive,
                        "xl/_rels/workbook.xml.rels",
                        CreateWorkbookRelationships());
                    WriteEntry(archive, "xl/styles.xml", CreateStyles());
                    WriteEntry(
                        archive,
                        "xl/worksheets/sheet1.xml",
                        CreateWorksheet(rows, freezeRows));
                }

                ReplaceFile(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static List<List<string>> BuildTableRows(
            LubanTableDescriptor table,
            IReadOnlyList<List<string>> oldRows,
            IReadOnlyDictionary<string, int> oldColumns)
        {
            int rowCount = Math.Max(4, oldRows.Count);
            List<List<string>> rows = new List<List<string>>(rowCount);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                rows.Add(new List<string>(table.Columns.Count + 1));
                EnsureColumnCount(rows[rowIndex], table.Columns.Count + 1);
            }

            rows[0][0] = "##var";
            rows[1][0] = "##type";
            rows[2][0] = "##group";
            rows[3][0] = "##";

            for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                LubanColumnDescriptor column = table.Columns[columnIndex];
                int targetColumnIndex = columnIndex + 1;
                rows[0][targetColumnIndex] = column.Name;
                rows[1][targetColumnIndex] = column.LubanType;
                rows[2][targetColumnIndex] = table.Group;
                rows[3][targetColumnIndex] = column.Comment;

                int sourceColumnIndex = -1;
                if (!oldColumns.TryGetValue(column.Name, out int existingColumnIndex))
                {
                    if (!string.IsNullOrEmpty(column.FormerName)
                        && oldColumns.TryGetValue(
                            column.FormerName,
                            out int formerColumnIndex))
                    {
                        sourceColumnIndex = formerColumnIndex;
                    }
                }
                else
                {
                    sourceColumnIndex = existingColumnIndex;
                }

                for (int rowIndex = 4; rowIndex < rowCount; rowIndex++)
                {
                    string value = sourceColumnIndex >= 0
                        ? GetValue(oldRows, rowIndex, sourceColumnIndex)
                        : column.DefaultValue;
                    rows[rowIndex][targetColumnIndex] = value ?? string.Empty;
                }
            }

            return rows;
        }

        private static Dictionary<string, int> ReadColumnIndexes(
            IReadOnlyList<List<string>> rows)
        {
            Dictionary<string, int> columns = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            if (rows.Count == 0)
            {
                return columns;
            }

            for (int i = 1; i < rows[0].Count; i++)
            {
                string name = rows[0][i]?.Trim();
                if (!string.IsNullOrEmpty(name) && !columns.ContainsKey(name))
                {
                    columns.Add(name, i);
                }
            }

            return columns;
        }

        private static void BackupWorkbook(string path, string backupRoot)
        {
            string backupDirectory = Path.Combine(
                backupRoot,
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(backupDirectory);
            string backupPath = Path.Combine(
                backupDirectory,
                Path.GetFileName(path));
            if (File.Exists(backupPath))
            {
                backupPath = Path.Combine(
                    backupDirectory,
                    Path.GetFileNameWithoutExtension(path)
                    + "_"
                    + Guid.NewGuid().ToString("N").Substring(0, 6)
                    + Path.GetExtension(path));
            }

            File.Copy(path, backupPath, false);
        }

        private static void ReplaceFile(string temporaryPath, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path);
                return;
            }

            try
            {
                File.Replace(temporaryPath, path, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            XDocument document;
            using (Stream stream = entry.Open())
            {
                document = XDocument.Load(stream);
            }

            return document
                .Descendants(SpreadsheetNamespace + "si")
                .Select(item => string.Concat(item
                    .Descendants(SpreadsheetNamespace + "t")
                    .Select(text => text.Value)))
                .ToList();
        }

        private static string ReadCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings)
        {
            string type = cell.Attribute("t")?.Value;
            if (type == "inlineStr")
            {
                return string.Concat(cell
                    .Descendants(SpreadsheetNamespace + "t")
                    .Select(text => text.Value));
            }

            string value = cell.Element(SpreadsheetNamespace + "v")?.Value
                           ?? string.Empty;
            if (type == "s"
                && int.TryParse(value, out int sharedIndex)
                && sharedIndex >= 0
                && sharedIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedIndex];
            }

            if (type == "b")
            {
                return value == "1" ? "true" : "false";
            }

            return value;
        }

        private static XDocument CreateWorksheet(
            IReadOnlyList<List<string>> rows,
            int freezeRows)
        {
            int maxColumns = rows.Count == 0 ? 1 : rows.Max(row => row.Count);
            int rowCount = Math.Max(1, rows.Count);
            XElement sheetViews = new XElement(
                SpreadsheetNamespace + "sheetViews",
                new XElement(
                    SpreadsheetNamespace + "sheetView",
                    new XAttribute("workbookViewId", 0)));
            XElement sheetView = sheetViews.Element(
                SpreadsheetNamespace + "sheetView");
            if (freezeRows > 0)
            {
                sheetView.Add(
                    new XElement(
                        SpreadsheetNamespace + "pane",
                        new XAttribute("ySplit", freezeRows),
                        new XAttribute("topLeftCell", "A" + (freezeRows + 1)),
                        new XAttribute("activePane", "bottomLeft"),
                        new XAttribute("state", "frozen")));
            }

            XElement columns = new XElement(SpreadsheetNamespace + "cols");
            for (int columnIndex = 0; columnIndex < maxColumns; columnIndex++)
            {
                int maxLength = 8;
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    string value = GetValue(rows, rowIndex, columnIndex);
                    maxLength = Math.Max(maxLength, value?.Length ?? 0);
                }

                columns.Add(
                    new XElement(
                        SpreadsheetNamespace + "col",
                        new XAttribute("min", columnIndex + 1),
                        new XAttribute("max", columnIndex + 1),
                        new XAttribute("width", Math.Min(40, maxLength + 2)),
                        new XAttribute("customWidth", 1)));
            }

            XElement sheetData = new XElement(SpreadsheetNamespace + "sheetData");
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                XElement rowElement = new XElement(
                    SpreadsheetNamespace + "row",
                    new XAttribute("r", rowIndex + 1));
                List<string> row = rows[rowIndex];
                for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    string value = row[columnIndex];
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    XElement text = new XElement(
                        SpreadsheetNamespace + "t",
                        value);
                    if (value.StartsWith(" ", StringComparison.Ordinal)
                        || value.EndsWith(" ", StringComparison.Ordinal))
                    {
                        text.SetAttributeValue(
                            XNamespace.Xml + "space",
                            "preserve");
                    }

                    rowElement.Add(
                        new XElement(
                            SpreadsheetNamespace + "c",
                            new XAttribute(
                                "r",
                                ToColumnName(columnIndex) + (rowIndex + 1)),
                            new XAttribute("t", "inlineStr"),
                            new XAttribute("s", ResolveStyleIndex(rowIndex)),
                            new XElement(
                                SpreadsheetNamespace + "is",
                                text)));
                }

                sheetData.Add(rowElement);
            }

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(
                    SpreadsheetNamespace + "worksheet",
                    new XAttribute(
                        XNamespace.Xmlns + "r",
                        RelationshipNamespace),
                    new XElement(
                        SpreadsheetNamespace + "dimension",
                        new XAttribute(
                            "ref",
                            $"A1:{ToColumnName(maxColumns - 1)}{rowCount}")),
                    sheetViews,
                    new XElement(
                        SpreadsheetNamespace + "sheetFormatPr",
                        new XAttribute("defaultRowHeight", 18)),
                    columns,
                    sheetData));
        }

        private static XDocument CreateWorkbook(string sheetName)
        {
            string validSheetName = string.IsNullOrWhiteSpace(sheetName)
                ? "Sheet1"
                : sheetName.Replace("/", "_").Replace("\\", "_");
            if (validSheetName.Length > 31)
            {
                validSheetName = validSheetName.Substring(0, 31);
            }

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(
                    SpreadsheetNamespace + "workbook",
                    new XAttribute(
                        XNamespace.Xmlns + "r",
                        RelationshipNamespace),
                    new XElement(
                        SpreadsheetNamespace + "sheets",
                        new XElement(
                            SpreadsheetNamespace + "sheet",
                            new XAttribute("name", validSheetName),
                            new XAttribute("sheetId", 1),
                            new XAttribute(RelationshipNamespace + "id", "rId1")))));
        }

        private static XDocument CreateStyles()
        {
            return XDocument.Parse(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font>"
                + "<font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"11\"/><name val=\"Calibri\"/></font>"
                + "<font><b/><color rgb=\"FF1F2937\"/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
                + "<fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill>"
                + "<fill><patternFill patternType=\"gray125\"/></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF2563EB\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDBEAFE\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFEF3C7\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>"
                + "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>"
                + "<border><left style=\"thin\"><color rgb=\"FFD1D5DB\"/></left><right style=\"thin\"><color rgb=\"FFD1D5DB\"/></right>"
                + "<top style=\"thin\"><color rgb=\"FFD1D5DB\"/></top><bottom style=\"thin\"><color rgb=\"FFD1D5DB\"/></bottom><diagonal/></border></borders>"
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                + "<cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\"/>"
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment vertical=\"center\"/></xf>"
                + "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\"/>"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment wrapText=\"1\"/></xf></cellXfs>"
                + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
                + "</styleSheet>");
        }

        private static XDocument CreateContentTypes()
        {
            return XDocument.Parse(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                + "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>"
                + "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>"
                + "</Types>");
        }

        private static XDocument CreateRootRelationships()
        {
            return XDocument.Parse(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>"
                + "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>"
                + "</Relationships>");
        }

        private static XDocument CreateWorkbookRelationships()
        {
            return XDocument.Parse(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
                + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
                + "</Relationships>");
        }

        private static XDocument CreateAppProperties()
        {
            return XDocument.Parse(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" "
                + "xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">"
                + "<Application>Sheng Game Framework</Application></Properties>");
        }

        private static XDocument CreateCoreProperties()
        {
            string now = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture) + "Z";
            return XDocument.Parse(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" "
                + "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" "
                + "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<dc:creator>Sheng Game Framework</dc:creator>"
                + $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:created>"
                + "</cp:coreProperties>");
        }

        private static void WriteEntry(
            ZipArchive archive,
            string path,
            XDocument document)
        {
            ZipArchiveEntry entry = archive.CreateEntry(
                path,
                CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(
                       stream,
                       Utf8WithoutBom,
                       1024,
                       false))
            {
                document.Save(writer, SaveOptions.DisableFormatting);
            }
        }

        private static int ResolveStyleIndex(int rowIndex)
        {
            if (rowIndex == 0)
            {
                return 1;
            }

            if (rowIndex == 1 || rowIndex == 2)
            {
                return 2;
            }

            return rowIndex == 3 ? 3 : 0;
        }

        private static string GetValue(
            IReadOnlyList<List<string>> rows,
            int rowIndex,
            int columnIndex)
        {
            return rowIndex >= 0
                   && rowIndex < rows.Count
                   && columnIndex >= 0
                   && columnIndex < rows[rowIndex].Count
                ? rows[rowIndex][columnIndex] ?? string.Empty
                : string.Empty;
        }

        private static bool IsDataRowEmpty(IReadOnlyList<string> row)
        {
            for (int i = 1; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureColumnCount(List<string> row, int count)
        {
            while (row.Count < count)
            {
                row.Add(string.Empty);
            }
        }

        private static int ParseIntAttribute(
            XElement element,
            string name,
            int fallback)
        {
            return int.TryParse(element.Attribute(name)?.Value, out int value)
                ? value
                : fallback;
        }

        private static int ParseColumnIndex(string reference)
        {
            int result = 0;
            for (int i = 0; i < reference.Length; i++)
            {
                char character = reference[i];
                if (!char.IsLetter(character))
                {
                    break;
                }

                result = result * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            }

            return Math.Max(0, result - 1);
        }

        private static string ToColumnName(int columnIndex)
        {
            int value = columnIndex + 1;
            StringBuilder builder = new StringBuilder();
            while (value > 0)
            {
                value--;
                builder.Insert(0, (char)('A' + value % 26));
                value /= 26;
            }

            return builder.ToString();
        }
    }
}
