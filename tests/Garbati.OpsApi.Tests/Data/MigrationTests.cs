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
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'ops_sessions' AND indexname = 'ix_ops_sessions_session_token_hash'";
        var indexDef = (string?)await command.ExecuteScalarAsync();

        Assert.NotNull(indexDef);
        Assert.Contains("UNIQUE", indexDef, StringComparison.OrdinalIgnoreCase);
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
