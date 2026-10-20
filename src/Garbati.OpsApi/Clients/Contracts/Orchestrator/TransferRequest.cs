namespace Garbati.OpsApi.Clients.Contracts.Orchestrator;

// Mirrors Garbati.OrchestratorApi.Contracts.Requests.TransferRequest. RequestedBy is whatever CustomerId
// the staff member operating the testing console types in - OrchestratorApi's own known gap (its DESIGN.md
// Section 1.6: no real Channel authenticates this yet) is unchanged by this service sitting in front of
// it; this is a staff testing tool, not a customer-facing path.
public record TransferRequest(
    string IdempotencyKey,
    string RequestedBy,
    string DebitAccountId,
    string CreditAccountId,
    decimal Amount,
    string Currency,
    string? Narration);
