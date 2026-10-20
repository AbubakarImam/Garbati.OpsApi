namespace Garbati.OpsApi.Clients.Contracts.Ledger;

// Mirrors LedgerApi.Contracts.Responses.TransferResponse - the shape POST /api/deposits also returns
// (LedgerApi.Controllers.DepositsController.Deposit returns ActionResult<TransferResponse>).
public record TransferResponse(
    string Reference,
    string Status,
    string? FailureReason = null,
    string? ChainReference = null,
    int? Sequence = null,
    string? Purpose = null);
