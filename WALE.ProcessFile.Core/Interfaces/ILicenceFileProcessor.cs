namespace WALE.ProcessFile.Core.Interfaces;

/// <summary>
/// Provides methods for processing license files, including extraction from and generation of Excel and CSV files.
/// </summary>
public interface ILicenceFileProcessor
{
    /// <summary>
    /// Extracts data from an Excel file and maps it to the specified generic type.
    /// Supports both single objects and collections (List&lt;T&gt;).
    /// </summary>
    /// <typeparam name="T">The target type to deserialize the Excel data into. Can be a single object or List&lt;T&gt; for multiple records.</typeparam>
    /// <param name="fileName">The name of the Excel file to read from (searches in resources folders)</param>
    /// <param name="headerMapping">Optional dictionary mapping Excel column headers to object property names. Key: Excel header, Value: Property name.</param>
    /// <param name="dontErrorIfMissingFields"></param>
    /// <returns>An instance of type T populated with data from the Excel file</returns>
    /// <exception cref="ArgumentException">Thrown when fileName is null or empty</exception>
    /// <exception cref="FileNotFoundException">Thrown when the Excel file is not found in any resources folder</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Excel file is empty or has no worksheets</exception>
    T ExtractExcel<T>(
        string fileName,
        Dictionary<string, List<string>>? headerMapping = null,
        List<string>? dontErrorIfMissingFields = null);

    /// <summary>
    /// Extracts data from a CSV file and maps it to the specified generic type.
    /// Supports both single objects and collections (List&lt;T&gt;).
    /// </summary>
    /// <typeparam name="T">The target type to deserialize the CSV data into. Can be a single object or List&lt;T&gt; for multiple records.</typeparam>
    /// <param name="fileName">The name of the CSV file to read from (searches in resources folders)</param>
    /// <param name="headerMapping">Optional dictionary mapping CSV column headers to object property names. Key: CSV header, Value: Property name.</param>
    /// <param name="excludeFields"></param>
    /// <returns>An instance of type T populated with data from the CSV file</returns>
    /// <exception cref="ArgumentException">Thrown when fileName is null or empty</exception>
    /// <exception cref="FileNotFoundException">Thrown when the CSV file is not found in any resources folder</exception>
    /// <exception cref="InvalidOperationException">Thrown when the CSV file is empty</exception>
    T ExtractCsv<T>(
        string fileName,
        Dictionary<string, List<string>>? headerMapping = null,
        List<string>? excludeFields = null);

    /// <summary>
    /// Generates an Excel file from a generic entity or collection of entities.
    /// Automatically creates appropriate headers from object properties and handles both single objects and collections.
    /// </summary>
    /// <typeparam name="T">The type of entity to export. Can be a single object or List&lt;T&gt; for multiple records.</typeparam>
    /// <param name="data">The entity or collection of entities to export to Excel</param>
    /// <param name="fileName">The name of the Excel file to create (without extension, .xlsx will be added automatically)</param>
    /// <param name="headerMapping">Optional dictionary mapping object property names to Excel column headers. Key: Property name, Value: Excel header.</param>
    /// <returns>The full path to the created Excel file</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null</exception>
    /// <exception cref="ArgumentException">Thrown when fileName is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to create the Excel file</exception>
    string GenerateExcel<T>(T data, string fileName, Dictionary<string, string> headerMapping);

    /// <summary>
    /// Generates an Excel file with multiple worksheets from a collection of tuples.
    /// Each tuple contains: sheet name, header mapping, and data.
    /// </summary>
    /// <param name="worksheetData">Collection of tuples containing (sheetName, headerMapping, data)</param>
    /// <param name="fileName">The name of the Excel file to create (without extension, .xlsx will be added automatically)</param>
    /// <returns>The full path to the created Excel file</returns>
    /// <exception cref="ArgumentNullException">Thrown when worksheetData is null</exception>
    /// <exception cref="ArgumentException">Thrown when fileName is null or empty, or when no worksheets are provided</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to create the Excel file</exception>
    string GenerateExcel(IEnumerable<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)> worksheetData, string fileName);

    /// <summary>
    /// Finds files in resources folder that start with the given pattern.
    /// </summary>
    /// <param name="pattern">The pattern to search for (e.g., "A_FILE_PATTERN")</param>
    /// <returns>List of file names (without path) that match the pattern</returns>
    /// <exception cref="ArgumentException">Thrown when pattern is null or empty</exception>
    List<string> FindFilesByPattern(string pattern);
}