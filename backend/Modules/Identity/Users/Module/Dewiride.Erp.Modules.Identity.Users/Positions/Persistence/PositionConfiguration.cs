using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Persistence;

// The rows a position names are soft-deleted rather than removed, so every key restricts deletion and history keeps naming
// them. The relationships have no navigations: positions are read by joins that choose for themselves whether deleted rows
// count. A position's changes are filed under its person, whose history shows where they worked and for whom.
internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        builder.RecordChangesAs("identity.users.positions");
        builder.FileChangesUnder(p => p.UserId);
        builder.HasKey(p => p.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.ManagerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Designation>().WithMany().HasForeignKey(p => p.DesignationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.UserId, p.EffectiveFrom }).IsUnique();
    }
}
