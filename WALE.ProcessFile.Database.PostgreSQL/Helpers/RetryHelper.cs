using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class RetryHelper
{
    public const int MaxRetries = 5;

    public static async Task WaitWithMessageAsync(int retryNumber, string serviceName)
    {
        // NOTE - This seems to be do with a few things, including checkpointing
        
        var delayInMs = GetDelayMs(retryNumber);
        await Task.Delay(delayInMs);
        
        ConsoleHelper.WriteLine($"WARNING - {serviceName} - EndOfStreamException occured - waiting for {delayInMs}ms before retrying.");
        // TODO - Log
    }
    
    private static int GetDelayMs(int retryNumber)
    {
        return retryNumber switch
        {
            0 => 200,
            1 => 400,
            2 => 1000,
            3 => 2000,
            4 => 5000,
            5 => 10000,
            _ => throw new NotImplementedException()
        };
    }
}