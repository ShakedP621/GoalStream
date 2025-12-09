using System;
using System.Threading;
using System.Threading.Tasks;

namespace Highlights.Api.Services.Cache
{
    // Tiny cache abstraction so the rest of the app doesn't need to know about Redis directly.
    public interface IHighlightCache
    {
        // Try to get a value for a key. Returns null/default if the key is missing.
        Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        // Store a value under a key with a given TTL.
        Task SetAsync<T>(
            string key,
            T value,
            TimeSpan ttl,
            CancellationToken cancellationToken = default);
    }
}
