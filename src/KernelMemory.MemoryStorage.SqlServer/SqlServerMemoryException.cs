// Copyright (c) Kevin BEAUGRAND. All rights reserved.

using Microsoft.KernelMemory;
using System;

namespace KernelMemory.MemoryStorage.SqlServer;

/// <summary>
/// Represents a SQL Server memory store exception.
/// </summary>
public class SqlServerMemoryException : KernelMemoryException
{
    /// <inheritdoc />
    public SqlServerMemoryException()
    {
    }

    /// <inheritdoc />
    public SqlServerMemoryException(string? message) : base(message)
    {
    }

    /// <inheritdoc />
    public SqlServerMemoryException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
