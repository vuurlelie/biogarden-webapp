using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> postBuilder)
    {
        postBuilder.ToTable("Posts");

        postBuilder.Property(post => post.Title)
            .HasMaxLength(200)
            .IsRequired();

        postBuilder.HasIndex(post => post.Title)
            .IsUnique();

        postBuilder.Property(post => post.Article)
            .IsRequired();

        postBuilder.Property(post => post.Image)
            .HasMaxLength(500);

        postBuilder.Property(post => post.PublishedAt)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        postBuilder.Property(post => post.RecordStatus)
            .HasConversion<byte>();

        postBuilder.HasQueryFilter(post =>
            post.RecordStatus == RecordStatus.Active);

        postBuilder.HasIndex(post => post.PublishedAt);

        postBuilder.HasIndex(post => new
        {
            post.RecordStatus,
            post.Draft,
            post.Category,
            post.PublishedAt,
            post.Title
        }).HasDatabaseName("IX_Posts_ListFilters_PublishedAt");

        postBuilder.HasIndex(post => new
        {
            post.RecordStatus,
            post.Draft,
            post.Category,
            post.Title,
            post.PublishedAt
        }).HasDatabaseName("IX_Posts_ListFilters_Title");
    }
}
