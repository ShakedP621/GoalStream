using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Highlights.Api.Services.Cache
{
    // This guy is our concrete Redis-backed implementation of IHighlightCache.
    public class RedisHighlightCache : IHighlightCache
    {
        private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions =
            new(System.Text.Json.JsonSerializerDefaults.Web);

        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public RedisHighlightCache(IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        public async Task<T?> GetAsync<T>(
     string key,
     CancellationToken cancellationToken = default)
        {
            var db = _connectionMultiplexer.GetDatabase();

            // Redis client doesn't use CancellationToken directly, but we keep it in the signature
            // so our abstraction stays friendly and future-proof.
            var value = await db.StringGetAsync(key).ConfigureAwait(false);

            if (value.IsNullOrEmpty)
            {
                return default;
            }

            // Be explicit: treat the Redis value as a JSON string.
            var json = value.ToString();

            return System.Text.Json.JsonSerializer.Deserialize<T>(
                json,
                SerializerOptions);
        }


        public async Task SetAsync<T>(
            string key,
            T value,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            var db = _connectionMultiplexer.GetDatabase();

            var json = System.Text.Json.JsonSerializer.Serialize(
                value,
                SerializerOptions);

            // This is a best-effort "set with TTL". If Redis is down, callers catch/log upstream.
            await db.StringSetAsync(key, json, ttl).ConfigureAwait(false);
        }
    }
}
