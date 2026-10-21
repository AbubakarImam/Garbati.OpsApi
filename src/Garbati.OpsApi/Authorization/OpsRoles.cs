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

    // DESIGN.md Section 5, "Request a reversal": the create endpoint's own role check (garbati-platform
    // T-034).
    public const string TellerOrCustomerServiceOfficer = Teller + "," + CustomerServiceOfficer;

    // DESIGN.md Section 5 / Section 4, "Reversal queue - read-only oversight" + "Request/Approve a
    // reversal": the full set of roles allowed to CALL the list/get endpoints at all (garbati-platform
    // T-034's controller-level gate). What each of them actually SEES is narrower, filtered in
    // Services/Implementations/ReversalRequestService.cs - Teller/CustomerServiceOfficer: only their own;
    // Supervisor: pending + personally decided; ComplianceOfficer/RiskOfficer/Auditor: everything, any
    // status. Administrator is deliberately absent - not in Section 4's row for this action.
    public const string ReversalQueueRoles = Teller + "," + CustomerServiceOfficer + "," + Supervisor + ","
        + ComplianceOfficer + "," + RiskOfficer + "," + Auditor;

    // DESIGN.md Section 4.1, "Update customer details": the ONLY two roles with any access at all to this
    // endpoint group (garbati-platform T-034) - every other role, including Administrator, gets 403
    // (task file, explicit - unlike reversals, there is no read-only-oversight row for ComplianceOfficer/
    // RiskOfficer/Auditor on this one).
    public const string CustomerServiceOfficerOrSupervisor = CustomerServiceOfficer + "," + Supervisor;
}
