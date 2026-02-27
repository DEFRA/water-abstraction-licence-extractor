namespace WALE.ProcessFile.Core.Helpers;

public static class ConsoleHelper
{
    public static bool WriteToBuffer;

    private static int _writeCount;
    private static readonly List<string> Buffer = [];
    
    public static void WriteLine(object text)
    {
        if (WriteToBuffer)
        {
            Buffer.Add(text.ToString()!);
            return;
        }

        foreach (var bufferLine in Buffer)
        {
            Console.WriteLine(bufferLine);
            _writeCount += 1;
        }
        
        Buffer.Clear();

        Console.WriteLine(text);
        _writeCount += 1;
    }

    public static void RemoveLastLine()
    {
        Console.SetCursorPosition(0, _writeCount);
        Console.Write(Enumerable.Repeat(' ', Console.BufferWidth).ToArray());
        Console.SetCursorPosition(0, _writeCount);
        
        // Need to do the following for the cursor not to lose its place
        Console.CursorVisible = false;
        Console.CursorVisible = true;
    }
}