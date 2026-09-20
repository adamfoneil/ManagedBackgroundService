using Dapper;
using Microsoft.Data.Sqlite;
using ManagedBackgroundServices.Abstractions;

namespace WebDemo;

public class SqLiteDurableQueue : DurableQueue
{
    private readonly string _connectionString;
    private readonly string _tableName = "queue_messages";

    public SqLiteDurableQueue(string databasePath = "queue.db")
    {
        _connectionString = $"Data Source={databasePath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute($@"
            CREATE TABLE IF NOT EXISTS {_tableName} (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                type_name TEXT NOT NULL,
                handler_name TEXT NOT NULL,
                machine_name TEXT NOT NULL,
                json_data TEXT NOT NULL,
                created_at TEXT NOT NULL,
                processed INTEGER DEFAULT 0
            )");
    }

    protected override async Task StoreMessageAsync(QueueMessage message)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        await connection.ExecuteAsync($@"
                INSERT INTO {_tableName} (timestamp, type_name, handler_name, machine_name, json_data, created_at)
                VALUES (@Timestamp, @TypeName, @HandlerName, @MachineName, @JsonData, @CreatedAt)",
            new
            {
                message.Timestamp,
                message.TypeName,
                message.HandlerName,
                message.MachineName,
                message.JsonData,
                CreatedAt = DateTime.UtcNow
            });
    }

    protected override async Task<IEnumerable<QueueMessage>> DequeueMessagesAsync(int batchSize, string machineName, CancellationToken stoppingToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Get unprocessed messages and mark them as processed in a single transaction
        using var transaction = connection.BeginTransaction();

        try
        {
            var messages = await connection.QueryAsync<(long id, string timestamp, string type_name, string handler_name, string machine_name, string json_data)>($@"
                        SELECT id, timestamp, type_name, handler_name, machine_name, json_data 
                        FROM {_tableName}
                        WHERE processed = 0 AND machine_name = @MachineName
                        ORDER BY id ASC
                        LIMIT @BatchSize",
                    new { BatchSize = batchSize, MachineName = machineName },
                    transaction: transaction);

            if (messages.Any())
            {
                var ids = messages.Select(m => m.id).ToList();
                await connection.ExecuteAsync($@"
                            UPDATE {_tableName}
                            SET processed = 1
                            WHERE id IN ({string.Join(",", ids)})",
                    transaction: transaction);
            }

            transaction.Commit();

            return [.. messages.Select(m => new QueueMessage(
                DateTime.Parse(m.timestamp),
                m.type_name,
                m.handler_name,
                m.machine_name,
                m.json_data
            ))];
        }
        catch 
        {
            transaction.Rollback();
            throw;
        }        
    }
}


