using System;

namespace SaaSBase.Domain;

public class RefreshToken : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;
	public string Token { get; set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; set; }
	public bool IsRevoked { get; set; }
	public DateTimeOffset? RevokedAt { get; set; }
	public string? RevokedBy { get; set; }
}

public class PasswordResetToken : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;
	public string Token { get; set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; set; }
	public bool IsUsed { get; set; }
	public DateTimeOffset? UsedAt { get; set; }
}

public class EmailVerificationToken : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;
	public string Token { get; set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; set; }
	public bool IsUsed { get; set; }
	public DateTimeOffset? UsedAt { get; set; }
}