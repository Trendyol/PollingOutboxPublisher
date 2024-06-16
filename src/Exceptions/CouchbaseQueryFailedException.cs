using System;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Exceptions;

[ExcludeFromCodeCoverage]
public class CouchbaseQueryFailedException(string errors) : Exception($"Couchbase query failed. Errors: {errors}");