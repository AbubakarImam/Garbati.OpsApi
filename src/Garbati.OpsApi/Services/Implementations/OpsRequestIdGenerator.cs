namespace Garbati.OpsApi.Services.Implementations;

// Same approach as every sibling service's own generated public identifier (e.g.
// Garbati.OrchestratorApi.Services.OperationIdGenerator, Garbati.IdentityApi.Services.MandateIdGenerator):
// human-readable, never a database primary key exposed directly - except here Id itself IS the primary
// key (see OpsReversalRequest's own comment on that judgment call), so this generates the one and only id
// each row has.
public static class OpsRequestIdGenerator
{
    private const string AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 10;

    // prefix is "OPS-REV" or "OPS-EDIT" - produces e.g. "OPS-REV-A7K3M9P2QX" (task file's exact
    // OPS-REV-XXXXXXXXXX / OPS-EDIT-XXXXXXXXXX shape, 10 generated characters).
    public static string Generate(string prefix)
    {
        var id = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            id[i] = AllowedChars[Random.Shared.Next(AllowedChars.Length)];
        }
        return $"{prefix}-{new string(id)}";
    }
}
