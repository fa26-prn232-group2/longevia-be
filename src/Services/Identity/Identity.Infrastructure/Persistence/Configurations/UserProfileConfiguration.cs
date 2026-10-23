using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.HasIndex(p => p.UserId)
            .IsUnique();

        builder.Property(p => p.BirthDate)
            .IsRequired();

        builder.Property(p => p.BiologicalSex)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.HeightCm)
            .IsRequired();

        builder.Property(p => p.ActivityLevel)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.DietaryPreference)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Allergies)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();
    }
}
