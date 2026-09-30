using org.pdfclown.documents.contents;
using org.pdfclown.documents.contents.objects;
using PdfClownPath = org.pdfclown.documents.contents.objects.Path;

// Replicates PdfClownGridTableExtractorService's own content-stream scanning logic directly
// (Scan/CollectPathRectangles), bypassing its swallowed try/catch, so a crash is visible rather
// than silently reported as "found no tables". This is the exact code path
// WrInspectionReportExtractionOrchestrator -> PdfClownGridTableExtractorService.GetTablesAsync
// exercises in production - not a simplified or isolated repro.
//
// Usage: dotnet PdfClownDockerVerify.dll <path-to-pdf>

var pdfPath = "wr51__sw0480192006__c9b5d652-2132-42be-9542-58f7dd894d92.pdf";//args[0];

Console.WriteLine($"Reading {pdfPath}");
var bytes = File.ReadAllBytes(pdfPath);

try
{
    var pdf = new org.pdfclown.files.File(new org.pdfclown.bytes.Buffer(bytes));

    using (pdf)
    {
        var pageNumber = 0;
        var totalRects = 0;

        foreach (var page in pdf.Document.Pages)
        {
            pageNumber++;
            Console.WriteLine($"Scanning page {pageNumber}");

            var scanner = new ContentScanner(page);
            totalRects += Scan(scanner);
        }

        Console.WriteLine($"SUCCESS: scanned {pageNumber} page(s), found {totalRects} filled rectangle(s) total");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"EXCEPTION: {ex.GetType().FullName}: {ex.Message}");
    Console.WriteLine(ex.ToString());
    var inner = ex.InnerException;
    while (inner != null)
    {
        Console.WriteLine("--- inner ---");
        Console.WriteLine(inner.ToString());
        inner = inner.InnerException;
    }
    Environment.Exit(1);
}

static int Scan(ContentScanner? scanner)
{
    if (scanner == null) return 0;
    var count = 0;

    while (scanner.MoveNext())
    {
        var current = scanner.Current;

        if (current is PdfClownPath)
        {
            count += CollectPathRectangles(scanner.ChildLevel, count);
            continue;
        }

        if (current is CompositeObject)
        {
            count += Scan(scanner.ChildLevel);
        }
    }

    return count;
}

static int CollectPathRectangles(ContentScanner? pathLevel, int countSoFar)
{
    if (pathLevel == null) return 0;

    var rectangles = new List<DrawRectangle>();
    var filled = false;

    while (pathLevel.MoveNext())
    {
        switch (pathLevel.Current)
        {
            case DrawRectangle rect:
                rectangles.Add(rect);
                break;
            case PaintPath { Filled: true }:
                filled = true;
                break;
        }
    }

    if (!filled || rectangles.Count == 0) return 0;

    var count = 0;
    foreach (var rect in rectangles)
    {
        Console.WriteLine($"  rect #{countSoFar + count} before transform");
        var p1 = pathLevel.State.UserToDeviceSpace(new System.Drawing.PointF((float)rect.X, (float)rect.Y));
        var p2 = pathLevel.State.UserToDeviceSpace(
            new System.Drawing.PointF((float)(rect.X + rect.Width), (float)(rect.Y + rect.Height)));
        count++;
    }

    return count;
}
