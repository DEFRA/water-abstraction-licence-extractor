namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Ground truth grid structure per document, read directly off the rendered page rather than
/// inferred - lets a cell-reconstruction bug be asserted against directly ("these two phrases
/// must never land in the same cell", "these phrases must land in the SAME cell together")
/// instead of eyeballing printed cell dumps. Each inner group is phrases that belong in one
/// cell together (a plain field's own label+value, or - where the document draws a genuine
/// colspan/rowspan - everything that merged cell should contain); every group must land in a
/// cell containing none of any other group's phrases.
/// </summary>
public static class ExpectedGridPatterns
{
    // wr51__2671321040 (T1) - Licence provisions + Measurement details grid, read off the
    // rendered page (200dpi render, cropped to this band) top to bottom, left to right.
    public static readonly string[][] T1_2671321040_LicenceProvisionsAndMeasurementDetails =
    [
        ["Source", "of", "supply:"],
        ["Quantities:"],
        ["Land", "(only", "if", "specified):", "n/a"],
        ["Point", "of", "abstraction:"],
        ["Means", "of", "measurement:"],
        ["Charging", "factors:", "n/a"],
        ["Means", "of", "abstraction:"],
        ["R", "ecords:"], // letter-kerned on this document - see RuleRecords' own comment
        ["Other", "provisions", "(specify", "below):", "n/a"], // rowspan
        ["Purpose(s):"],
        ["Provision", "of", "information:", "n/a"],
        ["Period:", "n/a"],
        ["Special", "conditions:", "n/a"],
        ["Measurement", "details"],
        ["Meter", "make:", "Not", "checked"],
        ["Serial", "number:", "Not", "checked"],
        ["Reading:", "Not", "operational"],
        ["Units:", "Not", "operational"],
        ["Certificates", "or", "records", "available", "for:", "Not", "checked"],
        ["Date", "of", "certificate", "or", "record:", "not", "checked"], // rowspan
        ["Calibration:"],
        ["Conformance:"],
        ["Flow", "verification:"],
        ["Meter", "verification:"],
        ["Maintenance:"],
        ["Readings", "taken:"],
        ["Where", "kept:"]
    ];

    // Same document, the header block above the grid - deliberately includes the 3-line
    // "Name and address" wrap and the 2-line "Telephone No"/"Email" wraps, exactly the shape
    // that needed AllowValueToWrapToNextLine/AllowValueToWrapPastSameLineEndTag special-cased
    // rule flags this session to reach at all. A real cell naturally contains every wrapped
    // line with no special-casing needed - this pattern is the evidence for that claim.
    public static readonly string[][] T1_2671321040_HeaderBlock =
    [
        ["Form", "WR", "-", "51"],
        ["Licence", "No.", "(or", "Application", "No.", "or", "GIC", "No.", "etc.)"],
        ["Inspection", "Class:", "HC"],
        ["Name", "and", "address:", "United", "Utilities", "Group", "PLC,", "Haweswater", "House,",
            "Lingley", "Mere", "Business", "Park,", "Lingley", "Green", "Avenue,", "Great", "Sankey,",
            "Warrington,", "WA5", "3LP"],
        ["Telephone", "No:", "07800", "530128"],
        ["Site", "address", "(if", "different):", "Corn", "Close", "BH", "2"],
        ["Email:", "nicola.heap@uuplc.co.uk"],
        ["Met", "with:", "Nicola", "Heap"],
        ["Position:", "Catchment", "Coordinator"]
    ];

