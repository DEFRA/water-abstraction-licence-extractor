namespace WALE.ProcessFile.Core.Exceptions;

public class TooManyPagesException(string message, int pageCount) : Exception(message);