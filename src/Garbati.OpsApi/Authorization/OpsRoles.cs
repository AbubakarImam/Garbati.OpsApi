namespace Garbati.OpsApi.Authorization;

// Exactly Garbati.StaffIdentityApi.Entities.Role's seven values, as strings - this service never
// references that enum type directly (StaffIdentityApi and OpsApi are separate deployables with no shared
// assembly), only the same string values StaffIdentityApi's own AuthenticateResponse/StaffResponse report
// (ARCHITECTURE.md Section 5: no shared database, no cross-service type reuse). DESIGN.md Section 4's
// matrix is the actual authorization surface this task enforces - these constants exist so that matrix
// can be expressed as ASP.NET Core's own built-in [Authorize(Roles = "...")], rather than a bespoke
// policy-per-scope mechanism (contrast Garbati.OrchestratorApi.Authorization.OrchestratorScopes, one
// policy per API-key scope): a staff member's Role is a single claim already resolved once per request by
// SessionAuthenticationHandler, so the framework's own role check is the "one shared, reusable check" the
// task file asks for - no custom attribute needed.
public static class OpsRoles
{
    public const string Teller = "Teller";
    public const string CustomerServiceOfficer = "CustomerServiceOfficer";
    public const string Supervisor = "Supervisor";
    public const string ComplianceOfficer = "ComplianceOfficer";
    public const string RiskOfficer = "RiskOfficer";
    public const string Auditor = "Auditor";
    public const string Administrator = "Administrator";

    public static readonly IReadOnlyList<string> All =
    [
        Teller, CustomerServiceOfficer, Supervisor, ComplianceOfficer, RiskOfficer, Auditor, Administrator,
    ];

    // DESIGN.md Section 4, "Customer & account lookup (read-only)": every role.
    public const string AnyRole = Teller + "," + CustomerServiceOfficer + "," + Supervisor + ","
        + ComplianceOfficer + "," + RiskOfficer + "," + Auditor + "," + Administrator;

    // DESIGN.md Section 4, "Open account / fund / transfer - testing console".
    public const string TellerOrSupervisor = Teller + "," + Supervisor;

    // DESIGN.md Section 4, "Joint-account approval test console": Supervisor acts, Auditor is read-only -
    // these two roles may both reach the read endpoints; only Supervisor may reach the approve action.
    public const string SupervisorOrAuditor = Supervisor + "," + Auditor;
}
