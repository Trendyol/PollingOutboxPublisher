using System;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Exceptions;

[ExcludeFromCodeCoverage]
public class UnsupportedDatabaseTypeException(string databaseType)
    : Exception($"Unsupported database type: {databaseType}");