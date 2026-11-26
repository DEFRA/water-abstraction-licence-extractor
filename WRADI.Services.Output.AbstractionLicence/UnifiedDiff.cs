using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Output.AbstractionLicence;

public static class UnifiedDiff
{
    public const int ContextLines = 3;

    public static List<LicenceRunDiffLine> Build(
        IReadOnlyList<string> older,
        IReadOnlyList<string> newer,
        int contextLines = ContextLines)
    {
        var lines = Walk(older, newer, LongestCommonSubsequence(older, newer));

        return contextLines < 0 ? lines : Collapse(lines, contextLines);
    }

    /// Row-by-row rather than the full table: the licence JSON runs to a few hundred lines, so
    /// the quadratic table is small, but there is no reason to hold all of it at once.
    private static int[,] LongestCommonSubsequence(
        IReadOnlyList<string> older,
        IReadOnlyList<string> newer)
    {
        var lengths = new int[older.Count + 1, newer.Count + 1];

        for (var olderIndex = older.Count - 1; olderIndex >= 0; olderIndex--)
        {
            for (var newerIndex = newer.Count - 1; newerIndex >= 0; newerIndex--)
            {
                lengths[olderIndex, newerIndex] =
                    older[olderIndex] == newer[newerIndex]
                        ? lengths[olderIndex + 1, newerIndex + 1] + 1
                        : Math.Max(
                            lengths[olderIndex + 1, newerIndex],
                            lengths[olderIndex, newerIndex + 1]);
            }
        }

        return lengths;
    }

    private static List<LicenceRunDiffLine> Walk(
        IReadOnlyList<string> older,
        IReadOnlyList<string> newer,
        int[,] lengths)
    {
        var lines = new List<LicenceRunDiffLine>();
        var olderIndex = 0;
        var newerIndex = 0;

        while (olderIndex < older.Count && newerIndex < newer.Count)
        {
            if (older[olderIndex] == newer[newerIndex])
            {
                lines.Add(Line(LicenceRunDiffLineKind.Context, olderIndex, newerIndex, older[olderIndex]));

                olderIndex++;
                newerIndex++;
            }
            else if (lengths[olderIndex + 1, newerIndex] >= lengths[olderIndex, newerIndex + 1])
            {
                lines.Add(Line(LicenceRunDiffLineKind.Removed, olderIndex, null, older[olderIndex]));

                olderIndex++;
            }
            else
            {
                lines.Add(Line(LicenceRunDiffLineKind.Added, null, newerIndex, newer[newerIndex]));

                newerIndex++;
            }
        }

        for (; olderIndex < older.Count; olderIndex++)
        {
            lines.Add(Line(LicenceRunDiffLineKind.Removed, olderIndex, null, older[olderIndex]));
        }

        for (; newerIndex < newer.Count; newerIndex++)
        {
            lines.Add(Line(LicenceRunDiffLineKind.Added, null, newerIndex, newer[newerIndex]));
        }

        return lines;
    }

    private static LicenceRunDiffLine Line(
        LicenceRunDiffLineKind kind,
        int? olderIndex,
        int? newerIndex,
        string text) =>
        new()
        {
            Kind = kind,
            OlderLineNumber = olderIndex + 1,
            NewerLineNumber = newerIndex + 1,
            Text = text
        };

    private static List<LicenceRunDiffLine> Collapse(
        List<LicenceRunDiffLine> lines,
        int contextLines)
    {
        var keep = new bool[lines.Count];

        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Kind == LicenceRunDiffLineKind.Context)
            {
                continue;
            }

            var from = Math.Max(0, index - contextLines);
            var to = Math.Min(lines.Count - 1, index + contextLines);

            for (var near = from; near <= to; near++)
            {
                keep[near] = true;
            }
        }

        var collapsed = new List<LicenceRunDiffLine>();
        var skipped = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            if (keep[index])
            {
                Flush(collapsed, ref skipped);

                collapsed.Add(lines[index]);
            }
            else
            {
                skipped++;
            }
        }

        // A trailing run is skipped too, or the diff simply stops and the reader cannot tell
        // the document ended from the rest of it being unchanged.
        Flush(collapsed, ref skipped);

        return collapsed;
    }

    private static void Flush(List<LicenceRunDiffLine> collapsed, ref int skipped)
    {
        if (skipped == 0)
        {
            return;
        }

        collapsed.Add(
            new LicenceRunDiffLine
            {
                Kind = LicenceRunDiffLineKind.Skipped,
                Text = $"{skipped} unchanged line{(skipped == 1 ? "" : "s")}"
            });

        skipped = 0;
    }
}
