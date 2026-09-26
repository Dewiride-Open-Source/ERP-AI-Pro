using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

public static class AttachmentErrors
{
    public static readonly Error NotFound = Error.NotFound("attachment.not-found", "The attachment does not exist, or the link to it is not valid.");

    public static readonly Error MultipartRequired = Error.UnsupportedType("attachment.multipart-required", "Send the file as multipart/form-data.");

    public static readonly Error FileMissing = Error.Validation("attachment.file-missing", "The request carries no file; send one multipart/form-data part with a file name.");

    public static readonly Error FileNameInvalid = Error.Validation("attachment.file-name-invalid", $"The file name must contain a visible character and be at most {Attachment.FileNameMaxLength} characters long.");

    public static readonly Error Empty = Error.Validation("attachment.empty", "The file is empty.");

    public static readonly Error Incomplete = Error.Validation("attachment.incomplete", "The upload ended before the whole file arrived.");

    public static readonly Error TooLarge = Error.TooLarge("attachment.too-large", "The file is larger than the configured upload limit.");

    public static readonly Error UnsupportedType = Error.UnsupportedType("attachment.unsupported-type", "Files of this type cannot be uploaded.");

    public static readonly Error ContentMismatch = Error.UnsupportedType("attachment.content-mismatch", "The file's content does not match its declared type.");

    public static readonly Error Infected = Error.Failure("attachment.infected", "The virus scanner rejected the file.");
}
