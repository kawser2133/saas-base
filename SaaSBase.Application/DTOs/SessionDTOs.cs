using System;

namespace SaaSBase.Application.DTOs;

public class UserSessionDto
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public string? UserEmail { get; set; }
	public string SessionId { get; set; } = string.Empty;
	public string DeviceId { get; set; } = string.Empty;
	public string DeviceName { get; set; } = string.Empty;
	public string DeviceType { get; set; } = string.Empty;
	public string BrowserName { get; set; } = string.Empty;
	public string BrowserVersion { get; set; } = string.Empty;
	public string OperatingSystem { get; set; } = string.Empty;
	public string IpAddress { get; set; } = string.Empty;
	public string UserAgent { get; set; } = string.Empty;
	public string Location { get; set; } = string.Empty;
	public DateTimeOffset LastActivityAt { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
	public bool IsActive { get; set; }
	public string? Notes { get; set; }
	public string? RefreshToken { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	
	// Organization context (for System Admin view)
	public Guid OrganizationId { get; set; }
	public string? OrganizationName { get; set; }
}

public class CreateSessionDto
{
	public string DeviceId { get; set; } = string.Empty;
	public string DeviceName { get; set; } = string.Empty;
	public string DeviceType { get; set; } = string.Empty;
	public string BrowserName { get; set; } = string.Empty;
	public string BrowserVersion { get; set; } = string.Empty;
	public string OperatingSystem { get; set; } = string.Empty;
	public string IpAddress { get; set; } = string.Empty;
	public string UserAgent { get; set; } = string.Empty;
	public string Location { get; set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; set; }
	public string? Notes { get; set; }
}
