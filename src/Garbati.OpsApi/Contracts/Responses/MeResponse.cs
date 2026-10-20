namespace Garbati.OpsApi.Contracts.Responses;

// DESIGN.md Section 2.1a: "Returns {staffId, fullName, role} from the current session - no downstream call."
public record MeResponse(string StaffId, string FullName, string Role);
