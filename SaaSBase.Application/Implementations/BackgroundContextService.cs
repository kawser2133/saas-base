using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace SaaSBase.Application.Services;

/// <summary>
/// Implementation of IBackgroundContextService using AsyncLocal for thread-safe context management
/// </summary>
public class BackgroundContextService : IBackgroundContextService
{
    private static readonly AsyncLocal<BackgroundContext?> _context = new();

    private class BackgroundContext
    {
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public void SetOrganizationContext(Guid organizationId, Guid? userId = null, string? userName = null)
    {
        _context.Value = new BackgroundContext
        {
            OrganizationId = organizationId,
            UserId = userId ?? Guid.Empty,
            UserName = userName ?? "System"
        };
    }

    public Guid GetOrganizationId()
    {
        return _context.Value?.OrganizationId ?? Guid.Empty;
    }

    public Guid GetUserId()
    {
        return _context.Value?.UserId ?? Guid.Empty;
    }

    public string GetUserName()
    {
        return _context.Value?.UserName ?? "System";
    }

    public void ClearContext()
    {
        _context.Value = null;
    }

    public async Task<T> ExecuteWithContextAsync<T>(Guid organizationId, Guid? userId, string? userName, Func<Task<T>> action)
    {
        var previousContext = _context.Value;
        try
        {
            SetOrganizationContext(organizationId, userId, userName);
            return await action();
        }
        finally
        {
            _context.Value = previousContext;
        }
    }

    public async Task ExecuteWithContextAsync(Guid organizationId, Guid? userId, string? userName, Func<Task> action)
    {
        var previousContext = _context.Value;
        try
        {
            SetOrganizationContext(organizationId, userId, userName);
            await action();
        }
        finally
        {
            _context.Value = previousContext;
        }
    }
}
