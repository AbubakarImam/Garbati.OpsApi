namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md Section 4.1. Field-for-field partial-update shape matching IdentityApi's own
// UpdateCustomerDetailsRequest (garbati-platform T-035, read directly against its real source - see the
// PR description for whether that task had merged to main by the time this was written) - not a generic
// JSON blob (task file, explicit). At least one of the three proposed fields must be non-null (checked by
// the controller, 400 otherwise) - nothing to propose otherwise.
public record CreateCustomerEditRequestRequest(
    string TargetCustomerId, string? ProposedFullName, string? ProposedEmail, string? ProposedPhoneNumber);
