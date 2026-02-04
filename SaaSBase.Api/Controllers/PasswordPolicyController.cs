using System;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBase.Api.Attributes;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/password-policy")]
[ApiVersion("1.0")]
[Authorize]
public class PasswordPolicyController : ControllerBase
{
	private readonly IPasswordPolicyService _passwordPolicyService;

	public PasswordPolicyController(IPasswordPolicyService passwordPolicyService)
	{
		_passwordPolicyService = passwordPolicyService;
	}

	[HttpGet]
	[HasPermission("PasswordPolicy.Read")]
	public async Task<IActionResult> GetPasswordPolicy()
	{
		var policy = await _passwordPolicyService.GetPasswordPolicyAsync();
		return Ok(policy);
	}

	[HttpPut]
	[HasPermission("PasswordPolicy.Update")]
	public async Task<IActionResult> UpdatePasswordPolicy([FromBody] UpdatePasswordPolicyDto dto)
	{
		var policy = await _passwordPolicyService.UpdatePasswordPolicyAsync(dto);
		return Ok(policy);
	}

	[HttpPost("validate")]
	[HasPermission("PasswordPolicy.Read")]
	public async Task<IActionResult> ValidatePassword([FromBody] ValidatePasswordRequest request)
	{
		var result = await _passwordPolicyService.ValidatePasswordAsync(request.Password, request.UserId);
		return Ok(result);
	}

	[HttpGet("expired/{userId}")]
	[HasPermission("PasswordPolicy.Read")]
	public async Task<IActionResult> IsPasswordExpired(Guid userId)
	{
		var isExpired = await _passwordPolicyService.IsPasswordExpiredAsync(userId);
		return Ok(new { isExpired });
	}

	[HttpPost("check-history")]
	[HasPermission("PasswordPolicy.Read")]
	public async Task<IActionResult> CheckPasswordHistory([FromBody] CheckPasswordHistoryRequest request)
	{
		var isInHistory = await _passwordPolicyService.IsPasswordInHistoryAsync(request.UserId, request.Password);
		return Ok(new { isInHistory });
	}

	[HttpPost("check-complexity")]
	[HasPermission("PasswordPolicy.Read")]
	public async Task<IActionResult> CheckPasswordComplexity([FromBody] CheckPasswordComplexityRequest request)
	{
		var isValid = await _passwordPolicyService.CheckPasswordComplexityAsync(request.Password);
		return Ok(new { isValid });
	}

	[HttpPost("check-common")]
	[HasPermission("PasswordPolicy.Read")]
	public async Task<IActionResult> CheckCommonPasswords([FromBody] CheckCommonPasswordsRequest request)
	{
		var isCommon = await _passwordPolicyService.CheckCommonPasswordsAsync(request.Password);
		return Ok(new { isCommon });
	}

	[HttpPost("check-user-info")]
	[HasPermission("PasswordPolicy.Read")]
	public async Task<IActionResult> CheckUserInfoInPassword([FromBody] CheckUserInfoInPasswordRequest request)
	{
		var containsUserInfo = await _passwordPolicyService.CheckUserInfoInPasswordAsync(request.UserId, request.Password);
		return Ok(new { containsUserInfo });
	}
}

public class ValidatePasswordRequest
{
	public string Password { get; set; } = string.Empty;
	public Guid? UserId { get; set; }
}

public class CheckPasswordHistoryRequest
{
	public Guid UserId { get; set; }
	public string Password { get; set; } = string.Empty;
}

public class CheckPasswordComplexityRequest
{
	public string Password { get; set; } = string.Empty;
}

public class CheckCommonPasswordsRequest
{
	public string Password { get; set; } = string.Empty;
}

public class CheckUserInfoInPasswordRequest
{
	public Guid UserId { get; set; }
	public string Password { get; set; } = string.Empty;
}
