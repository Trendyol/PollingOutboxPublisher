using System;

namespace PollingOutboxPublisher.Exceptions;

public class CouchbaseQueryFailedException(string errors) : Exception($"Couchbase query failed. Errors: {errors}");