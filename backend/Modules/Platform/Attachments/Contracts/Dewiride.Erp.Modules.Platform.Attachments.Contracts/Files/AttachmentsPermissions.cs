namespace Dewiride.Erp.Modules.Platform.Attachments.Contracts.Files;

public static class AttachmentsPermissions
{
    public const string Upload = "platform.attachments.files.upload";

    public const string Read = "platform.attachments.files.read";

    public const string Delete = "platform.attachments.files.delete";

    public static IReadOnlyList<string> All { get; } = [Upload, Read, Delete];
}
