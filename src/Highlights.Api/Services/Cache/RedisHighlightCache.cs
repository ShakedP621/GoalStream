using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Highlights.Api.Dtos;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Highlights.Api.Services.Cache
{
    // Redis implementation of IHighlightCache.
    // This is intentionally tiny and boring – just basic get/set/delete by id.
    public class RedisHighlightCache : IHighlightCache
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly ILogger<RedisHighlightCache> _logger;
        private readonly IDatabase _db;

        public RedisHighlightCache(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisHighlightCache> logger)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _logger = logger;
            _db = _connectionMultiplexer.GetDatabase();
        }

        private static string GetHighlightKey(Guid id) => $"highlight:{id}";

        public async Task<HighlightDto?> GetHighlightAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // Redis APIs don't take a CancellationToken, so we just bail early if requested.
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var key = GetHighlightKey(id);

            try
            {
                var value = await _db.StringGetAsync(key).ConfigureAwait(false);
                if (!value.HasValue)
                {
                    return null;
                }

                var json = (string)value!;
                var dto = JsonSerializer.Deserialize<HighlightDto>(json);

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading highlight {HighlightId} from Redis. Treating as cache miss.", id);
                return null;
            }
        }

        public async Task SetHighlightAsync(HighlightDto highlight, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var key = GetHighlightKey(highlight.Id);

            try
            {
                var json = JsonSerializer.Serialize(highlight);
                await _db.StringSetAsync(key, json, ttl).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error writing highlight {HighlightId} to Redis. Ignoring and falling back to DB next time.",
                    highlight.Id
                );
            }
        }

        public async Task RemoveHighlightAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var key = GetHighlightKey(id);

            try
            {
                await _db.KeyDeleteAsync(key).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing highlight {HighlightId} from Redis cache. Ignoring.", id);
            }
        }
    }
}