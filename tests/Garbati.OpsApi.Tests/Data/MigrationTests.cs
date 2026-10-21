using Garbati.OpsApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.OpsApi.Tests.Data;

// Confirms the migrated database schema actually matches Entities/OpsSession.cs - not just that EF's
// in-memory model does (task file: "Migration test confirming the OpsSession schema matches the design").
[Collection("Postgres")]
public class MigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AppliedMigrations_IncludeInitialCreate()
    {
        await using var ctx = fixture.CreateContext();

        var applied = await ctx.Database.GetAppliedMigrationsAsync();

        Assert.Contains(applied, m => m.EndsWith("InitialCreate"));
    }

    [Fact]
    public async Task OpsSessionsTable_ExistsWithItsExpectedColumns()
    {
        await using var ctx = fixture.CreateContext();

        await AssertColumnsAsync(ctx, "ops_sessions",
            "session_id", "session_token_hash", "staff_id", "full_name", "role",
            "created_at", "last_activity_at", "expires_at");
    }

    [Fact]
    public async Task OpsSessionsTable_HasAUniqueIndexOnSessionTokenHash()
    {
        await using var ctx = fixture.CreateContext();
        var indexDef = await GetIndexDefAsync(ctx, "ops_sessions", "ix_ops_sessions_session_token_hash");

        Assert.NotNull(indexDef);
        Assert.Contains("UNIQUE", indexDef, StringComparison.OrdinalIgnoreCase);
    }

    // T-034: the real migrated schema matches Entities/OpsReversalRequest.cs and
    // Entities/OpsCustomerEditRequest.cs, not just EF's in-memory model (same discipline this file already
    // applied to OpsSession).
    [Fact]
    public async Task AppliedMigrations_IncludeAddReversalAndCustomerEditRequests()
    {
        await using var ctx = fixture.CreateContext();

        var applied = await ctx.Database.GetAppliedMigrationsAsync();

        Assert.Contains(applied, m => m.EndsWith("AddReversalAndCustomerEditRequests"));
    }

    [Fact]
    public async Task OpsReversalRequestsTable_ExistsWithItsExpectedColumns()
    {
        await using var ctx = fixture.CreateContext();

        await AssertColumnsAsync(ctx, "ops_reversal_requests",
            "id", "idempotency_key", "original_transaction_reference", "amount", "currency", "reason_code",
            "requested_by_staff_id", "status", "approved_by_staff_id", "rejection_reason", "requested_at",
            "decided_at", "executed_reference", "execution_detail");
    }

    [Fact]
    public async Task OpsCustomerEditRequestsTable_ExistsWithItsExpectedColumns()
    {
        await using var ctx = fixture.CreateContext();

        await AssertColumnsAsync(ctx, "ops_customer_edit_requests",
            "id", "target_customer_id", "proposed_full_name", "proposed_email", "proposed_phone_number",
            "requested_by_staff_id", "status", "approved_by_staff_id", "rejection_reason", "requested_at", "decided_at");
    }

    // ReversalRequestService.CreateAsync's own retry-safety (IdempotencyIndexName) depends on this exact
    // index existing and being unique - this proves the real migrated database enforces it, not just that
    // the service's code assumes it does.
    [Fact]
    public async Task OpsReversalRequestsTable_HasAUniqueIndexOnRequestedByStaffIdAndIdempotencyKey()
    {
        await using var ctx = fixture.CreateContext();
        var indexDef = await GetIndexDefAsync(
            ctx, "ops_reversal_requests", "ix_ops_reversal_requests_requested_by_staff_id_idempotency_key");

        Assert.NotNull(indexDef);
        Assert.Contains("UNIQUE", indexDef, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> GetIndexDefAsync(Microsoft.EntityFrameworkCore.DbContext ctx, string table, string indexName)
    {
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT indexdef FROM pg_indexes WHERE tablename = @table AND indexname = @indexName";
        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "table";
        tableParam.Value = table;
        command.Parameters.Add(tableParam);
        var indexParam = command.CreateParameter();
        indexParam.ParameterName = "indexName";
        indexParam.Value = indexName;
        command.Parameters.Add(indexParam);

        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task AssertColumnsAsync(Microsoft.EntityFrameworkCore.DbContext ctx, string table, params string[] expectedColumns)
    {
        var actual = new List<string>();
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name = @table";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "table";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actual.Add(reader.GetString(0));
        }

        foreach (var expected in expectedColumns)
        {
            Assert.Contains(expected, actual);
        }
    }
}
