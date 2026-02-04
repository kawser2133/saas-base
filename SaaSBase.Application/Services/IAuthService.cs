using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface IAuthService
{
	Task<RegisterResponseDto> RegisterAsync(RegisterDto dto);
	Task<AuthResponseDto> LoginAsync(LoginDto dto);
	Task<AuthResponseDto> CompleteLoginWithMfaAsync(VerifyMfaLoginDto dto);
	Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto dto);
	Task<bool> ForgotPasswordAsync(ForgotPasswordDto dto);
	Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
	Task<bool> SendEmailVerificationAsync(Guid userId);
	Task<bool> VerifyEmailAsync(string token);
    Task<bool> ChangePasswordAsync(ChangePasswordDto dto);
}
