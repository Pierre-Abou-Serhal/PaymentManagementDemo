using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PaymentManagement.DAL;
public sealed class DapperAccess(string connectionString)
{
    public DalSession Open() => new(connectionString);
}

/// <summary>Owns a SQL connection and an optional transaction across procedure calls.</summary>
public sealed class DalSession : IAsyncDisposable
{
    private static readonly Dictionary<Procedure, string> Names = Enum.GetValues<Procedure>().ToDictionary(p => p, p => "dbo.sp_" + Regex.Replace(p.ToString(), "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
    private readonly SqlConnection connection;
    private SqlTransaction? transaction;
    internal DalSession(string connectionString) => connection = new SqlConnection(connectionString);
    public Task OpenAsync(CancellationToken cancellationToken = default) => connection.State == ConnectionState.Open ? Task.CompletedTask : connection.OpenAsync(cancellationToken);
    public async Task BeginSerializableAsync()
    {
        if (transaction is not null)
        {
            throw new InvalidOperationException("This DAL session already has an active transaction.");
        }

        await OpenAsync();
        transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
    }

    public async Task CommitAsync()
    {
        var active = transaction ?? throw new InvalidOperationException("No transaction to commit.");
        await active.CommitAsync();
        await active.DisposeAsync();
        transaction = null;
    }

    public Task<IEnumerable<T>> QueryAsync<T>(Procedure procedure, object? parameters = null) => connection.QueryAsync<T>(Command(procedure, parameters));
    public async Task<IEnumerable<T>> GetDecisionTransfersAsync<T>(IEnumerable<Guid> ids)
    {
        // Bind the structured parameter explicitly rather than expanding an IN list.
        using var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        foreach (var id in ids)
        {
            table.Rows.Add(id);
        }

        var parameters = new DynamicParameters();
        parameters.Add("ids", table.AsTableValuedParameter("dbo.t_transfer_id_list"));
        return await QueryAsync<T>(Procedure.GetDecisionTransfers, parameters);
    }

    public Task<T?> QuerySingleOrDefaultAsync<T>(Procedure procedure, object? parameters = null) => connection.QuerySingleOrDefaultAsync<T>(Command(procedure, parameters));
    public Task<T> QuerySingleAsync<T>(Procedure procedure, object? parameters = null) => connection.QuerySingleAsync<T>(Command(procedure, parameters));
    public Task<T?> ExecuteScalarAsync<T>(Procedure procedure, object? parameters = null, CancellationToken cancellationToken = default) => connection.ExecuteScalarAsync<T>(Command(procedure, parameters, cancellationToken));
    public Task<int> ExecuteAsync(Procedure procedure, object? parameters = null) => connection.ExecuteAsync(Command(procedure, parameters));
    private CommandDefinition Command(Procedure procedure, object? parameters, CancellationToken cancellationToken = default) => new(Names[procedure], parameters, transaction, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
