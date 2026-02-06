using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.KeyValue;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Exceptions;

namespace PollingOutboxPublisher.Database.Providers;

[ExcludeFromCodeCoverage]
public class CouchbaseScopeProvider : ICouchbaseScopeProvider
{
    private readonly ConfigOptions.Couchbase _couchbase;
    private IScope _scope;

    public CouchbaseScopeProvider(IOptions<ConfigOptions.Couchbase> couchbaseOptions)
    {
        _couchbase = couchbaseOptions.Value;
    }

    public async Task<IScope> GetScopeAsync()
    {
        if (_scope != null) return _scope;

        if (_couchbase.Host == null ||
            _couchbase.Username == null ||
            _couchbase.Password == null ||
            _couchbase.Bucket == null ||
            _couchbase.Scope == null)
        {
            throw new MissingConfigurationException("CouchbaseOptions.Host, CouchbaseOptions.Username, CouchbaseOptions.Password, CouchbaseOptions.Bucket, CouchbaseOptions.Scope");
        }
        
        var cluster = await Cluster.ConnectAsync(
            _couchbase.Host,
            _couchbase.Username,
            _couchbase.Password);

        var bucket = await cluster.BucketAsync(_couchbase.Bucket);
        _scope = await bucket.ScopeAsync(_couchbase.Scope);
        return _scope;
    }
}