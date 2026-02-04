using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaaSBase.Application.Services;

public interface INotificationTemplateService
{
	Task<string?> RenderTemplateAsync(string templateName, Dictionary<string, string> variables, Guid? organizationId = null);
	Task<(string Subject, string Body)> RenderEmailTemplateAsync(string templateName, Dictionary<string, string> variables, Guid? organizationId = null);
	Task<Dictionary<string, string>> GetOrganizationVariablesAsync(Guid organizationId);
}

