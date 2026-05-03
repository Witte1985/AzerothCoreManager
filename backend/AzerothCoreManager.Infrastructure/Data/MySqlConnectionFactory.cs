using System.Data.Common;
using AzerothCoreManager.Core.Services.Interfaces;
using AzerothCoreManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace AzerothCoreManager.Infrastructure.Data;

/// <summary>
/// Factory for creating MySQL connections to AzerothCore databases
/// </summary>
public class MySqlConnectionFactory : IMySqlConnectionFactory
{
    private readonly AzerothCoreDbContext _dbContext;

    public MySqlConnectionFactory(AzerothCoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DbConnection> CreateConnectionAsync(string stackId, string database, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == stackId, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{stackId}' not found");
        }

        var dbName = database.ToLowerInvariant() switch
        {
            "auth" => "acore_auth",
            "world" => "acore_world",
            "characters" => "acore_characters",
            _ => throw new ArgumentException($"Unknown database type: {database}. Valid values are: auth, world, characters", nameof(database))
        };

        // Connect to localhost since the backend is running on the host machine
        // The MySQL container exposes its port to the host
        // Use MySqlConnectionStringBuilder to properly escape special characters in password
        var builder = new MySqlConnectionStringBuilder
        {
            Server = "localhost",
            Port = (uint)stack.DatabasePort,
            Database = dbName,
            UserID = "root",
            Password = stack.DatabaseRootPassword,
            AllowPublicKeyRetrieval = true
        };

        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
