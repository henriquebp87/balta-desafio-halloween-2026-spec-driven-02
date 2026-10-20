using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data.Configurations;

public sealed class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("Links");
        builder.HasKey(link => link.Id);

        builder.Property(link => link.Code)
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(link => link.Code)
            .IsUnique();

        builder.Property(link => link.TargetUrl)
            .HasMaxLength(2048)
            .IsRequired();
        builder.Property(link => link.CreatedAtUtc)
            .IsRequired();
        builder.Property(link => link.AccessCount)
            .HasDefaultValue(0L)
            .IsRequired();
        builder.Property(link => link.ManagementKeyHash)
            .IsRequired();
    }
}
