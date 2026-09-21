# SqLitePersistentQueue Implementation

## Overview
The `SqLitePersistentQueue` class is a SQLite-based implementation of the `PersistentQueue` abstract class from the ManagedBackgroundServices framework. It provides persistent, durable message queuing capabilities for background jobs.

## Key Features

### Constructor
- `SqLitePersistentQueue(string databasePath = "queue.db")`
- Creates or opens a SQLite database file at the specified path
- Automatically initializes the schema on first run

### Core Functionality

#### 1. **Message Storage** (`StoreMessageAsync`)
- Stores queue messages in SQLite with the following fields:
  - Timestamp: UTC timestamp when the message was created
  - Type Name: Fully qualified type name of the message payload
  - Machine Name: Name of the machine that stored the message
  - JSON Data: Serialized message payload
  - Created At: When the message was stored
  - Processed: Flag indicating if the message has been processed

#### 2. **Message Dequeuing** (`DequeueMessagesAsync`)
- Retrieves unprocessed messages from the queue in batches
- Uses atomic transaction to:
  1. Select up to `batchSize` unprocessed messages (ordered by insertion time)
  2. Mark selected messages as processed
- Handles CancellationToken for graceful shutdown

#### 3. **Public Interface** (`DequeueAsync`)
- Entry point for the background service consumer
- Converts messages to array format
- Respects the machine name constraint

## Database Schema

```sql
CREATE TABLE queue_messages (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	timestamp TEXT NOT NULL,
	type_name TEXT NOT NULL,
	machine_name TEXT NOT NULL,
	json_data TEXT NOT NULL,
	created_at TEXT NOT NULL,
	processed INTEGER DEFAULT 0
)
```

## Dependencies
- **Microsoft.Data.Sqlite** (v10.0.0): Official Microsoft SQLite provider for .NET
  - Lightweight, zero-config
  - Fully managed, no native dependencies required
  - Cross-platform support

## Integration Example

```csharp
// Program.cs
builder.Services
    .AddDurableQueue<SqLiteDurableQueue>(sp => new SqLiteDurableQueue("queue.db"))
    .AddQueueConsumer<SampleMessage, SampleMessageHandler>();

// Now inject it in your components or services:
// @inject DurableQueue Queue
// or in a service constructor: public MyService(DurableQueue queue)
```

## Thread Safety
- All database operations are wrapped in `Task.Run()` to execute on thread pool threads
- SQLite handles locking automatically
- Safe for concurrent access from multiple consumers

## Error Handling
- Database initialization failures are thrown immediately at startup
- Query failures during dequeue are propagated to the caller
- Missing database file is automatically created

## Performance Considerations
- Simple schema with single table for fast queries
- Indexed lookup on `processed` and `id` for dequeue operations
- Batch processing reduces round trips to database
- LIMIT clause prevents memory exhaustion on large queues
