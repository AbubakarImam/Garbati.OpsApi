namespace Garbati.OpsApi.Contracts.Responses;

public record CustomerEditRequestResponse(
    string Id,
    string TargetCustomerId,
    string? ProposedFullName,
    string? ProposedEmail,
    string? ProposedPhoneNumber,
    string RequestedByStaffId,
    string Status,
    string? ApprovedByStaffId,
    string? RejectionReason,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt,
    string? ApplicationError);
