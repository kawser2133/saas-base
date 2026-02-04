using System;

namespace SaaSBase.Domain;

public abstract class BaseEntity
{
	public Guid Id { get; set; } = Guid.NewGuid();

	// Multi-tenant discriminator for SaaS isolation
	public Guid OrganizationId { get; set; }

	// Auditing
	public DateTimeOffset CreatedAtUtc { get; set; }
	public string? CreatedBy { get; set; }
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? ModifiedBy { get; set; }

	// Soft delete
	public bool IsDeleted { get; set; }
	public DateTimeOffset? DeletedAtUtc { get; set; }
	public string? DeletedBy { get; set; }

	// Concurrency token
	public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public interface ITenantEntity
{
	Guid OrganizationId { get; set; }
}

public interface IAggregateRoot { }


