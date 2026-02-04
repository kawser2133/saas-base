using System;
using System.Threading.Tasks;
using SaaSBase.Application.Services;

namespace SaaSBase.Application.Examples;

/// <summary>
/// Example showing how to use the background context solution
/// </summary>
public class BackgroundContextExample
{
    private readonly IBackgroundOperationService _backgroundOperationService;
    private readonly ICurrentTenantService _tenantService;

    public BackgroundContextExample(
        IBackgroundOperationService backgroundOperationService,
        ICurrentTenantService tenantService)
    {
        _backgroundOperationService = backgroundOperationService;
        _tenantService = tenantService;
    }

    /// <summary>
    /// Example: Start a background import job with proper organization context
    /// </summary>
    public async Task<string> StartImportJobWithContextAsync(Stream fileStream, string fileName, Guid organizationId, Guid userId, string userName)
    {
        // Capture context from HTTP request
        var currentOrgId = _tenantService.GetOrganizationId();
        var currentUserId = _tenantService.GetCurrentUserId();
        var currentUserName = _tenantService.GetCurrentUserName();

        // Start background task with proper context
        _ = Task.Run(async () =>
        {
            await _backgroundOperationService.ExecuteWithContextAsync<IImportExportService>(
                organizationId,
                userId,
                userName,
                async (importService) =>
                {
                    // Now GetOrganizationId() will return the correct organization ID
                    // even in background context
                    var orgId = _tenantService.GetOrganizationId(); // ✅ This will work!
                    
                    // Your import logic here
                    await ProcessImportFileAsync(importService, fileStream, fileName);
                });
        });

        return "Job started";
    }

    /// <summary>
    /// Example: Background service that needs organization context
    /// </summary>
    public async Task ProcessScheduledTaskAsync(Guid organizationId, Guid userId, string userName)
    {
        await _backgroundOperationService.ExecuteWithContextAsync<IUserService>(
            organizationId,
            userId,
            userName,
            async (userService) =>
            {
                // GetOrganizationId() will work here because context is set
                var orgId = _tenantService.GetOrganizationId(); // ✅ This will work!
                
                // Your background processing logic here
                await ProcessUsersAsync(userService);
            });
    }

    private async Task ProcessImportFileAsync(IImportExportService importService, Stream fileStream, string fileName)
    {
        // Your import processing logic
        await Task.CompletedTask;
    }

    private async Task ProcessUsersAsync(IUserService userService)
    {
        // Your user processing logic
        await Task.CompletedTask;
    }
}
