using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface IPasswordPolicyService
{
	Task<PasswordPolicyDto> GetPasswordPolicyAsync(Guid? organizationId = null);
	Task<PasswordPolicyDto> UpdatePasswordPolicyAsync(UpdatePasswordPolicyDto dto);
	Task<PasswordValidationResult> ValidatePasswordAsync(string password, Guid? userId = null, Guid? organizationId = null);
	Task<bool> IsPasswordExpiredAsync(Guid userId);
	Task<bool> IsPasswordInHistoryAsync(Guid userId, string password);
	Task<List<string>> GetPasswordHistoryAsync(Guid userId);
	Task<bool> CheckPasswordComplexityAsync(string password);
	Task<bool> CheckPasswordHistoryAsync(Guid userId, string password, int historyCount);
	Task<bool> CheckCommonPasswordsAsync(string password);
	Task<bool> CheckUserInfoInPasswordAsync(Guid userId, string password);
	Task<bool> ResetFailedAttemptsAsync(Guid userId);
}
