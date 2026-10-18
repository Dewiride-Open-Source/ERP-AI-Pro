using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Domain;

// The colour theme a person chose, in a row of its own keyed by their record's id, so that a choice moves none of the versions
// an administrator's open forms of that record hold. A person without a row follows the theme of their device. Each change
// of theme is a choice whose id a browser compares with the one it noted a choice over, to tell whether the account changed
// since. The id is worked out from the choice it replaces and the theme, and choosing the theme already held changes nothing,
// so the open tabs of a browser, which each save the change any one of them makes, are all answered the same id.
internal sealed class PersonTheme : AggregateRoot<UserId>, IAuditable
{
    private PersonTheme(UserId id)
        : base(id)
    {
    }

    public UserTheme Theme { get; private set; } = UserTheme.System;

    public Guid ChoiceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static PersonTheme ChooseFirst(UserId userId, UserTheme theme)
    {
        if (theme == UserTheme.System)
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "A person without a theme of their own follows the theme of their device already.");
        }

        var chosen = new PersonTheme(userId);
        chosen.Choose(theme);

        return chosen;
    }

    public void Choose(UserTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "A person chooses the system, light or dark theme.");
        }

        if (theme == Theme)
        {
            return;
        }

        ChoiceId = ChoiceIdOf(ChoiceId, theme);
        Theme = theme;
    }

    // RFC 9562 section 5.8: a version 8 UUID, whose free bits are the start of the SHA-256 hash of the replaced choice's id and
    // the theme.
    private static Guid ChoiceIdOf(Guid replaced, UserTheme theme)
    {
        Span<byte> input = stackalloc byte[17];
        replaced.TryWriteBytes(input[..16], bigEndian: true, out _);
        input[16] = (byte)theme;

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash[..16], bigEndian: true);
    }
}
