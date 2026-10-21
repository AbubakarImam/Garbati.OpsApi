namespace Garbati.OpsApi.Exceptions;

// Thrown when an approve/reject action targets a request that is no longer actionable - already decided,
// or past its 24-hour PendingApproval window. This is the live, decision-time check: a request can still
// be found by GetAsync and even appear outside "pending" list results, but approving/rejecting it must
// still correctly fail (task file, explicit: "excluding it from 'pending' results... even though approving
// it would still correctly fail"). Mirrors
// Garbati.IdentityApi.Exceptions.PendingApprovalRequestNotPendingException's own "resource exists, wrong
// state" shape. Mapped to 409 by ExceptionHandlingMiddleware.
public class OpsRequestNotPendingException(string message) : Exception(message);
