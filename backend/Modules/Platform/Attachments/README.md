# Platform / Attachments

HTTP surface of the attachments building block (`BuildingBlocks/Attachments`): upload, list, describe, delete and download files. The storage, encryption, metadata and download links live in the building block so that business modules can attach files through `IAttachmentService` without referencing this module; this module adds the routes, the handlers and the upload transport.

| Aspect | Value |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Platform.Attachments` |
| Schema | none of its own; the building block owns `files` (`AttachmentsDbContext`: `Attachments`, `StoredContents`, `UploadReservations`, `DownloadLinks`, `DownloadRedemptions`) |
| Route group | `/api/platform/attachments`: `GET /` (paged: `page`, `pageSize`, `sort` on `fileName`, `contentType`, `sizeBytes`, `createdAt`, `filter` on `fileName`, `contentType`, `createdAt`), `GET /policy`, `POST /` (multipart/form-data, first part with a file name whose extension belongs to the declared type), `GET /{id}`, `DELETE /{id}`, `POST /{id}/download-links`, `GET /{id}/content?link=` (the public request record `RedeemDownloadLinkRequest`: `link` required, at most 64 characters) |
| Feature flag | `Erp.Modules.Platform.Attachments` |
| Permissions | `platform.attachments.files.upload`, `platform.attachments.files.read`, `platform.attachments.files.delete` (enforced from the authentication phase) |
| Transfers | upload and download use the request timeout policy `platform.attachments.transfer` (`Erp:Platform:Attachments:TransferTimeout`); the upload's body limit is `MaxSizeBytes` plus 64 KiB of multipart framing, applied by routing through `UploadSizeLimit`; the upload also requires the rate-limiter policy `platform.attachments.uploads`, one concurrency limiter for the whole process (`Erp:Platform:Attachments:MaxConcurrentUploads`, no queue), because every upload in flight holds a 4 MiB staging block for its whole transfer; over the cap it answers 429 `rate-limit.exceeded` with `Retry-After` |
| Contract | `AttachmentsPermissions` |
| Events | none |

The download is a two-step flow. `POST /{id}/download-links` (the command `CreateDownloadLinkCommand`) returns a path with a single-purpose token bound to the person asking and valid for `DownloadLinkLifetime`, and answers 404 `attachment.not-found` for an unknown or deleted attachment and for one whose stored content cannot be opened (the blob is missing or its key is not configured). `GET /{id}/content?link=` (the command `RedeemDownloadLinkCommand`, a command because it records the redemption) streams the decrypted file; a missing `link` or one longer than 64 characters is the validation problem 400 `request.invalid`, and every other refusal (a wrong, expired or another person's token, a deleted attachment, missing or damaged content) is the same 404 `attachment.not-found`.
