using SaaSBase.Application.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;

namespace SaaSBase.Application.Implementations
{

    /// <summary>
    /// Simplified cache service using only Redis distributed cache
    /// Memory cache removed to prevent memory leaks and application hanging
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer? _redis;
        private readonly int _cacheExpirationMinutes;
        private readonly TimeSpan _defaultExpiration;

        // Track cache keys for pattern-based deletion (fallback if Redis connection not available)
        private static readonly HashSet<string> _cacheKeys = new HashSet<string>();
        private static readonly object _lockObject = new object();

        public CacheService(IDistributedCache distributedCache, IConfiguration configuration, IConnectionMultiplexer? redis = null)
        {
            _distributedCache = distributedCache;
            var configValue = configuration["AppSettings:CacheExpirationMinutes"];
            _cacheExpirationMinutes = int.TryParse(configValue, out var minutes) ? minutes : 30;
            _defaultExpiration = TimeSpan.FromMinutes(_cacheExpirationMinutes);

            // Use injected Redis connection if available, otherwise try to create one
            if (redis != null)
            {
                _redis = redis;
            }
            else
            {
                try
                {
                    var redisConnectionString = configuration.GetConnectionString("Redis");
                    if (!string.IsNullOrEmpty(redisConnectionString))
                    {
                        _redis = ConnectionMultiplexer.Connect(redisConnectionString);
                    }
                }
                catch
                {
                    // If Redis connection fails, fall back to HashSet tracking
                    _redis = null;
                }
            }
        }

        /// <summary>
        /// Gets the configured cache expiration in minutes
        /// </summary>
        public int CacheExpirationMinutes => _cacheExpirationMinutes;

        public async Task<T?> GetCachedAsync<T>(string key) where T : class
        {
            // Get from Redis only (no memory cache layer)
            var cachedValue = await _distributedCache.GetStringAsync(key);

            if (cachedValue != null)
            {
                return JsonSerializer.Deserialize<T>(cachedValue);
            }

            return null;
        }

        public async Task SetCacheAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            var expiry = expiration ?? _defaultExpiration;

            // Track the cache key for pattern-based deletion
            lock (_lockObject)
            {
                _cacheKeys.Add(key);
            }

            // Store in Redis only
            var serializedValue = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            };

            await _distributedCache.SetStringAsync(key, serializedValue, options);
        }

        public async Task RemoveCacheAsync(string key)
        {
            // Remove from tracking
            lock (_lockObject)
            {
                _cacheKeys.Remove(key);
            }

            // Remove from Redis only
            await _distributedCache.RemoveAsync(key);
        }

        public async Task RemoveCacheByPatternAsync(string pattern)
        {
            List<string> keysToRemove = new List<string>();

            // Use Redis SCAN if available (more reliable for distributed scenarios)
            if (_redis != null && _redis.IsConnected)
            {
                try
                {
                    var endpoints = _redis.GetEndPoints();
                    if (endpoints != null && endpoints.Length > 0)
                    {
                        // Convert pattern to Redis pattern format
                        // Handle wildcard patterns: "departments:list:*" or "user_permissions_*_{orgId}"
                        var redisPattern = pattern;

                        // If pattern doesn't contain *, treat as prefix and add *
                        if (!pattern.Contains("*"))
                        {
                            redisPattern = pattern + "*";
                        }

                        // Scan Redis for matching keys (use SCAN to avoid blocking)
                        var keys = new List<RedisKey>();
                        foreach (var endpoint in endpoints)
                        {
                            var endpointServer = _redis.GetServer(endpoint);
                            if (endpointServer != null && endpointServer.IsConnected)
                            {
                                // Use Keys() with pattern matching - this uses SCAN internally
                                var matchedKeys = endpointServer.Keys(pattern: redisPattern, pageSize: 1000);
                                keys.AddRange(matchedKeys);
                            }
                        }

                        // Remove duplicates and convert to strings
                        keysToRemove = keys.Distinct().Select(k => k.ToString()).ToList();
                    }
                }
                catch (Exception)
                {
                    // Fall back to HashSet if Redis scan fails
                    keysToRemove = GetKeysFromHashSet(pattern);
                }
            }
            else
            {
                // Fallback to HashSet tracking if Redis connection not available
                keysToRemove = GetKeysFromHashSet(pattern);
            }

            // Remove matched keys from Redis
            var removeTasks = keysToRemove.Select(async key =>
            {
                await _distributedCache.RemoveAsync(key);
                
                // Also remove from tracking HashSet
                lock (_lockObject)
                {
                    _cacheKeys.Remove(key);
                }
            });

            await Task.WhenAll(removeTasks);
        }

        private List<string> GetKeysFromHashSet(string pattern)
        {
            List<string> keysToRemove;

            lock (_lockObject)
            {
                // Handle wildcard patterns like "user_permissions_*_{organizationId}"
                if (pattern.Contains("*"))
                {
                    var parts = pattern.Split('*');
                    var prefix = parts[0];
                    var suffix = parts.Length > 1 ? parts[1] : "";

                    keysToRemove = _cacheKeys.Where(k =>
                    {
                        // Check if key starts with prefix and ends with suffix
                        if (!string.IsNullOrEmpty(suffix))
                        {
                            return k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                                   k.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
                        }
                        // If no suffix, just check prefix
                        return k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }
                else
                {
                    // Simple prefix matching for patterns without wildcards
                    keysToRemove = _cacheKeys.Where(k => k.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            return keysToRemove;
        }

        public string GenerateListCacheKey(string entityType, Guid organizationId, int page, int pageSize,
            string? search = null, string? sortField = null, string? sortDirection = null,
            params object[] additionalParams)
        {
            var keyParts = new List<string>
        {
            entityType.ToLower(),
            "list",
            organizationId.ToString(),
            page.ToString(),
            pageSize.ToString(),
            search ?? "null",
            sortField ?? "null",
            sortDirection ?? "null"
        };

            // Add additional parameters
            foreach (var param in additionalParams)
            {
                keyParts.Add(param?.ToString() ?? "null");
            }

            return string.Join(":", keyParts);
        }

        public string GenerateDropdownCacheKey(string entityType, Guid organizationId)
        {
            return $"{entityType.ToLower()}:dropdown:{organizationId}";
        }

        public string GenerateStatsCacheKey(string entityType, Guid organizationId)
        {
            return $"{entityType.ToLower()}:stats:{organizationId}";
        }

        public int GetCacheExpirationMinutes()
        {
            return _cacheExpirationMinutes;
        }
    }
}
