using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/database")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class DatabaseDiagnosticsController : ControllerBase
{
    private static readonly string[] ExpectedTables =
    [
        "submission_loss_years",
        "submission_loss_claims"
    ];

    private readonly ApplicationDbContext _db;

    public DatabaseDiagnosticsController(ApplicationDbContext db) => _db = db;

    [HttpGet("status")]
    public async Task<ActionResult<DatabaseStatusDto>> GetStatus(CancellationToken ct)
    {
        var canConnect = await _db.Database.CanConnectAsync(ct);
        if (!canConnect)
        {
            return Ok(new DatabaseStatusDto(
                CanConnect: false,
                ProviderName: _db.Database.ProviderName,
                DatabaseName: null,
                DataSource: null,
                LatestAppliedMigration: null,
                AppliedMigrations: [],
                PendingMigrations: [],
                ExpectedTables: ExpectedTables.Select(table => new DatabaseTableStatusDto(table, false)).ToArray()));
        }

        var connection = _db.Database.GetDbConnection();
        var appliedMigrations = (await _db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        var pendingMigrations = (await _db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        var tableStatuses = await GetTableStatusesAsync(ct);

        return Ok(new DatabaseStatusDto(
            CanConnect: true,
            ProviderName: _db.Database.ProviderName,
            DatabaseName: connection.Database,
            DataSource: connection.DataSource,
            LatestAppliedMigration: appliedMigrations.LastOrDefault(),
            AppliedMigrations: appliedMigrations,
            PendingMigrations: pendingMigrations,
            ExpectedTables: tableStatuses));
    }

    private async Task<DatabaseTableStatusDto[]> GetTableStatusesAsync(CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select table_name
                from information_schema.tables
                where table_schema = 'public'
                  and table_name = any(@table_names)
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "table_names";
            parameter.Value = ExpectedTables;
            command.Parameters.Add(parameter);

            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                existingTables.Add(reader.GetString(0));
            }

            return ExpectedTables
                .Select(table => new DatabaseTableStatusDto(table, existingTables.Contains(table)))
                .ToArray();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}

public sealed record DatabaseStatusDto(
    bool CanConnect,
    string? ProviderName,
    string? DatabaseName,
    string? DataSource,
    string? LatestAppliedMigration,
    IReadOnlyList<string> AppliedMigrations,
    IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<DatabaseTableStatusDto> ExpectedTables);

public sealed record DatabaseTableStatusDto(string Name, bool Exists);
