namespace Garbati.OpsApi.Exceptions;

// Thrown by ReversalRequestService/CustomerEditRequestService when an approve/reject action targets an id
// that does not exist. Mapped to 404 by ExceptionHandlingMiddleware.
public class OpsRequestNotFoundException(string message) : Exception(message);
