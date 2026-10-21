namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md Section 5.1. OriginalTransactionReference/Amount/Currency are caller-supplied - LedgerApi has
// no lookup-by-reference endpoint to derive them from, the same reasoning TransferRequest/
// AuthorizationRequest already rely on elsewhere on this platform. ReasonCode must be one of
// Validation.ReversalReasonCodes.All. IdempotencyKey is required and client-supplied
// (Validation/IdempotencyKeyRule.cs) - unlike CreateStaffRequest, this request eventually moves money, so
// it must be retry-safe from the moment it is created, not generated server-side (task file, explicit).
public record CreateReversalRequestRequest(
    string IdempotencyKey, string OriginalTransactionReference, decimal Amount, string Currency, string ReasonCode);
