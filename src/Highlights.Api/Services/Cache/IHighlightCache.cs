using System;
using System.Threading;
using System.Threading.Tasks;
using Highlights.Api.Dtos;

namespace Highlights.Api.Services.Cache
{
    // Very small caching abstraction so the rest of the app doesn't need
    // to know anything about Redis directly.
    public interface IHighlightCache
    {
        // Grab a single highlight from the cache by id.
        // Returns null when it's just not there.
        Task<HighlightDto?> GetHighlightAsync(Guid id, CancellationToken cancellationToken = default);

        // Store or update a highlight in the cache with a simple TTL.
        Task SetHighlightAsync(HighlightDto highlight, TimeSpan ttl, CancellationToken cancellationToken = default);

        // Remove a highlight from the cache (e.g. if it's been updated or deleted).
        Task RemoveHighlightAsync(Guid id, CancellationToken cancellationToken = default);
    }
}