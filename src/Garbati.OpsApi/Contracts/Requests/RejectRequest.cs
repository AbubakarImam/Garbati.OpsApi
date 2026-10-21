namespace Garbati.OpsApi.Contracts.Requests;

// Shared by both maker-checker workflows (reversal requests and customer-edit requests) - identical
// shape, no reason to duplicate. Required on every reject (task file, both workflows).
public record RejectRequest(string RejectionReason);
