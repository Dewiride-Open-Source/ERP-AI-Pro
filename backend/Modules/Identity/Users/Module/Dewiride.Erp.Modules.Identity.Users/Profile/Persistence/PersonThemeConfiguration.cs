using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Profile.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Persistence;

// A theme's changes are filed under its person, so their history shows the themes they chose; the choice id only tells
// browsers whether the account changed, so it is left out of the records.
internal sealed class PersonThemeConfiguration : IEntityTypeConfiguration<PersonTheme>
{
    public void Configure(EntityTypeBuilder<PersonTheme> builder)
    {
        builder.ToTable("PersonThemes");
        builder.RecordChangesAs("identity.users.themes");
        builder.FileChangesUnder(t => t.Id);
        builder.HasKey(t => t.Id);
        builder.HasOne<User>().WithOne().HasForeignKey<PersonTheme>(t => t.Id).OnDelete(DeleteBehavior.Restrict);
        builder.Property(t => t.Theme).HasConversion<byte>();
        builder.Property(t => t.ChoiceId).ExcludeFromChangeRecords();
    }
}
