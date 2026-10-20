namespace Garbati.OpsApi.Clients.Contracts.Identity;

// Mirrors Garbati.IdentityApi.Contracts.Responses.PendingApprovalRequestResponse exactly - the shape
// GET /v1/authorization/requests?status=Pending (T-031) and GET .../{id} both return. DESIGN.md Section
// 6 item 6: a thin pass-through, so this service's own response is this shape, unmodified.
public record PendingApprovalRequestResponse(
    string RequestId,
    string Kind,
    string Status,
    string LedgerAccountId,
    string RequestedBy,
    string? Operation,
    string? DebitAccountId,
    string? CreditAccountId,
    string? Amount,
    string? Currency,
    int? RequestedRequiredApprovals,
    int RequiredApprovals,
    int ApprovalCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? DecisionId,
    string? Grant);
