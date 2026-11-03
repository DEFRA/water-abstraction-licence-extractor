using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelDataReader;
using WALE.ProcessFile.Core.Interfaces;

namespace WRADI.Services.LicenceFinder.Implementations;

/// <inheritdoc/>
public class LicenceFileProcessor : ILicenceFileProcessor
{
    public T ExtractExcel<T>(
        string fileName,
        Dictionary<string, List<string>>? headerMapping = null,
        List<string>? dontErrorIfMissingFields = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        // Register encoding provider for ExcelDataReader
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Find file in resources folder
        var filePath = FindFile(fileName, ".xlsx", ".xls");
        var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        
        using (stream)
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            if (dataSet.Tables.Count == 0)
            {
                throw new InvalidOperationException("No worksheets found in the Excel file.");
            }

            var dataTable = dataSet.Tables[0];
            
            if (dataTable.Rows.Count == 0)
            {
                throw new InvalidOperationException("Excel file is empty.");
            }

            // Determine target type for mapping
            var isCollection = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>);
            var targetType = isCollection ? typeof(T).GetGenericArguments()[0] : typeof(T);

            // Create header-to-column mapping
            var headers = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
            var columnMapping = CreateColumnMapping(headers, headerMapping);

            // Map all rows to objects
            var items = new List<object>();
            
            foreach (DataRow row in dataTable.Rows)
            {
                var item = MapRowToObject(row, targetType, columnMapping, dontErrorIfMissingFields);
                items.Add(item);
            }

            // Return based on type T
            if (isCollection)
            {
                var list = (IList)Activator.CreateInstance(typeof(T))!;
                
                foreach (var item in items)
                    list.Add(item);
                
                return (T)list;
            }

