namespace WALE.Api.Areas.BFF.SignalR;

using Microsoft.AspNetCore.SignalR;

public class ProcessHub : Hub
{
    public Task JoinProcess(int processRunId)
    {
        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"process-{processRunId}");
    }

    public Task LeaveProcess(int processRunId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"process-{processRunId}");
    }
}