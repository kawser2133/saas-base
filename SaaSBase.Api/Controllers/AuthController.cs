using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
	private readonly IAuthService _authService;
	private readonly ISessionService _sessionService;
	private readonly IMfaService _mfaService;

	public AuthController(IAuthService authService, ISessionService sessionService, IMfaService mfaService)
	{
		_authService = authService;
		_sessionService = sessionService;
		_mfaService = mfaService;
	}


	[AllowAnonymous]
	[HttpPost("register")]
	[EnableRateLimiting("AuthPolicy")]
	public async Task<IActionResult> Register([FromBody] RegisterDto dto)
	{
		try
		{
			var result = await _authService.RegisterAsync(dto);
			return Ok(result);
		}
		catch (ArgumentException ex)
		{
			return Conflict(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status409Conflict,
				Title = "Registration Failed",
				Detail = ex.Message
			});
		}
		catch (Exception ex)
		{
			return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Bad Request",
				Detail = ex.Message
			});
		}
	}

	[AllowAnonymous]
	[HttpPost("login")]
	[EnableRateLimiting("AuthPolicy")]
	public async Task<IActionResult> Login([FromBody] LoginDto dto)
	{
		try
		{
			var result = await _authService.LoginAsync(dto);
			return Ok(result);
		}
		catch (UnauthorizedAccessException ex)
		{
			// Return Unauthorized with exception message in ProblemDetails format
			// This ensures the message is included in the 'detail' field
			return Unauthorized(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status401Unauthorized,
				Title = "Unauthorized",
				Detail = ex.Message,
				Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
			});
		}
	}

	[AllowAnonymous]
	[HttpPost("send-mfa-code-login")]
	[EnableRateLimiting("AuthPolicy")]
	public async Task<IActionResult> SendMfaCodeLogin([FromBody] SendMfaCodeLoginDto dto)
	{
		try
		{
			var success = await _mfaService.SendMfaCodeAsync(dto.UserId, dto.MfaType);
			if (!success)
			{
				return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
				{
					Status = StatusCodes.Status400BadRequest,
					Title = "Failed to Send Code",
					Detail = "Unable to send verification code. Please check your configuration or try again later."
				});
			}
			return Ok(new { success = true });
		}
		catch (ArgumentException ex)
		{
			return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Bad Request",
				Detail = ex.Message
			});
		}
		catch (Exception ex)
		{
			return StatusCode(StatusCodes.Status500InternalServerError, new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status500InternalServerError,
				Title = "Internal Server Error",
				Detail = "An error occurred while sending the verification code. Please try again later."
			});
		}
	}

	[AllowAnonymous]
	[HttpPost("verify-mfa-login")]
	[EnableRateLimiting("AuthPolicy")]
	public async Task<IActionResult> VerifyMfaLogin([FromBody] VerifyMfaLoginDto dto)
	{
		try
		{
			var result = await _authService.CompleteLoginWithMfaAsync(dto);
			return Ok(result);
		}
		catch (UnauthorizedAccessException ex)
		{
			return Unauthorized(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status401Unauthorized,
				Title = "Unauthorized",
				Detail = ex.Message,
				Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
			});
		}
	}

	[AllowAnonymous]
	[HttpPost("refresh")]
	public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
	{
		try
		{
			var result = await _authService.RefreshTokenAsync(dto);
			return Ok(result);
		}
		catch (UnauthorizedAccessException)
		{
			return Unauthorized();
		}
	}

	[AllowAnonymous]
	[HttpPost("forgot-password")]
	public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
	{
		await _authService.ForgotPasswordAsync(dto);
		return Ok(new { message = "Password reset email sent" });
	}

	[AllowAnonymous]
	[HttpPost("reset-password")]
	public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
	{
		try
		{
			var success = await _authService.ResetPasswordAsync(dto);
			if (!success) return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Bad Request",
				Detail = "Invalid or expired token"
			});
			return Ok(new { message = "Password reset successfully" });
		}
		catch (ArgumentException ex)
		{
			// Return BadRequest with exception message in ProblemDetails format
			// This ensures the message is included in the 'detail' field
			return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Bad Request",
				Detail = ex.Message
			});
		}
	}

	[AllowAnonymous]
	[HttpPost("send-email-verification")]
	public async Task<IActionResult> SendEmailVerification([FromBody] SendEmailVerificationDto dto)
	{
		var success = await _authService.SendEmailVerificationAsync(dto.UserId);
		if (!success) return BadRequest("Unable to send verification email");
		return Ok(new { message = "Verification email sent" });
	}

	[AllowAnonymous]
	[HttpPost("verify-email")]
	public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
	{
		var success = await _authService.VerifyEmailAsync(dto.Token);
		if (!success) return BadRequest("Invalid or expired token");
		return Ok(new { message = "Email verified successfully" });
	}

	[HttpPost("change-password")]
	public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
	{
		try
		{
			var ok = await _authService.ChangePasswordAsync(dto);
			return ok ? Ok(new { message = "Password changed successfully" }) : BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Bad Request",
				Detail = "Unable to change password"
			});
		}
		catch (ArgumentException ex)
		{
			// Return BadRequest with exception message in ProblemDetails format
			// This ensures the message is included in the 'detail' field
			// Note: Changed from UnauthorizedAccessException to ArgumentException
			// to prevent logout on wrong current password (validation error, not auth failure)
			return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Bad Request",
				Detail = ex.Message
			});
		}
	}

	[HttpPost("logout")]
	[Authorize]
	public async Task<IActionResult> Logout()
	{
		// Get session ID from JWT claims
		var sessionId = User.FindFirst("sessionId")?.Value
			?? User.FindFirst("sid")?.Value;

		if (!string.IsNullOrEmpty(sessionId))
		{
			// Revoke the current session
			var success = await _sessionService.RevokeSessionAsync(sessionId);
			if (success)
			{
				return Ok(new { message = "Logged out successfully" });
			}
		}

		// Even if session ID not found or revoke failed, return success
		// to allow frontend to clear local storage
		return Ok(new { message = "Logged out successfully" });
	}
}