            return items.Count > 0 ? (T)items[0] : Activator.CreateInstance<T>();
        }
    }
    
    public T ExtractCsv<T>(
        string fileName,
        Dictionary<string, List<string>>? headerMapping = null,
        List<string>? excludeFields = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        // Find file in resources folder
        var filePath = FindFile(fileName, ".csv");
        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        // Parse header row
        var headers = lines[0].Split(',').Select(h => h.Trim('"', ' '));
        var columnMapping = CreateColumnMapping(headers, headerMapping);

        // Determine target type for mapping
        var isCollection = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>);
        var targetType = isCollection ? typeof(T).GetGenericArguments()[0] : typeof(T);

        // Map all data rows to objects
        var items = new List<object>();
        
        for (var i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var item = MapRowToObject(values, targetType, columnMapping, excludeFields);
            
            items.Add(item);
        }

        // Return based on type T
        if (isCollection)
        {
            var list = (IList)Activator.CreateInstance(typeof(T))!;
            
            foreach (var item in items)
                list.Add(item);
            
            return (T)list;
        }

        return items.Count > 0 ? (T)items[0] : Activator.CreateInstance<T>();
    }

    public string GenerateExcel<T>(T data, string fileName, Dictionary<string, string> headerMapping)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        // Ensure the file has .xlsx extension
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".xlsx";
        }

        // Determine if data is a collection
        var isCollection = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>);
        var itemType = isCollection ? typeof(T).GetGenericArguments()[0] : typeof(T);

        // Convert data to list format for uniform processing
        var items = new List<object>();

        if (isCollection)
        {
            var enumerable = (IEnumerable)data;

            foreach (var item in enumerable)
            {
                items.Add(item);
            }
        }
        else
        {
            items.Add(data);
        }

        // Create output file path in Resources folder
        var outputPath = CreateOutputFilePath(fileName);

        // Generate Excel file
        CreateExcelFile(items, itemType, outputPath, headerMapping);

        return outputPath;
    }

    public string GenerateExcel(
        IEnumerable<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)> worksheetData,
        string fileName)
    {
        ArgumentNullException.ThrowIfNull(worksheetData);

        var worksheets = worksheetData.ToList();
        
        if (worksheets.Count == 0)
        {
            throw new ArgumentException("At least one worksheet must be provided", nameof(worksheetData));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        // Ensure the file has .xlsx extension
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".xlsx";
        }

        // Create output file path in Resources folder
        var outputPath = CreateOutputFilePath(fileName);

        // Generate Excel file with multiple worksheets
        CreateExcelFileWithWorksheets(worksheets, outputPath);

        return outputPath;
    }

    public List<string> FindFilesByPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
        }

        var matchingFiles = FindResourcesByPattern(pattern);
        
        return matchingFiles
            .Distinct()
            .ToList();
    }

    #region Private Helper Methods

    /// <summary>
    /// Finds resources by pattern from resources folder
    /// </summary>
    /// <param name="pattern">The pattern to search for</param>
    /// <returns>List of matching resource names</returns>
    private static List<string> FindResourcesByPattern(string pattern)
    {
        var matchingResources = new List<string>();
        var resourceNames = Directory.GetFiles("Resources");

        foreach (var resourceName in resourceNames)
        {
            // Extract just the filename from the resource name
            var fileName = Path.GetFileName(resourceName);

            if (string.IsNullOrEmpty(fileName))
            {
                fileName = resourceName.Split('.').LastOrDefault();
            }

            if (!string.IsNullOrEmpty(fileName)
                && fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                matchingResources.Add(fileName);
            }
        }

        return matchingResources;
    }

    /// <summary>
    /// Searches for a file with the specified name and extensions in various resource folders.
    /// </summary>
    /// <param name="fileName">The name of the file to search for</param>
    /// <param name="extensions">Supported file extensions in order of priority</param>
    /// <returns>The full path to the found file</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file is not found in any location</exception>
    private static string FindFile(string fileName, params string[] extensions)
    {
        // Check if fileName already has a valid extension
        var hasValidExtension = extensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

        var possibleFolders = new[]
        {
            "Resources",
            "resources", 
            "Assets",
            "assets",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources"),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources"),
            Path.Combine(Directory.GetCurrentDirectory(), "resources")
        };

        // If fileName already has an extension, just search for it
        if (hasValidExtension)
        {
            // First try physical files
            foreach (var folder in possibleFolders)
            {
                var path = Path.Combine(folder, fileName);

                if (File.Exists(path))
                {
                    return path;
                }
            }
            throw new FileNotFoundException($"File '{fileName}' not found in resources folder.");
        }

        // If no extension, try each extension in priority order
        foreach (var extension in extensions)
        {
            var fileNameWithExt = fileName + extension;

            // First try physical files
            foreach (var folder in possibleFolders)
            {
                var path = Path.Combine(folder, fileNameWithExt);
                if (File.Exists(path))
                    return path;
            }
        }

        // File not found with any extension
        var fileType = extensions.Length == 1 && extensions[0] == ".csv" ? "CSV" : 
            extensions.Any(e => e == ".xlsx" || e == ".xls") ? "Excel" : "File";
        
        throw new FileNotFoundException($"{fileType} file '{fileName}' not found in resources folder.");
    }

    /// <summary>
    /// Creates a mapping dictionary that maps property names to column indexes from header names.
    /// </summary>
    /// <param name="headers">Array or collection of header names</param>
    /// <param name="headerMapping">Optional mapping from file headers to property names</param>
    /// <returns>Dictionary mapping property names to column indexes</returns>
    private static Dictionary<string, int> CreateColumnMapping(
        IEnumerable<string> headers,
        Dictionary<string, List<string>>? headerMapping)
    {
        var columnMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerArray = headers.ToArray();

        // Map column headers to property names
        for (var i = 0; i < headerArray.Length; i++)
        {
            var columnName = headerArray[i];
            
            var mappingEntry = headerMapping?.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, columnName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(mappingEntry?.Key))
            {
                columnMapping[columnName] = i;
                continue;
            }

            if (mappingEntry.Value.Value.Count >= 2)
            {
                throw new Exception($"Unknown which of multiple values to use for {columnName}.");
            }

            var found = false;
            
            foreach (var value in mappingEntry.Value.Value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    columnMapping[value] = i;

                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Use column name as property name if no mapping provided
                columnMapping[columnName] = i;
            }
        }

        return columnMapping;
    }

    /// <summary>
    /// Maps row data to an object of the specified type using column mapping.
    /// Supports both Excel DataRow and CSV string array data sources.
    /// </summary>
    /// <param name="rowData">The row data (DataRow for Excel, string[] for CSV)</param>
    /// <param name="targetType">The target object type to create</param>
    /// <param name="columnMapping">Dictionary mapping property names to column indexes</param>
    /// <param name="dontErrorIfMissingFields"></param>
    /// <returns>An instance of the target type with populated properties</returns>
    private static object MapRowToObject(
        object rowData,
        Type targetType,
        Dictionary<string, int> columnMapping,
        List<string>? dontErrorIfMissingFields)
    {
        var item = Activator.CreateInstance(targetType)!;
        
        var properties = targetType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (destinationPropertyName, _) in properties)
        {
            var existsInColumnMapping = columnMapping.Any(x =>
            {
                var (columnMappingKey, _) = x;
                var columnMappingPropertyName = columnMappingKey
                    .Replace(" ", string.Empty)
                    .Replace("/", string.Empty)
                    .Replace(".", string.Empty);

                return destinationPropertyName.Equals(
                    columnMappingPropertyName,
                    StringComparison.InvariantCultureIgnoreCase);
            });

            if (!existsInColumnMapping)
            {
                if (dontErrorIfMissingFields?.Contains(destinationPropertyName) == true)
                {
                    continue;
                }
                
                throw new Exception($"Destination model {targetType.Name} contains field {destinationPropertyName} that" +
                    $" cannot be found in column mapping");
            }
        }
        
        foreach (var (propertyNameLoop, columnIndex) in columnMapping)
        {
            var propertyName = propertyNameLoop
                .Replace(" ", string.Empty)
                .Replace("/", string.Empty)
                .Replace(".", string.Empty);
            
            if (!properties.TryGetValue(propertyName, out var property))
            {
                if (propertyName.StartsWith("Column") || dontErrorIfMissingFields?.Contains(propertyName) == true)
                {
                    continue;
                }
                
                var modelFields = string.Join(", ", properties.Values.Select(v => v.Name));
                
                throw new Exception($"Excel contains field {propertyName} that" +
                    $" cannot be found in destination model ({modelFields})");
            }
            
            string? cellValue = null;

            // Handle different row data types
            switch (rowData)
            {
                case DataRow excelRow when columnIndex < excelRow.ItemArray.Length && excelRow[columnIndex] != DBNull.Value:
                    cellValue = excelRow[columnIndex].ToString();
                    break;
                case string[] csvRow when columnIndex < csvRow.Length && !string.IsNullOrEmpty(csvRow[columnIndex]):
                    cellValue = csvRow[columnIndex].Trim('"', ' ');
                    break;
            }

            if (!string.IsNullOrEmpty(cellValue))
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var convertedValue = Convert.ChangeType(cellValue, propertyType);
                property.SetValue(item, convertedValue);
            }
        }

        return item;
    }

    /// <summary>
    /// Parses a single CSV line, handling quoted values and commas inside them.
    /// </summary>
    /// <param name="line">The CSV line to parse</param>
    /// <returns>Array of field values from the CSV line</returns>
    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var inQuotes = false;
        var currentValue = new StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(currentValue.ToString().Trim());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(character);
            }
        }

        // Add the last value
        values.Add(currentValue.ToString().Trim());

        return values.ToArray();
    }

    /// <summary>
    /// Creates the output file path for generated Excel files, ensuring the extract directory exists on desktop.
    /// </summary>
    /// <param name="fileName">The name of the file to create</param>
    /// <returns>The full path where the file will be created</returns>
    private static string CreateOutputFilePath(string fileName)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var extractPath = Path.Combine(desktopPath, "DMSExtract");

        // Create extract directory if it doesn't exist
        if (!Directory.Exists(extractPath))
        {
            Directory.CreateDirectory(extractPath);
        }

        return Path.Combine(extractPath, fileName);
    }

    /// <summary>
    /// Creates an Excel file from a collection of objects using DocumentFormat.OpenXml.
    /// </summary>
    /// <param name="items">Collection of objects to write to Excel</param>
    /// <param name="itemType">The type of objects in the collection</param>
    /// <param name="filePath">Full path where the Excel file will be created</param>
    /// <param name="headerMapping">Optional mapping from property names to Excel column headers</param>
    /// <exception cref="InvalidOperationException">Thrown when unable to create the Excel file</exception>
    private static void CreateExcelFile(List<object> items, Type itemType, string filePath, Dictionary<string, string>? headerMapping)
    {
        try
        {
            // Create Excel document
            using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);

            // Add workbook part
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            // Add worksheet part
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            // Add sheet to workbook
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            var sheet = new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Data"
            };
            
            sheets.Append(sheet);

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

            // Get properties and create headers
            var properties = GetWritableProperties(itemType, headerMapping);
            var headers = CreateHeaders(properties, headerMapping);

            // Add header row
            AddHeaderRow(sheetData!, headers);

            // Add data rows
            AddDataRows(sheetData!, items, properties, headerMapping);

            // Save the document
            workbookPart.Workbook.Save();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to create Excel file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates an Excel file with multiple worksheets from a collection of worksheet data tuples.
    /// </summary>
    /// <param name="worksheets">Collection of tuples containing (sheetName, headerMapping, data)</param>
    /// <param name="filePath">Full path where the Excel file will be created</param>
    /// <exception cref="InvalidOperationException">Thrown when unable to create the Excel file</exception>
    private static void CreateExcelFileWithWorksheets(
        List<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)> worksheets,
        string filePath)
    {
        try
        {
            // Create Excel document
            using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);

            // Add workbook part
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            // Add sheets collection
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            uint sheetId = 1;
            
            foreach (var (sheetName, headerMapping, data) in worksheets)
            {
                // Validate sheet name
                var validSheetName = ValidateSheetName(sheetName);

                // Add worksheet part
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData());

                // Add sheet to workbook
                var sheet = new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId++,
                    Name = validSheetName
                };
                
                sheets.Append(sheet);

                // Process data for this worksheet
                var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

                // Determine data type and convert to items list
                var (items, itemType) = ProcessWorksheetData(data);

                if (items.Count <= 0)
                {
                    continue;
                }
                
                // Get properties and create headers
                var properties = GetWritableProperties(itemType, headerMapping);
                var headers = CreateHeaders(properties, headerMapping);

                // Add header row
                AddHeaderRow(sheetData!, headers);

                // Add data rows
                AddDataRows(sheetData!, items, properties, headerMapping);
            }

            // Save the document
            workbookPart.Workbook.Save();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to create Excel file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates and sanitizes a sheet name for Excel compatibility.
    /// </summary>
    /// <param name="sheetName">The proposed sheet name</param>
    /// <returns>A valid Excel sheet name</returns>
    private static string ValidateSheetName(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            return "Sheet1";
        }

        // Excel sheet name restrictions:
        // - Maximum 31 characters
        // - Cannot contain: / \ ? * [ ] :
        var invalidChars = new char[] { '/', '\\', '?', '*', '[', ']', ':' };
        var validName = sheetName;

        foreach (var invalidChar in invalidChars)
        {
            validName = validName.Replace(invalidChar, '_');
        }

        // Trim to max 31 characters
        if (validName.Length > 31)
        {
            validName = validName.Substring(0, 31);
        }

        return validName.Trim();
    }

    /// <summary>
    /// Processes worksheet data and converts it to a list of items with their type.
    /// </summary>
    /// <param name="data">The data object to process</param>
    /// <returns>A tuple containing the items list and the item type</returns>
    private static (List<object> Items, Type ItemType) ProcessWorksheetData(object data)
    {
        if (data == null)
        {
            return ([], typeof(object));
        }

        var dataType = data.GetType();
        var items = new List<object>();
        Type itemType;

        // Check if data is a collection
        if (dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(List<>))
        {
            itemType = dataType.GetGenericArguments()[0];
            var enumerable = (IEnumerable)data;

            foreach (var item in enumerable)
            {
                items.Add(item);
            }
        }
        else if (data is IEnumerable enumerable && dataType != typeof(string))
        {
            // Handle other enumerable types
            var enumerableType = dataType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableType != null)
            {
                itemType = enumerableType.GetGenericArguments()[0];

                foreach (var item in enumerable)
                {
                    items.Add(item);
                }
            }
            else
            {
                itemType = typeof(object);

                foreach (var item in enumerable)
                {
                    items.Add(item);
                }
            }
        }
        else
        {
            // Single object
            itemType = dataType;
            items.Add(data);
        }

        return (items, itemType);
    }

    /// <summary>
    /// Gets writable properties from a type, filtered and ordered for Excel export.
    /// </summary>
    /// <param name="type">The type to get properties from</param>
    /// <param name="headerMapping">Optional header mapping to determine property order</param>
    /// <returns>Array of writable PropertyInfo objects</returns>
    private static PropertyInfo[] GetWritableProperties(Type type, Dictionary<string, string>? headerMapping)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && (p.PropertyType.IsValueType || p.PropertyType == typeof(string)))
            .ToArray();

        // If header mapping is provided, use its order
        if (headerMapping != null && headerMapping.Count > 0)
        {
            var orderedProperties = new List<PropertyInfo>();
            var propertyLookup = properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            // Add properties in the order specified by header mapping
            foreach (var mappingKey in headerMapping.Keys)
            {
                if (propertyLookup.TryGetValue(mappingKey, out var property))
                {
                    orderedProperties.Add(property);
                }
            }

            // Add any remaining properties that weren't in the mapping
            var remainingProperties = properties.Except(orderedProperties).OrderBy(p => p.Name);
            orderedProperties.AddRange(remainingProperties);

            return orderedProperties.ToArray();
        }

        // Default alphabetical ordering if no header mapping
        return properties.OrderBy(p => p.Name).ToArray();
    }

    /// <summary>
    /// Creates Excel headers from property names, applying header mapping if provided.
    /// </summary>
    /// <param name="properties">Array of properties to create headers for</param>
    /// <param name="headerMapping">Optional mapping from property names to Excel headers</param>
    /// <returns>Array of header strings for Excel columns</returns>
    private static string[] CreateHeaders(PropertyInfo[] properties, Dictionary<string, string>? headerMapping)
    {
        var headers = new string[properties.Length];

        for (var i = 0; i < properties.Length; i++)
        {
            var propertyName = properties[i].Name;

            // Use header mapping if available, otherwise use property name
            if (headerMapping != null && headerMapping.TryGetValue(propertyName, out var mappedHeader))
            {
                headers[i] = mappedHeader;
            }
            else
            {
                headers[i] = propertyName;
            }
        }

        return headers;
    }

    /// <summary>
    /// Adds the header row to the Excel worksheet.
    /// </summary>
    /// <param name="sheetData">The worksheet's SheetData element</param>
    /// <param name="headers">Array of header strings</param>
    private static void AddHeaderRow(SheetData sheetData, string[] headers)
    {
        var headerRow = new Row() { RowIndex = 1 };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = new Cell()
            {
                CellReference = GetColumnName(i) + "1",
                DataType = CellValues.InlineString,
                InlineString = new InlineString() { Text = new Text(headers[i]) }
            };
            
            headerRow.AppendChild(cell);
        }

        sheetData.AppendChild(headerRow);
    }

    /// <summary>
    /// Adds data rows to the Excel worksheet from the collection of objects.
    /// </summary>
    /// <param name="sheetData">The worksheet's SheetData element</param>
    /// <param name="items">Collection of objects to write as rows</param>
    /// <param name="properties">Array of properties to read from each object</param>
    /// <param name="headerMapping">Optional header mapping to identify special columns</param>
    private static void AddDataRows(SheetData sheetData, List<object> items, PropertyInfo[] properties, Dictionary<string, string>? headerMapping = null)
    {
        for (var rowIndex = 0; rowIndex < items.Count; rowIndex++)
        {
            var dataRow = new Row() { RowIndex = (uint)(rowIndex + 2) }; // +2 because Excel is 1-based and we have a header row
            var item = items[rowIndex];

            for (var colIndex = 0; colIndex < properties.Length; colIndex++)
            {
                var property = properties[colIndex];
                var value = property.GetValue(item);
                var cellValue = value?.ToString() ?? string.Empty;

                // Get the header name for this column
                var headerName = headerMapping?.GetValueOrDefault(property.Name) ?? property.Name;

                var cell = new Cell()
                {
                    CellReference = GetColumnName(colIndex) + (rowIndex + 2)
                };

                // Special handling for PermitNumber column - format as number if it's numeric
                if ((headerName.Equals("NaldId", StringComparison.OrdinalIgnoreCase)
                    || headerName.Equals("NaldIssueNo", StringComparison.OrdinalIgnoreCase)) && 
                    long.TryParse(cellValue, out long numericValue))
                {
                    cell.DataType = CellValues.Number;
                    cell.CellValue = new CellValue(numericValue.ToString());
                }
                // Special handling for URL columns - create hyperlinks
                else if ((headerName.Contains("URL", StringComparison.OrdinalIgnoreCase)
                         || headerName.Contains("FullPath", StringComparison.OrdinalIgnoreCase))
                         && 
                         !string.IsNullOrWhiteSpace(cellValue) )
                {
                    cell.DataType = CellValues.InlineString;
                    cell.InlineString = new InlineString() { Text = new Text(cellValue) };

                    // Validate URL before creating hyperlink
                    if (Uri.TryCreate(cellValue, UriKind.Absolute, out Uri? validUri) && 
                        (validUri.Scheme == Uri.UriSchemeHttp || validUri.Scheme == Uri.UriSchemeHttps))
                    {
                        // Add external hyperlink relationship
                        var worksheet = sheetData.Ancestors<Worksheet>().FirstOrDefault();
                        var worksheetPart = worksheet?.WorksheetPart;
                        if (worksheetPart != null)
                        {
                            // Create external relationship for the URL
                            var hyperlinkRelationship = worksheetPart.AddHyperlinkRelationship(validUri, true);

                            // Get or create hyperlinks element
                            var hyperlinks = worksheet!.Elements<Hyperlinks>().FirstOrDefault();
                            if (hyperlinks == null)
                            {
                                hyperlinks = new Hyperlinks();
                                worksheet.AppendChild(hyperlinks);
                            }

                            // Create hyperlink with relationship ID
                            var hyperlink = new Hyperlink()
                            {
                                Reference = cell.CellReference,
                                Id = hyperlinkRelationship.Id
                            };
                            hyperlinks.AppendChild(hyperlink);
                        }
                    }
                }
                else
                {
                    cell.DataType = CellValues.InlineString;
                    cell.InlineString = new InlineString() { Text = new Text(cellValue) };
                }

                dataRow.AppendChild(cell);
            }

            sheetData.AppendChild(dataRow);
        }
    }

    /// <summary>
    /// Converts a zero-based column index to Excel column name (A, B, C, ..., AA, AB, etc.).
    /// </summary>
    /// <param name="columnIndex">Zero-based column index</param>
    /// <returns>Excel column name string</returns>
    private static string GetColumnName(int columnIndex)
    {
        var columnName = string.Empty;
        
        while (columnIndex >= 0)
        {
            columnName = (char)('A' + (columnIndex % 26)) + columnName;
            columnIndex = (columnIndex / 26) - 1;
        }
        
        return columnName;
    }

    /// <summary>
    /// Creates a change log template Excel file in the Resources folder
    /// </summary>
    public void CreateChangeLogTemplate()
    {
        var headers = new Dictionary<string, string>
        {
            { "PermitNumber", "Permit Number" },
            { "OriginalPath", "Original Path" },
            { "UpdatedPath", "Updated Path" },
            { "Action", "Action" }
        };

        // Create empty data structure for template
        var emptyData = new List<object>();

        // Create file path in Resources folder
        var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        if (!Directory.Exists(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
        }

        var filePath = Path.Combine(resourcesPath, "Change_Log_Template.xlsx");
    }

    #endregion
}