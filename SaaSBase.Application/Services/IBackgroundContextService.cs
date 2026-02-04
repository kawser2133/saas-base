using System;

namespace SaaSBase.Application.Services;

/// <summary>
/// Service for managing context in background operations
/// Solves the issue where HttpContext is not available in background tasks
/// </summary>
public interface IBackgroundContextService
{
    /// <summary>
    /// Set the organization context for background operations
    /// </summary>
    void SetOrganizationContext(Guid organizationId, Guid? userId = null, string? userName = null);

    /// <summary>
    /// Get the organization ID for background operations
    /// </summary>
    Guid GetOrganizationId();

    /// <summary>
    /// Get the user ID for background operations
    /// </summary>
    Guid GetUserId();

    /// <summary>
    /// Get the user name for background operations
    /// </summary>
    string GetUserName();

    /// <summary>
    /// Clear the background context
    /// </summary>
    void ClearContext();

    /// <summary>
    /// Execute an action with specific organization context
    /// </summary>
    Task<T> ExecuteWithContextAsync<T>(Guid organizationId, Guid? userId, string? userName, Func<Task<T>> action);

    /// <summary>
    /// Execute an action with specific organization context (void return)
    /// </summary>
    Task ExecuteWithContextAsync(Guid organizationId, Guid? userId, string? userName, Func<Task> action);
}
