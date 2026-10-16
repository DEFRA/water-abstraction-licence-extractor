namespace WALE.Api.Areas.BFF.Models;

// Enqueued is deliberately reported rather than inferred: SQS can reject individual entries in an
// otherwise successful batch, so "the request succeeded" and "every file was queued" are different
// facts and the caller needs both.
public class SendFileProcessSingleMessagesResponse
{
    public int Requested { get; set; }

    public int Enqueued { get; set; }

    public List<Guid> FailedFileIds { get; set; } = [];
}
