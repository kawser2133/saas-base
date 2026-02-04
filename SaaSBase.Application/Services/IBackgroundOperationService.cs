using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace SaaSBase.Application.Services;

/// <summary>
/// Helper service for executing background operations with proper context
/// </summary>
public interface IBackgroundOperationService
{
    /// <summary>
    /// Execute an operation in background with organization context
    /// </summary>
    Task ExecuteWithContextAsync<TService>(
        Guid organizationId,
        Guid? userId,
        string? userName,
        Func<TService, Task> operation) where TService : class;

    /// <summary>
    /// Execute an operation in background with organization context and return result
    /// </summary>
    Task<TResult> ExecuteWithContextAsync<TService, TResult>(
        Guid organizationId,
        Guid? userId,
        string? userName,
        Func<TService, Task<TResult>> operation) where TService : class;
}

/// <summary>
/// Implementation of IBackgroundOperationService
/// </summary>
public class BackgroundOperationService : IBackgroundOperationService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public BackgroundOperationService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task ExecuteWithContextAsync<TService>(
        Guid organizationId,
        Guid? userId,
        string? userName,
        Func<TService, Task> operation) where TService : class
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

        // Set background context
        tenantService.SetBackgroundContext(organizationId, userId, userName);

        await operation(service);
    }

    public async Task<TResult> ExecuteWithContextAsync<TService, TResult>(
        Guid organizationId,
        Guid? userId,
        string? userName,
        Func<TService, Task<TResult>> operation) where TService : class
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

        // Set background context
        tenantService.SetBackgroundContext(organizationId, userId, userName);

        return await operation(service);
    }
}
