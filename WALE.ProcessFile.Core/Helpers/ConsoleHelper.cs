namespace WALE.ProcessFile.Core.Helpers;

public static class ConsoleHelper
{
    public static bool WriteToConsoleBuffer;
    private static readonly Lock ConsoleBufferLock = new();
    private static int _writeCount;
    private static readonly List<string> ConsoleBuffer = [];

    private const bool WriteToFile = true;
    private const int FileFlushAfterXLines = 100;
    private static readonly List<string> FileBuffer = [];
    
    public static void WriteLine(object text)
    {
        if (WriteToConsoleBuffer)
        {
            lock (ConsoleBufferLock)
            {
                ConsoleBuffer.Add(text.ToString()!);
            }

            return;
        }

        if (ConsoleBuffer.Count > 0)
        {
            lock (ConsoleBufferLock)
            {
                foreach (var bufferLine in ConsoleBuffer)
                {
                    Console.WriteLine(bufferLine);

                    if (WriteToFile)
                    {
                        FileBuffer.Add(bufferLine);
                    }

                    _writeCount += 1;
                }

                ConsoleBuffer.Clear();
            }
        }

        Console.WriteLine(text);

        if (WriteToFile)
        {
            FileBuffer.Add(text.ToString() ?? string.Empty);

            if (FileBuffer.Count >= FileFlushAfterXLines)
            {
                Flush();
            }
        }

        _writeCount += 1;
    }

    public static void Flush()
    {
        using var w = File.AppendText("log.txt");
        
        foreach (var fileLine in FileBuffer)
        {
            w.WriteLine(fileLine);
        }

        FileBuffer.Clear();
    }

    public static void TryRemoveLastLine()
    {
        try
        {
            Console.SetCursorPosition(0, _writeCount);
            Console.Write(Enumerable.Repeat(' ', Console.BufferWidth).ToArray());
            Console.SetCursorPosition(0, _writeCount);
        }
        catch
        {
            // Swallow exception
        }
        finally
        {
            // Need to do the following for the cursor not to lose its place
            Console.CursorVisible = false;
            Console.CursorVisible = true;
        }
    }
}