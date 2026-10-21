namespace Garbati.OpsApi.Contracts.Responses;

public record ReversalRequestResponse(
    string Id,
    string OriginalTransactionReference,
    decimal Amount,
    string Currency,
    string ReasonCode,
    string RequestedByStaffId,
    string Status,
    string? ApprovedByStaffId,
    string? RejectionReason,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt,
    string? ExecutedReference,
    string? ExecutionDetail);