    // wr51__940030021sr (T4) - a structurally different template from T1: LicenceProvisions
    // cells hold real multi-line narrative content instead of a bare tick (Source of
    // supply/Quantities/Point of abstraction etc.), "Other provisions" spans 3 row-slots (not
    // T1's 2), Site address has no Email column, and Measurement details has two full meter
    // rows. Text copied verbatim from actual output, including this document's own PdfPig
    // tokenisation quirks (letter-spaced "A l l", split ordinal suffixes "1 st") - matching a
    // fixed field's exact rendering isn't the point here, matching cell BOUNDARIES is.
    public static readonly string[][] T4_940030021sr =
    [
        ["Form", "WR", "-", "51"],
        ["Licence", "No:", "9/40/03/0021/SR"],
        ["Inspection", "Class:", "Highly", "Critical"],
        ["Permit", "holder", "name", "and", "address:", "Clock", "House", "Farm", "Ltd.", "The", "Farm",
            "Office,", "Clock", "House", "Farm,", "Heath", "Road,", "Coxheath,", "Maidstone,", "KENT,",
            "ME17", "4PG"],
        ["Telephone", "No:", "01622", "743955"],
        ["Site", "address", "(if", "different):", "Court", "Lodge", "Farm", "(Spray)", "–", "HydroRef",
            "E5/043.", "Yalding", "ME18.", "AKA", "Gooselands"],
        ["Met", "with:", "Ollie", "Bekker,", "Macey", "Willard,", "Stephen", "Wilson"],
        ["Position:", "CHF", "representatives"],
        ["Inspecting", "Officer:", "Jac", "Corbett"],
        ["Inspection", "Date:", "26/1/2026"],
        ["Time:", "3pm"],
        ["Source", "of", "supply:", "✓", "River", "Beult", "&", "River", "Medway", "in", "the", "parish",
            "of", "Yalding", "Kent"],
        ["Quantities:", "✓", "40187.44", "cubic", "metres", "per", "year"],
        ["Land", "(only", "if", "specified):", "see", "notes", "Area", "in", "red", "on", "map", "in",
            "licence"],
        ["Point", "of", "abstraction:", "✓", "Between", "NGRs", "TQ", "6925", "5000", "and", "TQ", "6926",
            "5023", "(Points", "A", "&", "B)"],
        ["Means", "of", "measurement:", "✓", "Meters", "to", "be", "read", "annually"],
        ["Charging", "factors:"],
        ["Means", "of", "abstraction:", "✓", "Pumps", "not", "exceeding", "46.2", "lps"],
        ["Records:", "see", "notes", "A", "l", "l", "readings", "of", "the", "meters", "and", "hours"],
        ["Purpose(s):", "Spray", "Irrigation", "✓"],
        ["Provision", "of", "information", "Annual", "return", "before"],
        ["Period:", "15", "May", "to", "15", "September"],
        ["Special", "conditions:"],
        ["Other", "provisions", "(specify", "below):", "X", "Condition", "9.2", "-", "additional",
            "abstraction"],
        ["Measurement", "details"],
        ["Meter", "at", "previous", "site", "visit", "25", "th", "March", "2025", "Meter", "make:", "ARAD",
            "Serial", "number:", "20-100019688"],
        ["Reading:", "0022430", "x10", "display"],
        ["Meter", "make:", "VuAqua", "Serial", "number", "25", "061010"],
        ["Reading", "000001", "m", "3"],
        ["Certificates", "or", "records", "available:", "see", "notes"],
        ["Calibration:"],
        ["Conformance:"],
        ["Flow", "verification:"],
        ["Meter", "verification:"],
        ["Date", "of", "certificate", "or", "record:"],
        ["Maintenance:"],
        ["Readings", "taken:"],
        ["Where", "kept:"]
    ];

    // wr51__121013s32 (T6) - includes the big free-text "General comments" narrative block
    // (a completely different field shape from every tick/cross/label+value cell around it)
    // and the "Form sent to"/"Date" footer row below the main grid.
    public static readonly string[][] T6_121013s32 =
    [
        ["Form", "WR", "-", "51"],
        ["Licence", "No.", "(or", "Application", "No.", "or", "GIC", "No.", "etc.)", "12/101/3/S/32"],
        ["Name", "and", "address:", "Southern", "Water,", "Southern", "House,", "Yeoman", "Road,",
            "Worthing,", "BN13", "3NX"],
        ["Telephone", "No:", "0", "7", "5", "4", "2", "3", "9", "6", "3", "00"],
        ["Site", "address", "(if", "different):", "Medina", "–", "Yar", "Transfer", "at", "Blackwater",
            "Corner,", "Sandy", "Lane,", "P030", "3BS"],
        ["Email:", "jane.batchelor@southernw"],
        ["Met", "with:", "Jane", "Batchelor"],
        ["Position:", "Flow", "Measurement", "Coordinator"],
        ["Inspecting", "Officer:", "Arthur", "Layton,", "Filippos", "Sidiropoulos"],
        ["Inspection", "Date:", "20", "th", "January", "2026"],
        ["Time:", "09:50"],
        ["Source", "of", "supply:", "✓"],
        ["Quantities:", "✓"],
        ["Land", "(only", "if", "specified):", "N/A"],
        ["Point", "of", "abstraction:", "✓"],
        ["Means", "of", "measurement:", "✓"],
        ["Charging", "factors:", "N/A"],
        ["Means", "of", "abstraction:", "✓"],
        ["R", "e", "c", "o", "rds:", "✓"], // letter-kerned differently again on this document
        ["Purpose(s):", "✓"],
        ["Provision", "of", "information:", "✓"],
        ["Period:", "✓"],
        ["Special", "conditions:", "✓"],
        ["Other", "provisions", "(specify", "below):", "HoF"], // rowspan
        ["Meter", "name:", "Medina", "Yar", "SWA"],
        ["Asset", "number:", "1080970"],
        ["Meter", "make:", "ABB", "Magmaster"],
        ["Serial", "number:", "V/18947/1/1"],
        ["Reading:", "4,338,914"],
        ["Units:", "m3"],
        ["Certificates", "or", "records", "available", "for:"],
        ["Calibration:"],
        ["Conformance:"],
        ["Flow", "verification:"],
        // "Meter verification:" deliberately not asserted here - it's a genuine text collision
        // in the source document itself (also appears as a sub-heading inside the General
        // comments narrative below), not ambiguous in the reconstruction.
        ["Date", "of", "certificate", "or", "record:", "08/04/2025"], // rowspan
        ["Maintenance:"],
        ["Readings", "taken:"],
        ["Where", "kept:", "Site", "diary", "and", "online."],
        ["General", "comments,", "details", "/", "dates", "of", "occupation", "changes,", "actions",
            "required", "etc.", "Licence", "12/101/3/S/32", "allows", "for", "water", "to", "be",
            "transferred", "from", "the", "River", "Medina", "to", "the", "River", "Yar"],
        ["Form", "sent", "to:", "Jane", "Batchelor"],
        ["Date:", "10", "th", "February", "2026"]
    ];
}
