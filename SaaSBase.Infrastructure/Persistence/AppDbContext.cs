using SaaSBase.Application;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace SaaSBase.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
	private readonly ICurrentTenantService _currentTenantService;

	public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenantService currentTenantService) : base(options)
	{
		_currentTenantService = currentTenantService;
	}

	// Identity & Auth
	public DbSet<User> Users { get; set; } = null!;
	public DbSet<Role> Roles { get; set; } = null!;
	public DbSet<Permission> Permissions { get; set; } = null!;
	public DbSet<Menu> Menus { get; set; } = null!;
	public DbSet<UserRole> UserRoles { get; set; } = null!;
	public DbSet<RolePermission> RolePermissions { get; set; } = null!;
	public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
	public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
	public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; } = null!;
	public DbSet<PasswordPolicy> PasswordPolicies { get; set; } = null!;
	public DbSet<UserSession> UserSessions { get; set; } = null!;
	public DbSet<MfaSettings> MfaSettings { get; set; } = null!;
	public DbSet<MfaAttempt> MfaAttempts { get; set; } = null!;
	public DbSet<UserActivityLog> UserActivityLogs { get; set; } = null!;
	public DbSet<UserNotificationPreferences> UserNotificationPreferences { get; set; } = null!;
	public DbSet<UserImportHistory> UserImportHistories { get; set; } = null!;
	public DbSet<RoleImportHistory> RoleImportHistories { get; set; } = null!;
	public DbSet<PermissionImportHistory> PermissionImportHistories { get; set; } = null!;
	public DbSet<ImportExportHistory> ImportExportHistories { get; set; } = null!;

	// Organization Management
	public DbSet<Organization> Organizations { get; set; } = null!;
	public DbSet<Location> Locations { get; set; } = null!;
	public DbSet<Department> Departments { get; set; } = null!;
	public DbSet<Position> Positions { get; set; } = null!;
	public DbSet<BusinessSetting> BusinessSettings { get; set; } = null!;
	public DbSet<Currency> Currencies { get; set; } = null!;
	public DbSet<TaxRate> TaxRates { get; set; } = null!;
	public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
	public DbSet<IntegrationSetting> IntegrationSettings { get; set; } = null!;

	// Compliance & Privacy Management
	public DbSet<DataSubjectRequest> DataSubjectRequests { get; set; } = null!;
	public DbSet<PrivacySettings> PrivacySettings { get; set; } = null!;
	public DbSet<DataBreachIncident> DataBreachIncidents { get; set; } = null!;
	public DbSet<PrivacyImpactAssessment> PrivacyImpactAssessments { get; set; } = null!;
	public DbSet<DataProcessingRecord> DataProcessingRecords { get; set; } = null!;
	public DbSet<DataTransferRecord> DataTransferRecords { get; set; } = null!;

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		// Enable PostgreSQL extensions
		modelBuilder.HasPostgresExtension("pg_trgm"); // Required for trigram GIN indexes on text search

		modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
			{
				var method = typeof(AppDbContext).GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Static)!
					.MakeGenericMethod(entityType.ClrType);
				method.Invoke(null, new object[] { modelBuilder, this });
			}
		}

		// Configure RowVersion concurrency token for entities that have it
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			var prop = entityType.FindProperty("RowVersion");
			if (prop is not null)
			{
				prop.IsConcurrencyToken = true;
				// Do not treat RowVersion as store-generated in Postgres; we will set it in-app
				prop.ValueGenerated = ValueGenerated.Never;
				prop.SetMaxLength(8);
				prop.IsNullable = false;
			}
		}

		// Configure partial unique index for User email (only for non-deleted records)
		// This allows soft deleted users to have the same email as new users
		modelBuilder.Entity<User>()
			.HasIndex(u => new { u.Email, u.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");

		// Performance indexes for User table
		ConfigureUserPerformanceIndexes(modelBuilder);
		ConfigureUserRolePerformanceIndexes(modelBuilder);
		ConfigureUserActivityLogPerformanceIndexes(modelBuilder);
		
		// Performance indexes for Role table
		ConfigureRolePerformanceIndexes(modelBuilder);
		ConfigureRolePermissionPerformanceIndexes(modelBuilder);
	}

	private static void SetTenantFilter<TEntity>(ModelBuilder builder, AppDbContext context) where TEntity : class
	{
		// Enforce tenant scoping strictly: for tenant entities, require a non-empty tenant and match
		builder.Entity<TEntity>().HasQueryFilter(e =>
			!EF.Property<bool>(e, "IsDeleted") &&
			(
				!typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)) ||
				(
					context._currentTenantService.GetOrganizationId() != Guid.Empty &&
					EF.Property<Guid>(e, "OrganizationId") == context._currentTenantService.GetOrganizationId()
				)
			)
		);
	}

	/// <summary>
	/// Configure performance indexes for User table
	/// </summary>
	private static void ConfigureUserPerformanceIndexes(ModelBuilder modelBuilder)
	{
		// Composite index for organization + soft delete filtering
		modelBuilder.Entity<User>()
			.HasIndex(nameof(User.OrganizationId), nameof(User.IsDeleted))
			.HasDatabaseName("IX_users_OrganizationId_IsDeleted");

		// Composite index for organization + active + verified filtering
		modelBuilder.Entity<User>()
			.HasIndex(nameof(User.OrganizationId), nameof(User.IsActive), nameof(User.IsEmailVerified))
			.HasDatabaseName("IX_users_Org_Active_Verified")
			.HasFilter("\"IsDeleted\" = false");

		// Index for department filtering
		modelBuilder.Entity<User>()
			.HasIndex(nameof(User.Department))
			.HasDatabaseName("IX_users_Department")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for job title filtering
		modelBuilder.Entity<User>()
			.HasIndex(nameof(User.JobTitle))
			.HasDatabaseName("IX_users_JobTitle")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for location filtering
		modelBuilder.Entity<User>()
			.HasIndex(nameof(User.Location))
			.HasDatabaseName("IX_users_Location")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for created date sorting
		modelBuilder.Entity<User>()
			.HasIndex(nameof(User.CreatedAtUtc))
			.HasDatabaseName("IX_users_CreatedAtUtc")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for last login sorting
		modelBuilder.Entity<User>()
			.HasIndex(nameof(User.LastLoginAt))
			.HasDatabaseName("IX_users_LastLoginAt")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Full-text search index for PostgreSQL (GIN index with trigram operator)
		// Note: Requires pg_trgm extension to be enabled
		modelBuilder.Entity<User>()
			.HasIndex("Email", "FullName", "EmployeeId")
			.HasDatabaseName("IX_users_Search")
			.HasMethod("gin")
			.HasOperators("gin_trgm_ops", "gin_trgm_ops", "gin_trgm_ops")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");
	}

	/// <summary>
	/// Configure performance indexes for UserRole table
	/// </summary>
	private static void ConfigureUserRolePerformanceIndexes(ModelBuilder modelBuilder)
	{
		// Composite index for user + organization filtering
		modelBuilder.Entity<UserRole>()
			.HasIndex(nameof(UserRole.UserId), nameof(UserRole.OrganizationId))
			.HasDatabaseName("IX_userroles_UserId_OrganizationId");

		// Composite index for role + organization filtering
		modelBuilder.Entity<UserRole>()
			.HasIndex(nameof(UserRole.RoleId), nameof(UserRole.OrganizationId))
			.HasDatabaseName("IX_userroles_RoleId_OrganizationId");
	}

	/// <summary>
	/// Configure performance indexes for UserActivityLog table
	/// </summary>
	private static void ConfigureUserActivityLogPerformanceIndexes(ModelBuilder modelBuilder)
	{
		// Composite index for user + timestamp filtering
		modelBuilder.Entity<UserActivityLog>()
			.HasIndex(nameof(UserActivityLog.UserId), nameof(UserActivityLog.Timestamp))
			.HasDatabaseName("IX_useractivitylogs_UserId_Timestamp");

		// Composite index for organization + timestamp filtering
		modelBuilder.Entity<UserActivityLog>()
			.HasIndex(nameof(UserActivityLog.OrganizationId), nameof(UserActivityLog.Timestamp))
			.HasDatabaseName("IX_useractivitylogs_OrganizationId_Timestamp");
	}

	/// <summary>
	/// Configure performance indexes for Role table
	/// </summary>
	private static void ConfigureRolePerformanceIndexes(ModelBuilder modelBuilder)
	{
		// Composite index for organization + soft delete filtering
		modelBuilder.Entity<Role>()
			.HasIndex(nameof(Role.OrganizationId), nameof(Role.IsDeleted))
			.HasDatabaseName("IX_roles_OrganizationId_IsDeleted");

		// Composite index for organization + active filtering
		modelBuilder.Entity<Role>()
			.HasIndex(nameof(Role.OrganizationId), nameof(Role.IsActive))
			.HasDatabaseName("IX_roles_Org_Active")
			.HasFilter("\"IsDeleted\" = false");

		// Index for role type filtering
		modelBuilder.Entity<Role>()
			.HasIndex(nameof(Role.RoleType))
			.HasDatabaseName("IX_roles_RoleType")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for parent role filtering
		modelBuilder.Entity<Role>()
			.HasIndex(nameof(Role.ParentRoleId))
			.HasDatabaseName("IX_roles_ParentRoleId")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for sort order
		modelBuilder.Entity<Role>()
			.HasIndex(nameof(Role.SortOrder))
			.HasDatabaseName("IX_roles_SortOrder")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for created date sorting
		modelBuilder.Entity<Role>()
			.HasIndex(nameof(Role.CreatedAtUtc))
			.HasDatabaseName("IX_roles_CreatedAtUtc")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Index for updated date sorting
		modelBuilder.Entity<Role>()
			.HasIndex(nameof(Role.ModifiedAtUtc))
			.HasDatabaseName("IX_roles_UpdatedAtUtc")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");

		// Full-text search index for PostgreSQL (GIN index with trigram operator)
		// Note: Requires pg_trgm extension to be enabled
		modelBuilder.Entity<Role>()
			.HasIndex("Name", "Description")
			.HasDatabaseName("IX_roles_Search")
			.HasMethod("gin")
			.HasOperators("gin_trgm_ops", "gin_trgm_ops")
			.HasFilter("\"IsDeleted\" = false AND \"OrganizationId\" IS NOT NULL");
	}

	/// <summary>
	/// Configure performance indexes for RolePermission table
	/// </summary>
	private static void ConfigureRolePermissionPerformanceIndexes(ModelBuilder modelBuilder)
	{
		// Composite index for role + organization filtering
		modelBuilder.Entity<RolePermission>()
			.HasIndex(nameof(RolePermission.RoleId), nameof(RolePermission.OrganizationId))
			.HasDatabaseName("IX_rolepermissions_RoleId_OrganizationId");

		// Composite index for permission + organization filtering
		modelBuilder.Entity<RolePermission>()
			.HasIndex(nameof(RolePermission.PermissionId), nameof(RolePermission.OrganizationId))
			.HasDatabaseName("IX_rolepermissions_PermissionId_OrganizationId");

		// Unique index for role + permission combination
		modelBuilder.Entity<RolePermission>()
			.HasIndex(nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId), nameof(RolePermission.OrganizationId))
			.IsUnique()
			.HasDatabaseName("IX_rolepermissions_RoleId_PermissionId_OrganizationId");
	}

	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		var utcNow = DateTimeOffset.UtcNow;
		var organizationId = _currentTenantService.GetOrganizationId();

		foreach (var entry in ChangeTracker.Entries<BaseEntity>())
		{
			if (entry.State == EntityState.Added)
			{
				if (entry.Entity is Menu)
				{
					entry.Entity.OrganizationId = Guid.Empty;
				}
				else
				{
					entry.Entity.OrganizationId = entry.Entity.OrganizationId == Guid.Empty ? organizationId : entry.Entity.OrganizationId;
				}
				entry.Entity.CreatedAtUtc = utcNow;
			}
			else if (entry.State == EntityState.Modified)
			{
				entry.Entity.ModifiedAtUtc = utcNow;
			}
		}

		return base.SaveChangesAsync(cancellationToken);
	}
}


