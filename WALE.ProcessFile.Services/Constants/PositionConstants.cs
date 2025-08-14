using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Constants;

public static class PositionConstants
{
    public const int UnknownLinesTotal = -1;
    public const int UnknownLineNumber = -1;
    public const int UnknownPageNumber = -1;
    public const int UnknownCoordinate = -1;    
    
    public const int PositionNotFound = -1;
    public const string CacheMetadataFilename = "cache-metadata.json";
    
    public const char SpaceChar = ' ';
    public const string SpaceString = " ";
    
    public const string StartOfBlockMarker = "[START_OF_BLOCK]";
    public const string EndOfLineMarker = "[END_OF_LINE]";
    public const string EndOfBlockMarker = "[END_OF_BLOCK]";
    public const string ReplacementMarker = "[WILL_BE_REPLACED_LATER]";

    public static DocumentLineWordCoordinates UnknownCoordinates =>
        new(
            UnknownCoordinate,
            UnknownCoordinate,
            UnknownCoordinate,
            UnknownCoordinate);
}