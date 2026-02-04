using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace SaaSBase.Application.Services;

/// <summary>
/// Generic service for caching any entity data to improve performance
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get cached data by key
    /// </summary>
    Task<T?> GetCachedAsync<T>(string key) where T : class;

    /// <summary>
    /// Set cache with expiration
    /// </summary>
    Task SetCacheAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Remove cache entry
    /// </summary>
    Task RemoveCacheAsync(string key);

    /// <summary>
    /// Remove cache entries by pattern
    /// </summary>
    Task RemoveCacheByPatternAsync(string pattern);

    /// <summary>
    /// Generate cache key for entity list query
    /// </summary>
    string GenerateListCacheKey(string entityType, Guid organizationId, int page, int pageSize, 
        string? search = null, string? sortField = null, string? sortDirection = null, 
        params object[] additionalParams);

    /// <summary>
    /// Generate cache key for dropdown options
    /// </summary>
    string GenerateDropdownCacheKey(string entityType, Guid organizationId);

    /// <summary>
    /// Generate cache key for statistics
    /// </summary>
    string GenerateStatsCacheKey(string entityType, Guid organizationId);

    /// <summary>
    /// Get the default cache expiration in minutes from configuration
    /// </summary>
    int GetCacheExpirationMinutes();
}
