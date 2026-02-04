using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaSBase.Infrastructure.Persistence;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
	public void Configure(EntityTypeBuilder<User> builder)
	{
		builder.ToTable("users");
		// Note: Unique index with IsDeleted filter is configured in AppDbContext.cs
		// to override this default configuration
		builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
		builder.Property(x => x.FullName).HasMaxLength(256);
		builder.Property(x => x.FirstName).HasMaxLength(100);
		builder.Property(x => x.LastName).HasMaxLength(100);
		builder.Property(x => x.PhoneNumber).HasMaxLength(20);
		builder.Property(x => x.PasswordHash).IsRequired();
		builder.Property(x => x.IsEmailVerified).IsRequired();
		builder.Property(x => x.IsPhoneVerified).IsRequired();
		builder.Property(x => x.LastLoginAt);
		builder.Property(x => x.PasswordChangedAt);
		builder.Property(x => x.FailedLoginAttempts).IsRequired();
		builder.Property(x => x.LockedUntil);
		builder.Property(x => x.AvatarUrl).HasMaxLength(500);
		builder.Property(x => x.TimeZone).HasMaxLength(50);
		builder.Property(x => x.Language).HasMaxLength(10);
		builder.Property(x => x.Notes).HasMaxLength(1000);
		builder.Property(x => x.IsMfaEnabled).IsRequired();
		builder.Property(x => x.LastPasswordChange);
		builder.Property(x => x.PasswordExpiresAt);
		builder.Property(x => x.MustChangePasswordOnNextLogin).IsRequired();
		builder.Property(x => x.JobTitle).HasMaxLength(100);
		builder.Property(x => x.Department).HasMaxLength(100);
		builder.Property(x => x.EmployeeId).HasMaxLength(50);
		builder.Property(x => x.DateOfBirth);
		builder.Property(x => x.Address).HasMaxLength(500);
		builder.Property(x => x.City).HasMaxLength(100);
		builder.Property(x => x.State).HasMaxLength(100);
		builder.Property(x => x.Country).HasMaxLength(100);
		builder.Property(x => x.PostalCode).HasMaxLength(20);
		builder.Property(x => x.EmergencyContactName).HasMaxLength(200);
		builder.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
		builder.Property(x => x.EmergencyContactRelation).HasMaxLength(50);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
	public void Configure(EntityTypeBuilder<Role> builder)
	{
		builder.ToTable("roles");
		builder.HasIndex(x => new { x.Name, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.RoleType).IsRequired().HasMaxLength(20);
		builder.Property(x => x.ParentRoleId);
		builder.Property(x => x.Level).IsRequired();
		builder.Property(x => x.IsSystemRole).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();
		builder.Property(x => x.SortOrder).IsRequired();
		builder.Property(x => x.Color).HasMaxLength(20);
		builder.Property(x => x.Icon).HasMaxLength(50);
		builder.Property(x => x.RowVersion).IsRowVersion();

		builder.HasOne(x => x.ParentRole)
			.WithMany(x => x.ChildRoles)
			.HasForeignKey(x => x.ParentRoleId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
	public void Configure(EntityTypeBuilder<Menu> builder)
	{
		builder.ToTable("menus");
		builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Route).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Icon).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Section).HasMaxLength(100);
		builder.Property(x => x.ParentMenuId);
		builder.Property(x => x.SortOrder).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Badge).HasMaxLength(50);
		builder.Property(x => x.BadgeColor).HasMaxLength(20);
		builder.Property(x => x.IsSystemMenu).IsRequired();
		builder.Property(x => x.RowVersion).IsRowVersion();

		builder.HasOne(x => x.ParentMenu)
			.WithMany(x => x.ChildMenus)
			.HasForeignKey(x => x.ParentMenuId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.Route, x.OrganizationId });
		builder.HasIndex(x => new { x.Section, x.OrganizationId });
	}
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
	public void Configure(EntityTypeBuilder<Permission> builder)
	{
		builder.ToTable("permissions");
		builder.HasIndex(x => new { x.Code, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Code).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Module).IsRequired().HasMaxLength(50);
		builder.Property(x => x.Action).IsRequired().HasMaxLength(50);
		builder.Property(x => x.Resource).IsRequired().HasMaxLength(50);
		builder.Property(x => x.IsSystemPermission).IsRequired();
		builder.Property(x => x.IsSystemAdminOnly).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();
		builder.Property(x => x.SortOrder).IsRequired();
		builder.Property(x => x.Category).HasMaxLength(50);
		
		// ✅ Menu Foreign Key (Required)
		builder.Property(x => x.MenuId).IsRequired();
		builder.HasOne(x => x.Menu)
			.WithMany(x => x.Permissions)
			.HasForeignKey(x => x.MenuId)
			.OnDelete(DeleteBehavior.Restrict);
		
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
	public void Configure(EntityTypeBuilder<UserRole> builder)
	{
		builder.ToTable("user_roles");
		builder.HasKey(x => new { x.UserId, x.RoleId, x.OrganizationId });
		builder.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
		builder.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
	}
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
	public void Configure(EntityTypeBuilder<RolePermission> builder)
	{
		builder.ToTable("role_permissions");
		builder.HasKey(x => new { x.RoleId, x.PermissionId, x.OrganizationId });
		builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
		builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
	}
}


