using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SaaSBase.Application;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Application.Implementations;

public class NotificationTemplateService : INotificationTemplateService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
	private readonly IConfiguration _configuration;
	private readonly ILogger<NotificationTemplateService> _logger;

	public NotificationTemplateService(
		IUnitOfWork unitOfWork,
		ICurrentTenantService tenantService,
		IConfiguration configuration,
		ILogger<NotificationTemplateService> logger)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_configuration = configuration;
		_logger = logger;
	}

	public async Task<string?> RenderTemplateAsync(string templateName, Dictionary<string, string> variables, Guid? organizationId = null)
	{
		var template = await GetTemplateByNameAsync(templateName, organizationId);
		if (template == null || !template.IsActive)
		{
			_logger.LogWarning("Template '{TemplateName}' not found or inactive", templateName);
			return null;
		}

		// Merge organization variables with provided variables
		var orgId = organizationId ?? _tenantService.GetOrganizationId();
		var orgVariables = await GetOrganizationVariablesAsync(orgId);
		var allVariables = new Dictionary<string, string>(orgVariables);
		foreach (var kvp in variables)
		{
			allVariables[kvp.Key] = kvp.Value;
		}

		return ReplaceVariables(template.Body, allVariables);
	}

	public async Task<(string Subject, string Body)> RenderEmailTemplateAsync(string templateName, Dictionary<string, string> variables, Guid? organizationId = null)
	{
		var template = await GetTemplateByNameAsync(templateName, organizationId);
		if (template == null || !template.IsActive)
		{
			_logger.LogWarning("Template '{TemplateName}' not found or inactive", templateName);
			return (string.Empty, string.Empty);
		}

		// Merge organization variables with provided variables
		var orgId = organizationId ?? _tenantService.GetOrganizationId();
		var orgVariables = await GetOrganizationVariablesAsync(orgId);
		var allVariables = new Dictionary<string, string>(orgVariables);
		foreach (var kvp in variables)
		{
			allVariables[kvp.Key] = kvp.Value;
		}

		var subject = ReplaceVariables(template.Subject, allVariables);
		var body = ReplaceVariables(template.Body, allVariables);
		
		// Wrap body in email shell with organization branding
		var htmlBody = await BuildEmailShellAsync(body, orgId, allVariables);

		return (subject, htmlBody);
	}

	public async Task<Dictionary<string, string>> GetOrganizationVariablesAsync(Guid organizationId)
	{
		var variables = new Dictionary<string, string>();
		
		try
		{
			var orgRepo = _unitOfWork.Repository<Organization>();
			var organization = await orgRepo.FindAsync(o => o.Id == organizationId && !o.IsDeleted);
			
			if (organization != null)
			{
				variables["OrganizationName"] = organization.Name ?? "SaaS Base";
				variables["OrganizationEmail"] = organization.Email ?? _configuration["SendGrid:FromEmail"] ?? "noreply@SaaSBase.com";
				variables["OrganizationWebsite"] = organization.Website ?? _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
				variables["OrganizationPhone"] = organization.Phone ?? string.Empty;
				variables["OrganizationAddress"] = organization.Address ?? string.Empty;
				variables["OrganizationCity"] = organization.City ?? string.Empty;
				variables["OrganizationState"] = organization.State ?? string.Empty;
				variables["OrganizationCountry"] = organization.Country ?? string.Empty;
				variables["OrganizationPostalCode"] = organization.PostalCode ?? string.Empty;
				
				// Get additional settings from BusinessSettings
				var businessSettingsRepo = _unitOfWork.Repository<BusinessSetting>();
				var settings = await businessSettingsRepo.FindManyAsync(
					bs => bs.OrganizationId == organizationId && bs.IsActive && !bs.IsDeleted);
				
				foreach (var setting in settings)
				{
					var key = $"Organization{setting.Key}";
					variables[key] = setting.Value ?? string.Empty;
				}
			}
			else
			{
				// Fallback to configuration values
				variables["OrganizationName"] = _configuration["SendGrid:FromName"] ?? "SaaS Base";
				variables["OrganizationEmail"] = _configuration["SendGrid:FromEmail"] ?? "noreply@SaaSBase.com";
				variables["OrganizationWebsite"] = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting organization variables for organization {OrganizationId}", organizationId);
			// Fallback to configuration values
			variables["OrganizationName"] = _configuration["SendGrid:FromName"] ?? "SaaS Base";
			variables["OrganizationEmail"] = _configuration["SendGrid:FromEmail"] ?? "noreply@SaaSBase.com";
			variables["OrganizationWebsite"] = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
		}

		// Add base URL and login URL
		var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
		variables["BaseUrl"] = baseUrl;
		variables["LoginUrl"] = $"{baseUrl}/login";

		return variables;
	}

	private async Task<NotificationTemplate?> GetTemplateByNameAsync(string templateName, Guid? organizationId = null)
	{
		var orgId = organizationId ?? _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		
		// First try to find organization-specific template
		var template = await templateRepo.FindAsync(
			t => t.Name == templateName && 
			t.OrganizationId == orgId && 
			t.IsActive && 
			!t.IsDeleted);
		
		// If not found, try to find system template (fallback)
		if (template == null)
		{
			template = await templateRepo.FindAsync(
				t => t.Name == templateName && 
				t.IsSystemTemplate && 
				t.IsActive && 
				!t.IsDeleted);
		}

		return template;
	}

	private string ReplaceVariables(string template, Dictionary<string, string> variables)
	{
		if (string.IsNullOrEmpty(template))
			return template;

		var result = template;
		foreach (var kvp in variables)
		{
			var placeholder = $"{{{{{kvp.Key}}}}}";
			result = result.Replace(placeholder, kvp.Value ?? string.Empty);
		}

		// Remove any remaining placeholders that weren't replaced
		result = Regex.Replace(result, @"\{\{(\w+)\}\}", string.Empty);

		return result;
	}

	private async Task<string> BuildEmailShellAsync(string bodyContent, Guid organizationId, Dictionary<string, string> variables)
	{
		var orgName = variables.GetValueOrDefault("OrganizationName", "SaaS Base");
		var orgWebsite = variables.GetValueOrDefault("OrganizationWebsite", _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com");
		var orgEmail = variables.GetValueOrDefault("OrganizationEmail", _configuration["SendGrid:FromEmail"] ?? "noreply@SaaSBase.com");
		var year = DateTime.UtcNow.Year;
		
		// Get backend base URL for media files (logo) from configuration
		var backendBaseUrl = _configuration["AppSettings:BackendUrl"] ?? "http://localhost:5091";
		
		// Get organization colors and logo dynamically from database
		var orgRepo = _unitOfWork.Repository<Organization>();
		var organization = await orgRepo.FindAsync(o => o.Id == organizationId && !o.IsDeleted);
		var primaryColor = organization?.PrimaryColor ?? "#2563eb";
		var logoUrl = organization?.LogoUrl;

		string? fullLogoUrl = null;
		if (!string.IsNullOrEmpty(logoUrl))
		{
			if (Uri.IsWellFormedUriString(logoUrl, UriKind.Absolute))
			{
				// Already a full URL
				fullLogoUrl = logoUrl;
			}
			else
			{
				// Relative path - convert to full URL
				var cleanPath = logoUrl.TrimStart('/', '\\').Replace("\\", "/");
				fullLogoUrl = $"{backendBaseUrl.TrimEnd('/')}/media/{cleanPath}";
			}
		}

		var logoHtml = !string.IsNullOrEmpty(fullLogoUrl) 
			? $"<img src='{fullLogoUrl}' alt='{orgName}' style='max-height:40px;margin-bottom:16px' />"
			: string.Empty;

		return $@"
<table width='100%' cellpadding='0' cellspacing='0' style='font-family:Segoe UI,Arial,sans-serif;background:#f5f7fb;color:#111827;padding:24px'>
  <tr><td align='center'>
    <table width='640' cellpadding='0' cellspacing='0' style='background:#ffffff;border:1px solid #e5e7eb;border-radius:12px'>
      <tr>
        <td style='padding:24px;border-bottom:1px solid #e5e7eb;text-align:center'>
          {logoHtml}
          <h2 style='margin:0;color:#111827'>{orgName}</h2>
        </td>
      </tr>
      <tr>
        <td style='padding:24px'>
          {bodyContent}
        </td>
      </tr>
      <tr>
        <td style='padding:16px 24px;border-top:1px solid #e5e7eb;color:#6b7280;font-size:12px;text-align:center'>
          © {year} {orgName} · <a href='{orgWebsite}' style='color:{primaryColor};text-decoration:none'>{orgWebsite}</a>
          {(!string.IsNullOrEmpty(orgEmail) ? $"<br>Contact: <a href='mailto:{orgEmail}' style='color:{primaryColor};text-decoration:none'>{orgEmail}</a>" : string.Empty)}
        </td>
      </tr>
    </table>
  </td></tr>
</table>";
	}
}

