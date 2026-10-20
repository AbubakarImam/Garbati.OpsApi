using Garbati.OpsApi.Clients.Contracts.Orchestrator;
using Garbati.OpsApi.Clients.Interfaces;

namespace Garbati.OpsApi.Tests.Fixtures;

public class FakeOrchestratorApiClient : IOrchestratorApiClient
{
    public int StartAccountOpeningCallCount { get; private set; }
    public int TransferCallCount { get; private set; }

    public StartAccountOpeningRequest? LastStartAccountOpeningRequest { get; private set; }
    public TransferRequest? LastTransferRequest { get; private set; }

    public Func<StartAccountOpeningRequest, AccountOpeningResponse>? StartAccountOpeningHandler { get; set; }
    public Func<TransferRequest, TransferResultResponse>? TransferHandler { get; set; }

    public Task<AccountOpeningResponse> StartAccountOpeningAsync(StartAccountOpeningRequest request, CancellationToken cancellationToken)
    {
        StartAccountOpeningCallCount++;
        LastStartAccountOpeningRequest = request;
        var handler = StartAccountOpeningHandler ?? (r => new AccountOpeningResponse(
            $"OP-FAKE{StartAccountOpeningCallCount:D7}", "Complete", "Complete", "CUS-FAKE1", "1000000099",
            null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        return Task.FromResult(handler(request));
    }

    public Task<TransferResultResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        TransferCallCount++;
        LastTransferRequest = request;
        var handler = TransferHandler ?? (r => new TransferResultResponse(
            $"DEC-FAKE{TransferCallCount:D7}", $"TXN-FAKE{TransferCallCount:D7}", "Posted", null));
        return Task.FromResult(handler(request));
    }
}
