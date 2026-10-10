using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Identity.Users.People.Persistence;

// A soft-deleted record keeps its row, so the unique indexes leave deleted rows out and a person whose record was deleted
// can be registered, or sign in, again. A work email is unique only among records no sign-in has linked yet, the ones a
// first sign-in matches by it: Entra can give the address of a person who left to someone else, whose record is their own.
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength);
        builder.Property(u => u.WorkEmail).HasMaxLength(User.WorkEmailMaxLength);
        builder.Property(u => u.EmployeeCode).HasMaxLength(User.EmployeeCodeMaxLength);
        builder.Property(u => u.PhoneNumber).HasMaxLength(User.PhoneNumberMaxLength).IsUnicode(false);
        builder.Property(u => u.Designation).HasMaxLength(User.DesignationMaxLength);
        builder.Property(u => u.Status).HasConversion<byte>();
        builder.HasIndex(u => u.EntraObjectId).IsUnique().HasFilter("[EntraObjectId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(u => u.WorkEmail).IsUnique().HasFilter("[EntraObjectId] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(u => u.EmployeeCode).IsUnique().HasFilter("[EmployeeCode] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(u => u.DisplayName);
    }
}
