using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaSBase.Infrastructure.Persistence;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
	public void Configure(EntityTypeBuilder<RefreshToken> builder)
	{
		builder.ToTable("refresh_tokens");
		builder.HasIndex(x => x.Token).IsUnique();
		builder.Property(x => x.Token).IsRequired().HasMaxLength(512);
		builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
	public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
	{
		builder.ToTable("password_reset_tokens");
		builder.HasIndex(x => x.Token).IsUnique();
		builder.Property(x => x.Token).IsRequired().HasMaxLength(512);
		builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}
