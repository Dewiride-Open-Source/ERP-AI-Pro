# Platform / Attachments

HTTP surface of the attachments building block (`BuildingBlocks/Attachments`): upload, list, describe, delete and download files. The storage, encryption, metadata and download links live in the building block so that business modules can attach files through `IAttachmentService` without referencing this module; this module adds the routes, the handlers and the upload transport.

| Aspect | Value |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Platform.Attachments` |
| Schema | none of its own; the building block owns `files` (`AttachmentsDbContext`: `Attachments`, `StoredContents`, `UploadReservations`, `DownloadLinks`, `DownloadRedemptions`) |
| Route group | `/api/platform/attachments`: `GET /` (paged: `page`, `pageSize`, `sort` on `fileName`, `contentType`, `sizeBytes`, `createdAt`, `filter` on `fileName`, `contentType`, `createdAt`), `GET /policy`, `POST /` (multipart/form-data, first part with a file name), `GET /{id}`, `DELETE /{id}`, `POST /{id}/download-links`, `GET /{id}/content?link=` |
| Feature flag | `Erp.Modules.Platform.Attachments` |
| Permissions | `platform.attachments.files.upload`, `platform.attachments.files.read`, `platform.attachments.files.delete` (enforced from the authentication phase) |
| Transfers | upload and download use the request timeout policy `platform.attachments.transfer` (`Erp:Platform:Attachments:TransferTimeout`); the upload's body limit is `MaxSizeBytes` plus 64 KiB of multipart framing, applied by routing through `UploadSizeLimit` |
| Contract | `AttachmentsPermissions` |
| Events | none |

The download is a two-step flow: `POST /{id}/download-links` returns a path with a single-purpose token bound to the person asking and valid for `DownloadLinkLifetime`; `GET /{id}/content?link=` streams the decrypted file and records the redemption. Every refusal is the same 404 `attachment.not-found`.
