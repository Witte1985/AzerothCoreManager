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

        var containerName = $"ac-database-{stackId}";
        var connectionString = $"Server={containerName};Port={stack.DatabasePort};Database={dbName};Uid=root;Pwd={stack.DatabaseRootPassword};AllowPublicKeyRetrieval=True;";

        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
