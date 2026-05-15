namespace WALE.ProcessFile.Core.Exceptions;

public class TooManyPagesException(string message, int pageCount) : Exception(message)
{
    public int NumberOfPages { get; set; } = pageCount;
}
