using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Persistence;

internal sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.ToTable("Designations");
        builder.RecordChangesAs("identity.users.designations");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(Designation.NameMaxLength);
        builder.HasIndex(d => d.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
