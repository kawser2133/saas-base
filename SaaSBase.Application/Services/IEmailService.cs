using System.Threading.Tasks;

namespace SaaSBase.Application.Services;

public interface IEmailService
{
	Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
	Task<bool> SendPasswordResetEmailAsync(string email, string resetToken);
	Task<bool> SendMfaCodeEmailAsync(string email, string code);
    Task<bool> SendEmailVerificationAsync(string email, string verificationToken);
    Task<bool> SendAccountPasswordEmailAsync(string email, string fullName, string password);
    Task<bool> SendWelcomeEmailAsync(string email, string fullName);
    Task<bool> SendInvitationEmailAsync(string email, string fullName);
    Task<bool> SendAccountActivationEmailAsync(string email, string fullName);
    Task<bool> SendAccountDeactivationEmailAsync(string email, string fullName);
}
