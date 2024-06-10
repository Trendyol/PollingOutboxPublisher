using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace PollingOutboxPublisher.Extensions;

[ExcludeFromCodeCoverage]
public static class DapperPollyExtensions
{
    private static IEnumerable<TimeSpan> RetryTimes =
        Backoff.ConstantBackoff(TimeSpan.FromMilliseconds(100), retryCount: 3);

    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<SqlException>(SqlServerTransientExceptionDetector.ShouldRetryOn)
        .Or<TimeoutException>()
        .OrInner<Win32Exception>(SqlServerTransientExceptionDetector.ShouldRetryOn)
        .WaitAndRetryAsync(RetryTimes,
            (exception, timeSpan, retryCount, context) =>
            {
                var logger = (ILogger)context["logger"];
                logger.LogInformation(
                    exception,
                    "WARNING: Error talking to Sql DB, will retry after {RetryTimeSpan}. Retry attempt {RetryCount}",
                    timeSpan,
                    retryCount
                );
            });

    public static async Task<int> ExecuteAsyncWithRetry(this IDbConnection cnn, string sql, ILogger logger,
        object param = null,
        IDbTransaction transaction = null, int? commandTimeout = null,
        CommandType? commandType = null)
    {
        return await RetryPolicy.ExecuteAsync(
            async (_) => await cnn.ExecuteAsync(sql, param, transaction, commandTimeout, commandType),
            new Dictionary<string, object>() { { "logger", logger } });
    }

    public static async Task<IEnumerable<T>> QueryAsyncWithRetry<T>(this IDbConnection cnn, string sql,
        ILogger logger,
        object param = null,
        IDbTransaction transaction = null, int? commandTimeout = null,
        CommandType? commandType = null) =>
        await RetryPolicy.ExecuteAsync(async (_) =>
                await cnn.QueryAsync<T>(sql, param, transaction, commandTimeout, commandType),
            new Dictionary<string, object>() { { "logger", logger } });
}