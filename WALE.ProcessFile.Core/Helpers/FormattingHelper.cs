using System.Text;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static class FormattingHelper
{
    public static string? RemoveSeperators(string? licenceNumber)
    {
        return licenceNumber?
            .Replace(".", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)            
            .Replace("/", string.Empty);
    }

    public static List<string> StripForComparisonMultipleOptions(string? formattedLicenceNumber, int regionCode)
    {
        var stripped = StripForComparison(formattedLicenceNumber, regionCode);

        if (string.IsNullOrEmpty(stripped))
        {
            return [];
        }

        if (!stripped.Contains('_'))
        {
            return [stripped];
        }

        var licenceNumberNoSeperators = RemoveSeperators(formattedLicenceNumber)!;

        return
        [
            stripped,
            licenceNumberNoSeperators
        ];
    }

    public static bool GetDmsFileData(
        string? licenceNumber,
        int regionCode,
        Dictionary<string, DmsFileData>? licenceNumberMapping,
        out DmsFileData? dmsFileData)
    {
        var strippedOptions = StripForComparisonMultipleOptions(licenceNumber, regionCode);
        dmsFileData = null;
            
        foreach (var stripped in strippedOptions)
        {
            if (licenceNumberMapping?.TryGetValue(stripped, out dmsFileData) == true)
            {
                return true;
            }
        }

        return false;
    }
    
    public static string? StripForComparison(string? formattedLicenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(formattedLicenceNumber))
        {
            return null;
        }

        if (IsNeLicenceNumber(formattedLicenceNumber, regionCode))
        {
            return StripForComparison_NE(formattedLicenceNumber);
        }

        var licenceNumber = formattedLicenceNumber
            .Replace("//", "/")
            .Replace(".", "/")
            .Replace(" ", "/")
            .Replace("-", "/");

        var parts = licenceNumber.Split('/');

        var first = true;
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            var partChanged = part;
            
            while (partChanged.StartsWith('0'))
            {
                partChanged = partChanged[1..];
            }

            if (!first)
            {
                sb.Append('_');
            }
            
            sb.Append(partChanged);
            first = false;            
        }

        var str = sb.ToString();

        // Commented out 12-01-2026 as it makes these the same 4/29/10/*G/0010 and 4/29/10/*G/0100
        
        /*if (str.EndsWith('0'))
        {
            var partWithoutTrailingZero = str[..^1];
            return partWithoutTrailingZero.Replace("0", string.Empty) + "0";
        }*/

        // Commented out 12-01-2026 as it makes these the same 6/33/02/*G/0103 and 6/33/02/*G/0013
        return str;// str.Replace("0", string.Empty);
    }

    private static string? StripForComparison_NE(string? formattedLicenceNumber)
    {
        var licenceNumber = ToFullLicenceNumber_NE(formattedLicenceNumber);
        return licenceNumber?.Replace("/", "_");
    }

    private static string? ToFullLicenceNumber_NE(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }

        var origLicenceNumber = licenceNumber;
        licenceNumber = licenceNumber
            .Replace("//", "/")
            .Replace(".", "/")
            .Replace(" ", "/")
            .Replace("-", "/");

        var origSectionLengths = licenceNumber.Split('/');
        var origSectionInts = origSectionLengths
            .Select(s => int.TryParse(s, out var i) ? i : (int?)null)
            .ToArray();

        licenceNumber = RemoveSeperators(licenceNumber)!;
        
        var parts = new List<string>();
        var remainingLicenceNumber = licenceNumber;
        
        // [1/2]/12/01/012
        if (remainingLicenceNumber[0] == '1' || remainingLicenceNumber[0] == '2')
        {
            // Examples
            // 2/27/29/31 (goes into NALD as 22729031 - 0 is padding to part 4)
            // 2/27/29/059 (22729059)
            // 2/27/28/285 (22728285)
            // 1/22/02/087 (12202087)
            // 1/22/2/43 (12202043 - 0 is padded in part 3 and part 4)
            // 1/24/4/016 (12404016 - 0 is padded in part 3)
            // 1/22/03/131/1 ( - has the 1 at the end)
            
            // Part 1 - 1
            var part1 = remainingLicenceNumber[..1];
            remainingLicenceNumber = remainingLicenceNumber[1..];
            
            // Part 2 - 12
            var part2 = remainingLicenceNumber[..2];
            remainingLicenceNumber = remainingLicenceNumber[2..];

            parts.Add(part1);
            parts.Add(part2);

            var splitAtChar5 = remainingLicenceNumber.Length >= 6 ? new[]
            {
                remainingLicenceNumber[..5],
                remainingLicenceNumber[5..]
            } : [remainingLicenceNumber];
            
            var first5 = splitAtChar5[0];
            var first5Digits = string.Join(string.Empty, first5.Where(char.IsDigit).ToArray());

            if (first5 != first5Digits)
            {
                if (splitAtChar5.Length == 1)
                {
                    splitAtChar5 =
                    [
                        first5Digits,
                        first5[first5Digits.Length..]
                    ];
                }
                else
                {
                    splitAtChar5 =
                    [
                        first5Digits,
                        first5[first5Digits.Length..] + splitAtChar5[1]
                    ];
                }
            }
            
            // Part 3 - Is 1 or 2 long, NALD wants it as 2
            // Part 4 - 12 (if part 3 has length 1) or 123 (if part 3 has length 2, new) - NALD has as 123
            
            if (first5Digits.Length == 5)
            {
                // Part 3 - 12
                var part3 = first5Digits[..2];
                first5Digits = first5Digits[2..];
                
                // Part 4 - 123
                var part4 = first5Digits[..3];
                
                parts.Add(part3);
                parts.Add(part4);
            }
            else if (first5Digits.Length == 4)
            {
                var firstChar = first5Digits[0];
                var secondChar = first5Digits[1];
                
                string? part3;
                string? part4;                
                
                // Definately needs padding as only valid range is 1-34 for this region
                if (firstChar is '4' or '5' or '6' or '7' or '8' or '9')
                {
                    // Part 3 - 1
                    part3 = "0" + first5Digits[..1];
                    first5Digits = first5Digits[1..];
                    
                    // Part 4 - 123
                    part4 = first5Digits[..3];
                }
                else if (secondChar == '0')
                {
                    // Second section is padded with 0, means the first section must not be
                    
                    // Part 3 - 1
                    part3 = "0" + first5Digits[..1];
                    first5Digits = first5Digits[1..];
                    
                    // Part 4 - 123
                    part4 = first5Digits[..3];
                }
                // 1/21/00, 1/22/01-06, 1/23/01-05, 1/24/01-05, 1/25/01-06
                else if (part1 == "1"
                    && part2 is "21" or "22" or "23" or "24" or "25")
                {
                    if (firstChar == '0')
                    {
                        // Part 3 - 1
                        part3 = first5Digits[..2];
                        first5Digits = first5Digits[2..];

                        // Part 4 - 12
                        part4 = "0" + first5Digits[..2];
                    }
                    else
                    {
                        // Part 3 - 1
                        part3 = "0" + first5Digits[..1];
                        first5Digits = first5Digits[1..];

                        // Part 4 - 123
                        part4 = first5Digits[..3];
                    }
                }
                // 2/27/19-29
                else if (part1 == "2" && part2 == "27")
                {
                    var first2Digits = int.Parse(first5Digits[..2]);

                    if (first2Digits is >= 19 and <= 29)
                    {
                        // Part 3 - 12
                        part3 = first5Digits[..2];
                        first5Digits = first5Digits[2..];

                        // Part 4 - 12
                        part4 = "0" + first5Digits[..2];
                    }
                    else
                    {
                        if (origSectionLengths is [_, _, { Length: 2 }, _, ..])
                        {
                            // Part 3 - 12
                            part3 = first5Digits[..2];
                            first5Digits = first5Digits[2..];

                            // Part 4 - 12
                            part4 = "0" + first5Digits[..2];
                        }
                        else
                        {
                            // Part 3 - 1
                            part3 = "0" + first5Digits[..1];
                            first5Digits = first5Digits[1..];

                            // Part 4 - 123
                            part4 = first5Digits[..3];
                        }
                    }
                }
                // 2/26/30-34
                else if (part1 == "2" && part2 == "26")
                {
                    var first2Digits = int.Parse(first5Digits[..2]);

                    if (first2Digits is >= 30 and <= 34)
                    {
                        // Part 3 - 12
                        part3 = first5Digits[..2];
                        first5Digits = first5Digits[2..];

                        // Part 4 - 12
                        part4 = "0" + first5Digits[..2];
                    }
                    else
                    {
                        // Part 3 - 1
                        part3 = "0" + first5Digits[..1];
                        first5Digits = first5Digits[1..];

                        // Part 4 - 123
                        part4 = first5Digits[..3];
                    }
                }
                // 2/27/1-18
                else if (part1 == "2" && part2 == "27")
                {
                    if (firstChar == '0')
                    {
                        // Part 3 - 12
                        part3 = first5Digits[..2];
                        first5Digits = first5Digits[2..];

                        // Part 4 - 12
                        part4 = "0" + first5Digits[..2];
                    }
                    else if (firstChar != '1')
                    {
                        // Part 3 - 1
                        part3 = "0" + first5Digits[..1];
                        first5Digits = first5Digits[1..];

                        // Part 4 - 123
                        part4 = first5Digits[..3];
                    }
                    else if (firstChar == '1')
                    {
                        if (origSectionLengths.Length >= 4)
                        {
                            if (origSectionLengths[2].Length == 2)
                            {
                                // Part 3 - 12
                                part3 = first5Digits[..2];
                                first5Digits = first5Digits[2..];

                                // Part 4 - 12
                                part4 = "0" + first5Digits[..2];
                            }
                            else
                            {
                                // Part 3 - 1
                                part3 = '0' + first5Digits[..1];
                                first5Digits = first5Digits[1..];

                                // Part 4 - 123
                                part4 = first5Digits[..3];
                            }
                        }
                        else
                        {
                            // NOTE - This is a guess at this point, as there is no other way of doing it
                        
                            // Part 3 - 12
                            part3 = first5Digits[..2];
                            first5Digits = first5Digits[2..];

                            // Part 4 - 12
                            part4 = "0" + first5Digits[..2];
                        }
                    }
                    else
                    {
                        throw new Exception("Can't work it out (1)");
                    }
                }
                else
                {
                    return Yorkshire1_ToNaldLicenceNumber(licenceNumber);
                }
                
                parts.Add(part3);
                parts.Add(part4);
            }
            else if (first5Digits.Length == 3)
            {
                // Part 3 - 1
                var part3 = "0" + first5Digits[..1];
                first5Digits = first5Digits[1..];
                
                // Part 4 - 12
                var part4 = "0" + first5Digits[..2];
                
                parts.Add(part3);
                parts.Add(part4);
            }
            
            // Part 5 (optional) - R01, RO2 etc...
            
            var postRSection = splitAtChar5.Length > 1 ? splitAtChar5[1] : null;
        
            if (!string.IsNullOrEmpty(postRSection))
            {
                if (postRSection.Length == 1 && char.IsLetter(postRSection[0]))
                {
                    parts[^1] += postRSection;
                }
                else
                {
                    parts.Add(postRSection);   
                }
            }
        }
        else if (remainingLicenceNumber[0] is 'n' or 'N')
        {
            // Part 1 - NE
            parts.Add(remainingLicenceNumber[..2]);
            remainingLicenceNumber = remainingLicenceNumber[2..];
            
            // Part 2 - 000
            parts.Add(remainingLicenceNumber[..3]);
            remainingLicenceNumber = remainingLicenceNumber[3..];

            if (remainingLicenceNumber.Length >= 7)
            {
                // Part 3 - 0000
                var numberOfCharsInSection = 4;
                var thisPart = remainingLicenceNumber[..numberOfCharsInSection];

                if (int.TryParse(thisPart, out var thisPartInt))
                {
                    var partNumber = 2; // 3 but zero based
                    if (origSectionInts.Length > 2)
                    {
                        if (thisPartInt > origSectionInts[partNumber])
                        {
                            numberOfCharsInSection = 3;
                            thisPart = thisPart[..numberOfCharsInSection];
                        }
                    }
                }

                parts.Add(thisPart);
                remainingLicenceNumber = remainingLicenceNumber[numberOfCharsInSection..];

                // Part 4 - 000
                parts.Add(remainingLicenceNumber[..3]);
                remainingLicenceNumber = remainingLicenceNumber[3..];
            }
            else
            {
                if (remainingLicenceNumber.Length >= 3)
                {
                    // Part 3 - 000
                    parts.Add(remainingLicenceNumber[..3]);
                    remainingLicenceNumber = remainingLicenceNumber[3..];
                }

                if (remainingLicenceNumber.Length >= 3)
                {
                    // Part 4 - 000
                    parts.Add(remainingLicenceNumber[..3]);
                    remainingLicenceNumber = remainingLicenceNumber[3..];
                }
            }

            // Part 5 - Likely R01, but can be 1 and other stuff
            if (!string.IsNullOrEmpty(remainingLicenceNumber))
            {
                var lastPart = parts[^1];
                var endsWithR = lastPart[^1] == 'R';

                if (endsWithR)
                {
                    parts[^1] += remainingLicenceNumber;
                }
                else
                {
                    parts.Add(remainingLicenceNumber);   
                }
            }
        }
        else
        {
            return Yorkshire1_ToNaldLicenceNumber(licenceNumber);
        }

        if (origLicenceNumber.Contains('/'))
        {
            var origParts = origLicenceNumber.Split('/');
            var partsCount = 0;
            
            foreach (var origPart in origParts)
            {
                if (parts.Count - 1 < partsCount
                    || !int.TryParse(parts[partsCount++], out var partInt)
                    || !int.TryParse(origPart, out var origPartInt))
                {
                    continue;
                }
                
                if (partInt > origPartInt)
                {
                    // We messed it up somewhere - so use the original
                    return origLicenceNumber;
                }
            }
        }
        
        var outputString = string.Join('/', parts);
        if (outputString.Contains("R0") && !outputString.Contains("/R0"))
        {
            outputString = outputString.Replace("R0", "/R0");
        }
        
        return outputString;
    }

    private static bool IsMdLicenceNumber(string? licenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return false;
        }
        
        return licenceNumber.StartsWith("MD")
            || licenceNumber.StartsWith("18/")
            || licenceNumber.StartsWith("03/")
            || licenceNumber.StartsWith("3/");            
    }

    private static bool IsNeLicenceNumber(string? licenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return false;
        }

        if (!licenceNumber.Contains('/') && (licenceNumber.Contains(' ') || licenceNumber.Contains('.')))
        {
            return false;
        }
        
        if (licenceNumber[0] is 'm' or 'M' || licenceNumber[1] is 'd' or 'D')
        {
            return false;
        }
        
        if (licenceNumber[0] is '3' or '4' or '5' or '6' or '7' or '8' or '9')
        {
            return false;
        }
        
        if (licenceNumber[0] is 'n' or 'N' && licenceNumber[1] is 'e' or 'E')
        {
            return true;
        }
        
        licenceNumber = licenceNumber
            .Replace(".", "/")
            .Replace(" ", "/")
            .Replace("-", "/");

        var parts = licenceNumber.Split('/');

        if (parts[0] is "1")
        {
            return parts[1] is "21" or "22" or "23" or "24" or "25";
        }
        
        if (parts[0] is "2")
        {
            return parts[1] is "26" or "27";
        }

        return regionCode == 3;
    }

    public static bool? IsValidLicenceNumber(string licenceNumber, int regionCode)
    {
        if (regionCode != 3)
        {
            return null;
        }

        var siblingRegions = SiblingRegions(regionCode);
        var allRelevantRegions = siblingRegions.ToList();
        allRelevantRegions.Add(regionCode);

        foreach (var region in allRelevantRegions)
        {
            if (region == 2 && IsMdLicenceNumber(licenceNumber, regionCode))
            {
                return true;
            }
            
            if (region == 3 && IsNeLicenceNumber(licenceNumber, regionCode))
            {
                return true;
            }
        }

        return false;
    }

    private static List<int> SiblingRegions(int regionCode)
    {
        if (regionCode == 2)
        {
            // North East region
            return [3];
        }
        
        if (regionCode == 3)
        {
            // Midlands region
            return [2];
        }

        return [];
    }
    
    public static string? NoneSeperatedToNaldLicenceNumber(string? noneSeperatedLicenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(noneSeperatedLicenceNumber))
        {
            return noneSeperatedLicenceNumber;
        }

        if (IsNeLicenceNumber(noneSeperatedLicenceNumber, regionCode))
        {
            return ToFullLicenceNumber_NE(noneSeperatedLicenceNumber);
            //return Yorkshire1_ToNaldLicenceNumber(noneSeperatedLicenceNumber);
        }
        
        // TODO some other way
        return Yorkshire1_ToNaldLicenceNumber(noneSeperatedLicenceNumber);
    }

    public static string? FormatLicenceNumber(string? licenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }
        
        if (IsNeLicenceNumber(licenceNumber, regionCode))
        {
            return ToFullLicenceNumber_NE(licenceNumber);
        }

        licenceNumber = licenceNumber.Replace("//", "/");

        if (licenceNumber.StartsWith("NE"))
        {
            // TODO something
        }
        
        if (licenceNumber.Contains("*"))
        {
            return licenceNumber;
        }
        
        if (licenceNumber.Contains("I") || licenceNumber.Contains("S"))
        {
            return licenceNumber;
        }
        
        if (licenceNumber.StartsWith('J'))
        {
            licenceNumber = '1' + licenceNumber[1..];
        }
        if (licenceNumber.StartsWith('4'))
        {
            licenceNumber = '1' + licenceNumber[1..];
        }
        if (licenceNumber.StartsWith('7'))
        {
            licenceNumber = '1' + licenceNumber[1..];
        }
        
        var numberOfSlashes = licenceNumber.Count(c => c == '/');
        
        if (numberOfSlashes is 1 or 2)
        {
            return licenceNumber;
        }

        if (numberOfSlashes == 3 && licenceNumber.Split('/')[0].Length == 2)
        {
            return licenceNumber;
        }
        
        return NOTYorkshire1_PadLicenceNumber(licenceNumber, regionCode);
    }

    private static string? Yorkshire1_ToNaldLicenceNumber(string? noneSeperatedLicenceNumber)
    {
        if (string.IsNullOrEmpty(noneSeperatedLicenceNumber))
        {
            return noneSeperatedLicenceNumber;
        }
        
        var section1 = noneSeperatedLicenceNumber[0].ToString();

        if (section1 == "J" || section1 == "4" || section1 == "7")
        {
            section1 = "1";
        }

        var section2StartPoint = 1;
        var section2Length = 2;
        
        var section3StartPoint = 3;
        var section3Length = 2;
        
        var section4StartPoint = 5;
        
        if (noneSeperatedLicenceNumber.StartsWith("NE"))
        {
            section1 = "NE";
            
            section2StartPoint += 1;
            section2Length = 3;
            
            section3StartPoint += 2;
            section3Length = 4;
            
            section4StartPoint += 4;
        }
        else if (noneSeperatedLicenceNumber.StartsWith("0"))
        {
            section1 = noneSeperatedLicenceNumber[1].ToString();
            
            section2StartPoint += 1;
            section3StartPoint += 1;
            section4StartPoint += 1;
        }

        if (noneSeperatedLicenceNumber.Length < 3)
        {
            return noneSeperatedLicenceNumber;
        }
        
        var section2 = noneSeperatedLicenceNumber.Substring(section2StartPoint, section2Length);

        if (noneSeperatedLicenceNumber.Length < 5)
        {
            return $"{section1}/{section2}";
        }
        
        var section3EndPoint = section3StartPoint + section3Length;
        if (section3EndPoint >= noneSeperatedLicenceNumber.Length)
        {
            section3Length = noneSeperatedLicenceNumber.Length - section3StartPoint;
        }
        
        var section3 = noneSeperatedLicenceNumber.Substring(section3StartPoint, section3Length);
        var section4 = section4StartPoint < noneSeperatedLicenceNumber.Length ? noneSeperatedLicenceNumber[section4StartPoint..] : string.Empty;
        
        // Pad part 4 with zeroes (needs to have 3 digits)
        section4 = section4.Where(char.IsDigit).Count() switch
        {
            1 => $"00{section4}",
            2 => $"0{section4}",
            _ => section4
        };

        if (section4.Length > 3)
        {
            if (section4.StartsWith("S"))
            {
                var rest = section4[1..];
                if (rest is ['0', _, _, _])
                {
                    rest = rest[1..];
                }
                
                section4 = $"S/{rest}";
            }
            else
            {
                section4 = section4[..3] + "/" + section4[3..];   
            }
        }

        if (section4.EndsWith("/A") || section4.EndsWith("/B") || section4.EndsWith("/C"))
        {
            section4 = section4
                .Replace("/A", "A")
                .Replace("/B", "B")
                .Replace("/C", "C");                
        }
        
        if (section4.Contains("R01") && !section4.Contains("/R01"))
        {
            var section4Parts = section4.Split('/');
            var prePart = section4Parts[0];
            var ro1Part = section4Parts[1];
            
            var r01Position = ro1Part.IndexOf("R01", StringComparison.Ordinal);
            var preText = ro1Part[..r01Position];

            prePart += preText;
            ro1Part = ro1Part[r01Position..];

            section4 = $"{prePart}/{ro1Part}";
        }
        
        return $"{section1}/{section2}/{section3}/{section4}";
    }

    private static string? NOTYorkshire1_PadLicenceNumber(string? licenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }

        var startsWithDigit = char.IsDigit(licenceNumber[0]);
        var usesSlashes = true;
        
        // Replace dots with slashes IF its all dots
        if (licenceNumber.Contains('.') && !licenceNumber.Contains('/'))
        {
            licenceNumber = licenceNumber.Replace(".", "/");
            usesSlashes = false;
        }
        
        // Replace spaches with slashes IF its all spaces
        if (licenceNumber.Contains(' ') && !licenceNumber.Contains('/'))
        {
            licenceNumber = licenceNumber.Replace(" ", "/");
            usesSlashes = false;            
        }
        
        var parts = licenceNumber.Split('/');
        
        var part1 = parts[0];

        if (parts.Length < 2)
        {
            return startsWithDigit && usesSlashes
                ? NoneSeperatedToNaldLicenceNumber(part1.Replace("/", string.Empty), regionCode)
                : part1;
        }
        
        var part2 = parts[1];
        
        if (part2.Length == 1)
        {
            part2 = $"0{part2}";
        }
        
        if (parts.Length < 3)
        {
            return startsWithDigit && usesSlashes
                ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}".Replace("/", string.Empty), regionCode)
                : $"{part1}/{part2}";
        }
        
        var part3 = parts[2];

        if (part3.Length == 1)
        {
            part3 = $"0{part3}";
        }

        if (parts.Length < 4)
        {
            return startsWithDigit && usesSlashes
                ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}/{part3}".Replace("/", string.Empty), regionCode)
                : $"{part1}/{part2}/{part3}";
        }
        
        var part4 = parts[3];

        // Pad part 4 with zeroes (needs to have 3 digits)
        part4 = part4.Where(char.IsDigit).Count() switch
        {
            1 => $"00{part4}",
            2 => $"0{part4}",
            _ => part4
        };
        
        if (parts.Length < 5)
        {
            return startsWithDigit && usesSlashes
                ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}/{part3}/{part4}".Replace("/", string.Empty), regionCode)
                : $"{part1}/{part2}/{part3}/{part4}";
        }

        var part5 = parts[4];
        
        return startsWithDigit && usesSlashes
            ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}/{part3}/{part4}/{part5}".Replace("/", string.Empty), regionCode)
            : $"{part1}/{part2}/{part3}/{part4}/{part5}";
    }
    
    public static List<DocumentLine> RemoveMultipleBlankLines(IEnumerable<DocumentLine> sourceList)
    {
        var trimmedList = TrimList(sourceList);
        
        var returnList = new List<DocumentLine>();
        var previousLineWasBlank = false;
        
        foreach (var line in trimmedList.Where(line => !previousLineWasBlank || !string.IsNullOrEmpty(line.Text)))
        {
            previousLineWasBlank = string.IsNullOrEmpty(line.Text);
            returnList.Add(line);
        }

        return returnList;
    }

    public static List<DocumentLineWord> TrimFormatting(List<DocumentLineWord> words)
    {
        var newWords = new List<DocumentLineWord>();

        foreach (var word in words)
        {
            var isFirstWord = words[0] == word;
            var isLastWord = words.Last() == word;
            
            var returnWord = word.Clone();
            returnWord.Text = TrimFormatting(
                word.Text,
                isFirstWord,
                isLastWord)!;
            
            newWords.Add(returnWord);
        }
        
        return newWords;
    }
    
    public static string? TrimFormatting(
        string? text,
        bool trimPunctuationStart,
        bool trimPunctuationEnd)
    {
        var trimmed = text?.Trim();

        if (trimPunctuationStart)
        {
            while (trimmed?.Length >= 1
               && trimmed[0] != '('
               && trimmed[0] != '&'               
               && (char.IsPunctuation(trimmed[0])
                   || char.IsSymbol(trimmed[0])
                   || char.IsWhiteSpace(trimmed[0])))
            {
                trimmed = trimmed[1..];
            }
        }

        if (trimPunctuationEnd)
        {
            while (trimmed?.Length >= 1
               && trimmed[^1] != ')'
               && trimmed[^1] != ':'
               && trimmed[^1] != '&'               
               && trimmed[^1] != '/'
               && (char.IsPunctuation(trimmed[^1])
                   || char.IsSymbol(trimmed[^1])
                   || char.IsWhiteSpace(trimmed[^1])))
            {
                trimmed = trimmed[..^1];
            }
        }

        const string space = " ";
        const string doubleSpace = "  ";
        
        while (trimmed?.Contains(doubleSpace) == true)
        {
            trimmed = trimmed.Replace(doubleSpace, space);
        }
        
        return trimmed;
    }
    
    public static void Standardise(List<DocumentLineColumn> columns)
    {
        const string singleQuoteChar = "'";        
        const string doubleQuoteChar = "\"";
        const string asteriskString = "*";

        foreach (var column in columns)
        {
            foreach (var word in column.Words)
            {
                word.Text = word.Text.Trim();

                if (word.Text.Contains("‘‘"))
                {
                    word.Text = word.Text.Replace("‘‘", doubleQuoteChar);
                }

                if (word.Text.Contains("’’"))
                {
                    word.Text = word.Text.Replace("’’", doubleQuoteChar);
                }

                if (word.Text.Contains('‘'))
                {
                    word.Text = word.Text.Replace("‘", singleQuoteChar);
                }

                if (word.Text.Contains('’'))
                {
                    word.Text = word.Text.Replace("’", singleQuoteChar);
                }

                if (word.Text.Contains('“'))
                {
                    word.Text = word.Text.Replace("“", doubleQuoteChar);
                }

                if (word.Text.Contains('”'))
                {
                    word.Text = word.Text.Replace("”", doubleQuoteChar);
                }

                if (word.Text.Contains("'\""))
                {
                    word.Text = word.Text.Replace("'\"", doubleQuoteChar);
                }

                if (word.Text.Contains('°'))
                {
                    word.Text =
                        word.Text.Replace("\u00b0",
                            asteriskString); // degree character, OCR thinks it sees it for some small text
                }

                if (word.Text.Contains("  "))
                {
                    word.Text = word.Text.Replace("  ", PositionConstants.SpaceString);
                }

                if (word.Text.Contains("\"\""))
                {
                    word.Text = word.Text.Replace("\"\"", doubleQuoteChar);
                }
            }
        }
    }

    public static bool IsPageEmpty(string? input) => IsNullOrEmptyWhitespaceOrPunctuation(input);
    
    public static bool IsLineEmpty(DocumentLine? input) => IsNullOrEmptyWhitespaceOrPunctuation(input?.Text);

    public static bool IsNullOrEmptyWhitespaceOrPunctuation(string? input)
    {
        if (input == null)
        {
            return true;
        }

        var noPunctuationInput = new string(input.Where(c => !char.IsPunctuation(c)).ToArray());
        return string.IsNullOrWhiteSpace(noPunctuationInput);
    }
    
    public static void NullOutSubLabels(IReadOnlyList<LabelGroupResult> matches)
    {
        foreach (var match in matches)
        {
            if (match.MatchedLabel != null)
            {
                match.MatchedLabel.SubLabels = null;
            }

            NullOutSubLabels(match.SubResults);
        }
    }
    
    public static void RemoveRemoves(
        LabelGroupResult labelGroupResult,
        IReadOnlyList<string>? removedLines)
    {
        if (labelGroupResult.MatchedLabel?.Remove == null)
        {
            return;
        }
        
        labelGroupResult.MatchedLabel.Remove =
            labelGroupResult.MatchedLabel.Remove!.Where(removeLine => removedLines?.Contains(removeLine.Text) == true).ToList();

        if (labelGroupResult.MatchedLabel.Remove.Count == 0)
        {
            labelGroupResult.MatchedLabel.Remove = null;
        }
    }
    
    private static IReadOnlyList<DocumentLine> TrimList(IEnumerable<DocumentLine> sourceList)
    {
        return sourceList
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .ToList();
    }
}