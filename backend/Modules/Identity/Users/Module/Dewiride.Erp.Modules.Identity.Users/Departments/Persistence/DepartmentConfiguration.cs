using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Persistence;

// A name is unique among the departments of one parent that are not deleted; SQL Server's unique index treats NULLs as equal,
// so two top-level departments cannot share a name either. The filter is spelled out because EF Core would otherwise leave
// rows with no parent out of a unique index on a nullable column.
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.RecordChangesAs("identity.users.departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(Department.NameMaxLength);
        builder.HasOne<Department>().WithMany().HasForeignKey(d => d.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.ParentId, d.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
