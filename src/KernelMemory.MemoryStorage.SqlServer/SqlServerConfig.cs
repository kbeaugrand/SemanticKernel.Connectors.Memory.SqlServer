// Copyright (c) Kevin BEAUGRAND. All rights reserved.

namespace KernelMemory.MemoryStorage.SqlServer;

/// <summary>
/// Configuration for the SQL Server memory store.
/// </summary>
public class SqlServerConfig
{
    /// <summary>
    /// The default schema used by the SQL Server memory store.
    /// </summary>
    public const string DefaultSchema = "dbo";

    /// <summary>
    /// The connection string to the SQL Server database.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// The schema used by the SQL Server memory store.
    /// </summary>
    public string Schema { get; set; } = DefaultSchema;
}
