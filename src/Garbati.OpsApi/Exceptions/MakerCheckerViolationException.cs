namespace Garbati.OpsApi.Exceptions;

// The hard maker-checker rule (task file / DESIGN.md decision #4): the approver must not be the same
// person as the requester, and must currently hold Supervisor. [Authorize(Roles = OpsRoles.Supervisor)] on
// the controller action already guarantees the role half of this for every real request - re-resolved
// fresh per request by SessionAuthenticationHandler from the database, never a value cached from an
// earlier page load. This exception exists for the half that attribute cannot express (whether THIS
// specific resource's own requester is the one trying to decide it), and as a defensive,
// directly-unit-testable check at the service layer rather than relying on the attribute alone (task file:
// "enforced in code at the moment of decision"). Mirrors
// Garbati.IdentityApi.Exceptions.JointApprovalDeniedException's own "business rule, not a missing-role
// problem" shape. Mapped to 422 by ExceptionHandlingMiddleware.
public class MakerCheckerViolationException(string message) : Exception(message);
