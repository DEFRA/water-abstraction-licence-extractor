namespace WALE.ProcessFile.Core.Models;

public class FileProcessQueueBatchResult
{
    public int Requested { get; set; }

    public int Enqueued { get; set; }

    public List<Guid> FailedFileIds { get; set; } = [];
}
