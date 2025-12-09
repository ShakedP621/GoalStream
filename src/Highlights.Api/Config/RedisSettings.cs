using System;

namespace Highlights.Api.Config
{
    // Tiny settings class for Redis so we can bind it from configuration.
    public class RedisSettings
    {
        // Full StackExchange.Redis connection string.
        // For our docker setup this will usually look like: "redis:6379,abortConnect=false".
        public string ConnectionString { get; set; } = string.Empty;
    }
}