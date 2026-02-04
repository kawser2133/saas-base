using SaaSBase.Application;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Collections.Generic;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SaaSBase.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly INotificationTemplateService _templateService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public EmailService(
        ILogger<EmailService> logger, 
        IConfiguration configuration,
        INotificationTemplateService templateService,
        ICurrentTenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _configuration = configuration;
        _templateService = templateService;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            // Get email integration settings from database
            var emailSettings = await GetEmailIntegrationSettingsAsync();
            if (emailSettings == null)
            {
                _logger.LogError("Email integration settings not found. Please configure email provider in Integration Settings.");
                return false;
            }

            // Create SendGrid client dynamically from database API key
            var sendGridClient = new SendGridClient(emailSettings.ApiKey);

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(emailSettings.FromEmail, emailSettings.FromName),
                Subject = subject,
                PlainTextContent = isHtml ? null : body,
                HtmlContent = isHtml ? body : null
            };

            msg.AddTo(new EmailAddress(to));

            var response = await sendGridClient.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {Email}: {Subject}", to, subject);
                return true;
            }
            else
            {
                _logger.LogError("Failed to send email to {Email}. Status: {StatusCode}", to, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            return false;
        }
    }

    private async Task<EmailIntegrationSettings?> GetEmailIntegrationSettingsAsync()
    {
        try
        {
            var orgId = _tenantService.GetOrganizationId();
            if (orgId == Guid.Empty)
            {
                _logger.LogWarning("Organization ID not found, falling back to configuration");
                return GetEmailSettingsFromConfiguration();
            }

            var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
            
            // Find active and enabled email integration setting
            var emailIntegration = await integrationRepo.FindAsync(
                i => i.OrganizationId == orgId && 
                i.IntegrationType == "EMAIL" && 
                i.IsActive && 
                i.IsEnabled && 
                !i.IsDeleted);

            if (emailIntegration == null || string.IsNullOrEmpty(emailIntegration.Configuration))
            {
                _logger.LogWarning("Email integration setting not found in database, falling back to configuration");
                return GetEmailSettingsFromConfiguration();
            }

            // Parse configuration JSON
            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(emailIntegration.Configuration);
                if (config == null)
                {
                    _logger.LogWarning("Failed to parse email integration configuration JSON");
                    return GetEmailSettingsFromConfiguration();
                }

                var apiKey = config.ContainsKey("ApiKey") ? config["ApiKey"].GetString() : null;
                var fromEmail = config.ContainsKey("FromEmail") ? config["FromEmail"].GetString() : null;
                var fromName = config.ContainsKey("FromName") ? config["FromName"].GetString() : null;

                // Validate required fields - expect real values from database
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("API key not found in email integration settings, falling back to configuration");
                    return GetEmailSettingsFromConfiguration();
                }

                // If API key looks like a placeholder (for seed data compatibility), try to resolve from config
                if (apiKey.StartsWith("{{") && apiKey.EndsWith("}}"))
                {
                    _logger.LogWarning("Placeholder detected in API key. Please update Integration Settings with real API key value.");
                    var configKey = apiKey.Trim('{', '}');
                    apiKey = _configuration[configKey] ?? _configuration["SendGrid:ApiKey"];
                    
                    if (string.IsNullOrEmpty(apiKey) || apiKey == "dummy-key")
                    {
                        _logger.LogError("Cannot resolve API key from placeholder. Please configure real API key in Integration Settings.");
                        return null;
                    }
                }

                return new EmailIntegrationSettings
                {
                    ApiKey = apiKey,
                    FromEmail = fromEmail ?? _configuration["SendGrid:FromEmail"] ?? "noreply@SaaSBase.com",
                    FromName = fromName ?? _configuration["SendGrid:FromName"] ?? "SaaS Base"
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing email integration configuration JSON");
                return GetEmailSettingsFromConfiguration();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email integration settings from database");
            return GetEmailSettingsFromConfiguration();
        }
    }

    private EmailIntegrationSettings GetEmailSettingsFromConfiguration()
    {
        // Fallback to appsettings.json if database settings not available
        return new EmailIntegrationSettings
        {
            ApiKey = _configuration["SendGrid:ApiKey"] ?? "dummy-key",
            FromEmail = _configuration["SendGrid:FromEmail"] ?? "noreply@SaaSBase.com",
            FromName = _configuration["SendGrid:FromName"] ?? "SaaS Base"
        };
    }

    private class EmailIntegrationSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }

    public async Task<bool> SendEmailVerificationAsync(string email, string verificationToken)
    {
        try
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
            var verificationUrl = $"{baseUrl}/verify-email?token={verificationToken}";
            
            // Try to get user name from database if available
            var userName = await GetUserNameByEmailAsync(email) ?? email.Split('@')[0];
            
            var variables = new Dictionary<string, string>
            {
                { "UserName", userName },
                { "UserEmail", email },
                { "VerificationUrl", verificationUrl }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("Email Verification", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'Email Verification' not found, using fallback");
                return await SendEmailAsync(email, "Verify Your Email", 
                    $"<p>Please verify your email by clicking: <a href='{verificationUrl}'>Verify Email</a></p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email verification to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken)
    {
        try
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
            var resetUrl = $"{baseUrl}/reset-password?token={resetToken}";
            
            // Try to get user name from database if available
            var userName = await GetUserNameByEmailAsync(email) ?? email.Split('@')[0];
            
            var variables = new Dictionary<string, string>
            {
                { "UserName", userName },
                { "UserEmail", email },
                { "ResetUrl", resetUrl },
                { "ExpiryTime", "24 hours" }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("Password Reset", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'Password Reset' not found, using fallback");
                return await SendEmailAsync(email, "Password Reset", 
                    $"<p>Reset your password by clicking: <a href='{resetUrl}'>Reset Password</a></p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendMfaCodeEmailAsync(string email, string code)
    {
        try
        {
            // Try to get user name from database if available
            var userName = await GetUserNameByEmailAsync(email) ?? email.Split('@')[0];
            
            var variables = new Dictionary<string, string>
            {
                { "UserName", userName },
                { "UserEmail", email },
                { "MfaCode", code }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("MFA Code", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'MFA Code' not found, using fallback");
                return await SendEmailAsync(email, "Your MFA Verification Code", 
                    $"<p>Your verification code is: <strong>{code}</strong></p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending MFA code email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendAccountPasswordEmailAsync(string email, string fullName, string password)
    {
        try
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
            var loginUrl = $"{baseUrl}/login";
            
            var variables = new Dictionary<string, string>
            {
                { "UserName", fullName },
                { "UserEmail", email },
                { "TemporaryPassword", password },
                { "LoginUrl", loginUrl }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("Account Password", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'Account Password' not found, using fallback");
                return await SendEmailAsync(email, "Your Account Password", 
                    $"<p>Hi {fullName}, your temporary password is: <strong>{password}</strong></p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending account password email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string email, string fullName)
    {
        try
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
            var loginUrl = $"{baseUrl}/login";
            
            var variables = new Dictionary<string, string>
            {
                { "UserName", fullName },
                { "UserEmail", email },
                { "LoginUrl", loginUrl }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("Welcome Email", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'Welcome Email' not found, using fallback");
                return await SendEmailAsync(email, "Welcome", 
                    $"<p>Hi {fullName}, welcome to our system!</p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendInvitationEmailAsync(string email, string fullName)
    {
        try
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
            var invitationUrl = $"{baseUrl}/setup-account";
            
            var variables = new Dictionary<string, string>
            {
                { "UserName", fullName },
                { "UserEmail", email },
                { "InvitationUrl", invitationUrl }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("Invitation Email", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'Invitation Email' not found, using fallback");
                return await SendEmailAsync(email, "You're Invited", 
                    $"<p>Hi {fullName}, you've been invited! <a href='{invitationUrl}'>Setup Account</a></p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invitation email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendAccountActivationEmailAsync(string email, string fullName)
    {
        try
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://yourapp.com";
            var loginUrl = $"{baseUrl}/login";
            
            var variables = new Dictionary<string, string>
            {
                { "UserName", fullName },
                { "UserEmail", email },
                { "LoginUrl", loginUrl }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("Account Activation", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'Account Activation' not found, using fallback");
                return await SendEmailAsync(email, "Account Activated", 
                    $"<p>Hi {fullName}, your account has been activated. You can now access the system.</p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending account activation email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendAccountDeactivationEmailAsync(string email, string fullName)
    {
        try
        {
            var variables = new Dictionary<string, string>
            {
                { "UserName", fullName },
                { "UserEmail", email },
                { "DeactivatedAt", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
            };

            var (subject, body) = await _templateService.RenderEmailTemplateAsync("Account Deactivated", variables);
            
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Template 'Account Deactivated' not found, using fallback");
                return await SendEmailAsync(email, "Account Deactivated", 
                    $"<p>Hi {fullName}, your account has been deactivated. Please contact support if you need assistance.</p>");
            }

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending account deactivation email to {Email}", email);
            return false;
        }
    }

    private async Task<string?> GetUserNameByEmailAsync(string email)
    {
        try
        {
            var orgId = _tenantService.GetOrganizationId();
            if (orgId == Guid.Empty) return null;

            var userRepo = _unitOfWork.Repository<User>();
            var user = await userRepo.FindAsync(u => u.Email == email && u.OrganizationId == orgId && !u.IsDeleted);
            return user?.FullName;
        }
        catch
        {
            return null;
        }
    }
}
