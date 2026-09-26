# Attachments

How files are stored, read and removed, end to end. The decision, the rejected alternatives and the envelope format are in [ADR-0022](../adr/0022-attachments-in-azure-blob-storage.md); the settings are in the [configuration register](../configuration.md), the routes in the [routing register](../routing.md), and the key procedures in the [secrets runbook](../operations/runbooks/secrets.md), section 5f.

In one paragraph: the API streams an uploaded file through a size limit, a name and type check, SHA-256, an optional virus-scan hook and an AES-256-GCM envelope into uncommitted blocks of an Azure Storage block blob named by a random id; it commits the blob only when no identical stored content can be verified to open, and records the file in the SQL schema `files`. A download is a two-step flow: a short-lived link bound to the person who asked for it, then a streamed, decrypted response that is recorded. Tests, CI and the local containers use the Azurite emulator instead of a storage account.

## Components

| Piece | Location | Role |
|---|---|---|
| Building block | `backend/BuildingBlocks/Attachments/Dewiride.Erp.BuildingBlocks.Attachments` | storage, encryption, inspection, metadata, links, the sweeper, telemetry and health; the contract `IAttachmentService` |
| Module | `backend/Modules/Platform/Attachments` (`Dewiride.Erp.Modules.Platform.Attachments`) | the seven routes under `/api/platform/attachments`, their command and query handlers, the streamed multipart reader (`Files/Endpoints/Uploads/MultipartFileReader`), the per-endpoint body limit (`UploadSizeLimit`), the request timeout policy `platform.attachments.transfer` and the upload's rate-limiter policy `platform.attachments.uploads`; permissions in `AttachmentsPermissions` (Contracts) |
| Metadata | SQL schema `files`, `AttachmentsDbContext` (migration `InitialAttachments`, `ef.ts` key `attachments`) | attachments, stored contents, upload reservations, download links and redemptions |
| Content | one private container per environment: `attachments` in the Entra-only storage account (`sterpaiprodev` for development), or a container on Azurite | envelope bytes only, one blob per stored content |
| Key | `Erp:Platform:Attachments:EncryptionKey` (and `RetiredEncryptionKeys`), from Key Vault through App Configuration | wraps each file's data key |
| Web | `frontend/apps/web/src/features/platform/attachments`, route `/platform/attachments`, `shared/api/upload.ts`, `packages/ui/src/components/upload/file-drop-zone.tsx` | lists, uploads from the browser with progress, downloads and deletes |

Inside the building block:

| Folder | Types | Visibility |
|---|---|---|
| (root) | `AttachmentsOptions`, `AttachmentsRegistration.AddErpAttachments` | public |
| `Domain` | `Attachment` (aggregate, `IAuditable`, `ISoftDeletable`), `StoredContent`, `UploadReservation`, `DownloadLink`, `DownloadRedemption`, their strongly-typed ids, `AttachmentScanStatus`, `AttachmentErrors` | `AttachmentId`, `AttachmentScanStatus` and `AttachmentErrors` public, the rest internal |
| `Service` | `IAttachmentService`, `AttachmentService`, `AttachmentUploader`, `StoredContentVerifier`, `AttachmentUpload`, `AttachmentDetails`, `AttachmentDownload`, `DownloadLinkDetails`, `UploadPolicy` | the interface and its records public, the implementations internal |
| `Scanning` | `IAttachmentScanner`, `IAttachmentScan`, `AttachmentScanVerdict` | public |
| `Encryption` | `EncryptionKey`, `EncryptionKeys` (parsing), `EncryptionKeySet`, `KeyRing`, `EnvelopeHeader`, `EnvelopeWriter`, `EnvelopeReadStream`, `EnvelopeFormatException` | internal |
| `Inspection` | `ContentTypes` (allow-list, extensions and signatures), `Utf8TextValidator` (whole-file text check), `FileNames` (sanitising), `ReplayedHeadStream` | internal |
| `Storage`, `Storage/Blob` | `IDocumentStore`, `IDocumentUpload`, `StoredContentMissingException`; `AttachmentBlobClients`, `BlobDocumentStore`, `BlockStagingStream`, `AttachmentStorageHealthCheck` | internal; only `Storage.Blob` may use `Azure.Storage` types |
| `Hosting` | `AttachmentStorageInitializer`, `AttachmentsSweeper` | internal |
| `Options`, `Persistence`, `Telemetry` | `AttachmentsOptionsValidator`; `AttachmentsDbContext` and the entity configurations; `AttachmentsMetrics` | internal |

`ErpHostComposition.AddErpPlatform` calls `builder.AddErpAttachments()` for every host (the API, the test database host, the migrator, `dotnet ef`). It binds `AttachmentsOptions` from `Erp:Platform:Attachments` with `ValidateDataAnnotations()` and `AttachmentsOptionsValidator` (no `ValidateOnStart()`), registers `AttachmentsDbContext` with `AddModuleDbContext<AttachmentsDbContext>("files")`, the singletons `KeyRing`, `AttachmentBlobClients`, `IDocumentStore` (`BlobDocumentStore`), `StoredContentVerifier` and `AttachmentsMetrics`, the scoped `AttachmentUploader` and `IAttachmentService`, the hosted services `AttachmentStorageInitializer` and `AttachmentsSweeper`, the readiness check `storage:attachments` and the tracing sources `Azure.Storage.Blobs.*`. `AttachmentsModule.AddServices` adds the module's two transport policies from the same options: the request timeout policy `platform.attachments.transfer` (`TransferTimeout`) and the rate-limiter policy `platform.attachments.uploads` (`MaxConcurrentUploads`). The migrator never starts its host, so it needs no attachment setting; the API validates them when `AttachmentStorageInitializer` starts.

## Data model

Schema `files`, migrations history `files.__EFMigrationsHistory`:

| Table | Columns | Keys and indexes |
|---|---|---|
| `Attachments` | `Id`, `ContentId`, `FileName nvarchar(255)`, `ContentType varchar(127)`, `ScanStatus tinyint` (`0` not scanned, `1` clean), the `IAuditable` and `ISoftDeletable` columns | primary key `Id`; `ContentId` → `StoredContents` (restrict); index `CreatedAt`; the `SoftDelete` query filter hides deleted rows |
| `StoredContents` | `Id` (also the blob name), `Sha256 binary(32)` of the plaintext, `Length bigint` of the plaintext, `KeyId varchar(32)`, `StoredAt` | primary key `Id`; non-unique index `(Sha256, Length)` for deduplication; index `KeyId` for rotation |
| `UploadReservations` | `ContentId`, `ReservedAt`, `ReservedBy` | primary key `ContentId`; index `ReservedAt` for the sweeper |
| `DownloadLinks` | `Id`, `AttachmentId`, `ActorId`, `TokenHash binary(32)`, `CreatedAt`, `ExpiresAt` | primary key `Id`; unique index `TokenHash`; index `ExpiresAt` for the sweeper's purge of unused links; `AttachmentId` → `Attachments` (restrict) |
| `DownloadRedemptions` | `Id`, `LinkId`, `AttachmentId`, `ActorId`, `RedeemedAt` | primary key `Id`; `LinkId` → `DownloadLinks`, `AttachmentId` → `Attachments` (restrict); index `(AttachmentId, RedeemedAt)` |

Several attachments may share one stored content (deduplication). A reservation exists only while its content id names no `StoredContents` row. A download link that expired more than `AttachmentsSweeper.LinkGracePeriod` (one hour) ago without a redemption is deleted by the sweeper; a redeemed link stays, because its `DownloadRedemptions` rows reference it. Blobs carry no metadata: everything about a file except its bytes is in these tables.

## Upload

`POST /api/platform/attachments`, `multipart/form-data`, no `Idempotency-Key` (ADR-0022):

1. Routing applies `UploadSizeLimit` (`IRequestSizeLimitMetadata`: `MaxSizeBytes` + 64 KiB) as the request's body limit and the endpoint runs under the timeout policy `platform.attachments.transfer` (`TransferTimeout`). The rate limiter admits the upload only while fewer than `MaxConcurrentUploads` uploads are in flight in the process (policy `platform.attachments.uploads`: a `ConcurrencyLimiter` with a single partition and `QueueLimit` 0, applied after the global limiter and independently of it, so it holds even when `Erp:Platform:RateLimiting:Enabled` is `false`), because every upload in flight holds a 4 MiB staging block for its whole transfer, however slowly its body arrives; otherwise it answers 429 `rate-limit.exceeded` with `Retry-After` from the shared rejection writer (`RateLimitRejection`, [request pipeline](request-pipeline.md)). The feature gate then checks `Erp.Modules.Platform.Attachments`.
2. `MultipartFileReader.ReadFirstFileAsync` requires `multipart/form-data` with a boundary of at most 70 characters (else 415 `attachment.multipart-required`), skips parts without a file name, takes the first part that has one (`filename*` before `filename`) and never reads the parts after it; no such part answers 400 `attachment.file-missing`.
3. `UploadAttachmentHandler` passes the part to `IAttachmentService.UploadAsync`, which delegates to `AttachmentUploader`. The handler runs through the pipeline's logging and validation steps but not its unit of work, because the module assembly owns no catalogue context.
4. `FileNames.Sanitize` drops any directory, control and formatting characters, normalises to NFC and requires 1 to 255 characters that are not all dots (else 400 `attachment.file-name-invalid`). The declared media type is lower-cased without parameters and must be in `AllowedContentTypes` (else 415 `attachment.unsupported-type`). `ContentTypes.NameMatches` then requires the sanitised name to end in an extension of that type (table below; `Path.GetExtension` compared without regard to case, so `notes.txt.`, `notes` and `notes.txt.url` fail), else 415 `attachment.extension-mismatch`: a downloaded file is opened by whatever program its extension names, so text saved as `.hta` or `.js`, or a ZIP saved as `.jar`, would otherwise run as a program on the reader's computer.
5. The first 8 KiB are read: none is 400 `attachment.empty`; bytes that do not match the declared type are 415 `attachment.content-mismatch` (table below). Nothing has been stored yet.
6. An `UploadReservation` with a new `StoredContentId` is saved.
7. The head is replayed ahead of the rest of the part (`ReplayedHeadStream`) and read 64 KiB at a time; each read goes through the length check (over `MaxSizeBytes` is 413 `attachment.too-large`), for `text/plain` and `text/csv` the `Utf8TextValidator` (every byte of the file must decode as UTF-8 without NUL, a character split across two reads included; else 415 `attachment.content-mismatch`), SHA-256, the scan (when an `IAttachmentScanner` is registered) and `EnvelopeWriter`, which writes the header and 64 KiB chunks into `BlockStagingStream`; every full 4 MiB is staged as an uncommitted block of the blob named by the content id.
8. The final chunk is written, and a text file must not end inside a character (415 `attachment.content-mismatch`); an `Infected` verdict answers 422 `attachment.infected`.
9. Deduplication (`AttachmentUploader.FindReusableAsync`): the newest three `StoredContents` rows with the same SHA-256 and length whose `KeyId` is the id of a configured key are candidates (the SQL comparison of `KeyId` ignores case, so it only narrows the list). `StoredContentVerifier.CanOpenAsync` reads the first `EnvelopeHeader.MaxLength` (125) bytes of each candidate's blob with one ranged read (`IDocumentStore.OpenHeadAsync`) and opens its data key with `EnvelopeHeader.OpenDataKey`, which requires the header's content id to be the candidate's and finds its key id ordinally in `KeyRing`; the first candidate that opens is reused and the staged blocks are deleted after the save. A `StoredContents` row alone proves nothing about its blob (storage restored from another point in time can lack it, and a key id can return with different material), so when no candidate opens, the upload keeps its own copy: the block list is committed with `If-None-Match: *` and a `StoredContents` row is added. When that commit fails with `BlobAlreadyExists` or `ConditionNotMet`, `BlockStagingStream` reads the blob's committed block list and treats a list identical to its own as success (the client library retried a commit whose response was lost, and no other writer uses the random name); any other list rethrows.
10. The `Attachment` (and any new `StoredContent`) is added and the reservation removed in one `SaveChangesAsync`; the answer is 201 with `Location: /api/platform/attachments/{id}` and the attachment.

Content checks (`ContentTypes`, the default allow-list is all of them):

| Media type | The file name must end in | The content must be |
|---|---|---|
| `application/pdf` | `.pdf` | first bytes `%PDF-` |
| `image/png` | `.png` | first bytes `89 50 4E 47 0D 0A 1A 0A` |
| `image/jpeg` | `.jpg` or `.jpeg` | first bytes `FF D8 FF` |
| `image/gif` | `.gif` | first bytes `GIF87a` or `GIF89a` |
| `image/webp` | `.webp` | first bytes `RIFF`, four bytes, `WEBP` |
| `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `.xlsx` | first bytes `PK 03 04` (a ZIP container) |
| `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | `.docx` | first bytes `PK 03 04` (a ZIP container) |
| `text/plain` | `.txt` | valid UTF-8 without NUL: the first 8 KiB before anything is stored (`ContentTypes.Matches`; the head may end inside a character when more follows), then every byte while it streams (`Utf8TextValidator`) |
| `text/csv` | `.csv` | as `text/plain` |

## List, describe and delete

- `GET /api/platform/attachments` binds `PagingParameters`: `page`, `pageSize` (1 to 200, default 50), `sort` over `fileName`, `contentType`, `sizeBytes` and `createdAt` (default `createdAt:desc`, with the id as tie-breaker), `filter` over `fileName`, `contentType` and `createdAt` ([application pipeline](application-pipeline.md), "Paging, sorting and filtering"). It answers `{ items, page, pageSize, totalCount, totalPages }`; deleted attachments are not listed.
- `GET /api/platform/attachments/{id}` answers the attachment or 404 `attachment.not-found`. `GET /api/platform/attachments/policy` answers `{ maxSizeBytes, allowedContentTypes }` (sorted) so a client refuses a file before sending it.
- `DELETE /api/platform/attachments/{id}` soft-deletes the attachment (204; 404 when unknown or already deleted). Its links stop working at once, because a redemption joins to an attachment that is not deleted. The `StoredContents` row and the blob stay: other attachments may share them, and purging belongs to the documents phase.

Every attachment answers as `{ id, fileName, contentType, sizeBytes, sha256 (64 lowercase hex), scanStatus ("notScanned" or "clean"), createdAt, createdBy }`.

## Download

1. `POST /api/platform/attachments/{id}/download-links` (command `CreateDownloadLinkCommand`) answers 404 `attachment.not-found` when the attachment does not exist or is deleted, and when `StoredContentVerifier` cannot open its stored content (the blob is missing, or its header names another content or a key that is not configured; logged at Error), because a link to such content would fail only after the person had followed it. Otherwise it draws 32 random bytes, saves a `DownloadLinks` row with their SHA-256, the attachment, the current actor and `ExpiresAt = now + DownloadLinkLifetime`, and answers `{ url, expiresAt }`. `url` is the path of the named route `Platform.Attachments.Download` from `LinkGenerator.GetPathByName`: `/api/platform/attachments/{id}/content?link=<token>`, the token in Base64Url (43 characters). The token is returned once and never stored.
2. `GET /api/platform/attachments/{id}/content?link=<token>` binds the public request record `RedeemDownloadLinkRequest` (`link`: `Required`, `StringLength(64)`): a missing link or one over 64 characters is the validation problem 400 `request.invalid` for `link`. Otherwise the command `RedeemDownloadLinkCommand` (a command, logged at Information by the pipeline's logging step, because it records a redemption) reaches `AttachmentService.OpenDownloadAsync`, which requires a token that decodes to exactly 32 bytes and a link with that hash, this attachment, the current actor and `ExpiresAt > now` on an attachment that is not deleted.
3. The blob is opened (`DownloadStreamingAsync`) and `EnvelopeReadStream.OpenAsync` authenticates the header, the content id, the key and the first chunk.
4. A `DownloadRedemptions` row is saved, `erp.attachments.downloads` counts `served`, and the response streams the plaintext: the stored content type, `Content-Disposition: attachment` with the stored name (`filename` and `filename*`), `Content-Length` from `StoredContents.Length`, `Cache-Control: no-store` like every API response, no range support.

Every refusal in steps 2 and 3 after request validation is the same 404 `attachment.not-found` and counts `refused`, so a caller cannot tell a wrong token from an expired link, another person's link, a deleted attachment or damaged content. A link can be redeemed again until it expires; each redemption is a row. An unredeemed link is deleted by `AttachmentsSweeper` once it expired more than an hour ago; a redeemed one stays with its redemptions. The web app's Server Function `createDownloadLink` calls step 1 and the row opens the returned path through a temporary same-origin `<a download>` (`downloadWithoutLeavingPage` in `attachment-actions.tsx`), so the download is an ordinary same-origin request to `/api` (carrying the session cookie once the authentication phase issues one), the browser saves the file without leaving the page, and a refused redemption never replaces the ERP with the problem body; a failure of the Server Function, the 404 for content that cannot be opened included, shows in the row (`attachment-action-error`).

## Sweeper

`AttachmentsSweeper` is a `BackgroundService` whose `PeriodicTimer` ticks every `SweepInterval` (the first tick one interval after start, so a short-lived test host never reaches it; the tests call its two public methods directly). Each tick runs, in its own scope:

1. `SweepReservationsAsync`: up to 100 of the oldest reservations whose `ReservedAt` is more than `UploadReservationLifetime` ago; for each, the blob is deleted when no `StoredContents` row has its id, then the reservation; `erp.attachments.reservations.swept` counts the blobs removed.
2. `PurgeUnusedLinksAsync`: one `ExecuteDelete` of the `DownloadLinks` rows whose `ExpiresAt` is more than `LinkGracePeriod` (one hour) ago and that have no `DownloadRedemptions` row (index `IX_DownloadLinks_ExpiresAt`); the grace period covers a link followed in the moment before it expired, whose redemption is recorded just after. `erp.attachments.links.purged` counts the rows deleted.

When either removed something, it logs Information `Removed the blobs of {Removed} abandoned attachment uploads and {Purged} unused download links`. It works from its own database's rows, never from a blob listing, so developers sharing one storage account never remove each other's files.

## Failure handling

| Failure | Outcome |
|---|---|
| `MaxConcurrentUploads` uploads already in flight | 429 `rate-limit.exceeded` with `Retry-After` from the rate limiter, before the body is read; nothing is stored and the upload metrics count nothing |
| Body over `MaxSizeBytes` + 64 KiB | Kestrel stops the read with `BadHttpRequestException`, which the endpoint does not catch: 413 `request.too-large` from `GlobalExceptionHandler`. A declared `Content-Length` over the limit fails at the first read, before anything is stored; a body without one fails once it passes the limit, and when the reservation was already saved the upload cleanup below runs |
| File over `MaxSizeBytes` within that allowance | 413 `attachment.too-large`; cleanup |
| Body ending before the closing boundary (the client or a proxy closed it early) | `IOException` or `InvalidDataException` while reading, caught by the endpoint: 400 `attachment.incomplete`; cleanup |
| Client disconnect or request timeout during the upload | `OperationCanceledException`: 499 for a caller that has gone, 504 `request.timeout` when the policy expired; cleanup |
| Any failure after the reservation was saved | `AttachmentUploader` cleans up in `finally` under its own 30-second deadline, not the request token: after clearing the change tracker it deletes the blob (committed or only staged) unless a `StoredContents` row references it, then removes the reservation |
| That cleanup failing | Warning `Could not clean up the abandoned upload of content {ContentId} ({ExceptionType}); the reservation sweeper will remove it`; `AttachmentsSweeper` removes blob and reservation once the reservation is older than `UploadReservationLifetime` |
| Deleting the staged copy after a deduplication hit failing | Warning `Could not discard the uncommitted blocks of content {ContentId} …`; Blob Storage discards uncommitted blocks after seven days |
| The commit's response lost and the client library's retry meeting the blob its first attempt created | `BlobAlreadyExists` or `ConditionNotMet`; `BlockStagingStream` compares the committed block list with its own and succeeds when they are identical, otherwise the exception propagates (500, cleanup) |
| Final save failing after the commit | cleanup deletes the committed blob unless the save did reach the database (a `StoredContents` row with its id exists) |
| Two identical uploads racing | both commit their own copy and both rows stay valid (ADR-0022) |
| A deduplication candidate whose blob is missing, whose header names another content, or whose key id is not configured or carries different material | `StoredContentVerifier` logs Error `Stored content {ContentId} has no blob` or `The blob of stored content {ContentId} cannot be opened with the configured attachment keys` and skips it; when none of the three newest candidates opens, the upload commits its own copy |
| Link requested for content that cannot be opened | the same verifier logs the same Error; 404 `attachment.not-found`, no link is created |
| Blob missing on download | `StoredContentMissingException` (`BlobNotFound`, `ContainerNotFound`); Error `Stored content {ContentId} of attachment {AttachmentId} has no blob`; 404, no redemption |
| Envelope failing authentication when opened, or its key not configured | `EnvelopeFormatException`; Error `The blob of stored content {ContentId} of attachment {AttachmentId} failed envelope authentication and was not served`; 404, no redemption |
| A later chunk failing authentication | the headers are already sent, so the exception aborts the connection; the client receives fewer bytes than `Content-Length` announced |
| Any other storage error (network, 403 while a new role assignment propagates) | the exception reaches `GlobalExceptionHandler`: 500 `server.error`, no redemption |
| Storage unreachable | readiness reports `Degraded` through `storage:attachments`; every other route keeps working |
| Invalid attachment setting at startup | `AttachmentStorageInitializer` fails API startup with the validator's message naming the setting (`AttachmentSettingsStartupTests`) |
| Invalid attachment setting delivered by a refresh | `IOptionsMonitor<AttachmentsOptions>.CurrentValue` throws: uploads, the policy, new links and downloads answer 500 until the value is corrected |
| Sweeper iteration failing | Warning `The attachments sweep failed; it runs again at the next interval`; the host keeps running |

## Configuration

Section `Erp:Platform:Attachments` (`AttachmentsOptions`); the source of each value per environment is in the [configuration register](../configuration.md).

| Key | Default | Rule | Takes effect |
|---|---|---|---|
| `BlobServiceUri` | — | `https://<account>.blob.core.windows.net/`: absolute `https`, path `/`, no query or user info; exactly one of this and `EmulatorHost` | at the next start |
| `EmulatorHost` | — | `127.0.0.1`, `localhost`, `host.docker.internal` or a single-label name matching `^[a-z][a-z0-9-]{0,62}$`; host-local, never seeded | at the next start |
| `ContainerName` | `attachments` | 3 to 63 lowercase letters, digits and single hyphens, starting and ending with a letter or digit | at the next start |
| `MaxSizeBytes` | `26214400` (25 MiB) | 1 to 104 857 600 (`MaxSizeBytesCeiling`, 100 MiB) | on refresh (upload check, body limit, policy) |
| `AllowedContentTypes` | every checked type | `;`-separated, each one of the types in the table above | on refresh |
| `DownloadLinkLifetime` | `00:05:00` | 30 seconds to 1 hour | on refresh, for new links |
| `TransferTimeout` | `00:10:00` | 30 seconds to 10 minutes (the web rewrite gives up after 660 seconds) | at the next start (request timeout policy) |
| `UploadReservationLifetime` | `01:00:00` | 1 minute to 6 days, and at least twice `TransferTimeout` | at the next start |
| `SweepInterval` | `00:15:00` | 1 minute to 1 day | at the next start |
| `MaxConcurrentUploads` | `16` | 1 to 1024 | at the next start (rate-limiter policy) |
| `EncryptionKey` | — | required; `<key id>:<32 bytes in base64>`, key id 1 to 32 of `A-Z a-z 0-9 . _ -` | on refresh (`KeyRing`) |
| `RetiredEncryptionKeys` | — | `;`-separated entries of the same shape; an id may repeat only with identical material | on refresh |

## Keys

Each file gets its own random 32-byte data key; the key-encryption key wraps it inside the envelope header, whose layout ADR-0022 tabulates. `KeyRing` holds the current key (used to write) and the retired keys (used only to read), parsed once per options instance, so a rotation that the configuration refresh delivers applies without a restart. `StoredContents.KeyId` shows which key protects which content; content whose key is not configured, or whose key id names other material than the key that wrapped it, gets no download link and cannot be downloaded (404, Error log) and is never reused by deduplication. The key never appears in a log or an error message; the startup log names only its id (`Attachment storage ready at {ContainerUri} with encryption key {KeyId} …`). Creating, rotating and retiring keys is the [secrets runbook](../operations/runbooks/secrets.md), section 5f.

## Limits

| Limit | Value |
|---|---|
| File size | `MaxSizeBytes`, at most 100 MiB |
| Request body of the upload | `MaxSizeBytes` + 64 KiB (`UploadSizeLimit.MultipartAllowanceBytes`); every other route keeps `Erp:Platform:Host:MaxRequestBodyBytes` |
| Uploads in flight | `MaxConcurrentUploads` per API process, 16 by default; the next one answers 429 |
| Web rewrite buffer | `experimental.proxyClientMaxBodySize: "101mb"` for `next dev` and `next start` only (`frontend/apps/web/next.config.ts`, phases `PHASE_DEVELOPMENT_SERVER` and `PHASE_PRODUCTION_SERVER`); the image's standalone `server.js` keeps the 10 MB default recorded by `next build`, so the local containers, which have no edge proxy, cut off an upload over 10 MB sent through their web origin, while production's edge proxy sends `/api/*` straight to the API |
| Transfer time | `TransferTimeout`, at most 10 minutes |
| File name | 255 characters after sanitising, ending in an extension of the declared type; content type 127 characters |
| Type check | signatures: the first 8 KiB; text: every byte |
| Envelope chunk, block, read size | 64 KiB, 4 MiB, 64 KiB |
| Deduplication | the newest 3 candidates, each verified with one ranged read of at most 125 bytes (`EnvelopeHeader.MaxLength`) |
| Link token | 32 bytes (43 Base64Url characters); the `link` parameter at most 64 characters |
| Page size | 1 to 200, default 50 (the web page asks for 20) |
| Sweeper | every `SweepInterval`: up to 100 reservations; every unredeemed link that expired more than one hour ago (`LinkGracePeriod`) in one `ExecuteDelete` |
| Cleanup deadline | 30 seconds per failed upload |

## Telemetry

| Signal | What |
|---|---|
| Traces | the ASP.NET Core server span and the pipeline's command or query span; the client library's spans from the sources `Azure.Storage.Blobs.*` (`BlockBlobClient.StageBlock`, `BlockBlobClient.CommitBlockList`, …), and each storage request as an `HttpClient` span whose `url.full` names the container and the random blob name, with the whole query replaced by `*` by the .NET runtime's own URI redaction |
| Metrics | meter `Dewiride.Erp.Attachments` (instruments below) |
| Logs | `[LoggerMessage]` entries with ids, lengths and key ids only, never file names, tokens or content |

| Instrument | Type, unit | Tags | Recorded |
|---|---|---|---|
| `erp.attachments.uploads` | counter, `{upload}` | `erp.attachments.outcome`: `stored`, `deduplicated`, `rejected`; `erp.error.code` on `rejected` | every upload the uploader accepts or rejects through a result; the endpoint's own refusals (`attachment.multipart-required`, `attachment.file-missing`, `attachment.incomplete`), `request.too-large` and exceptions are not counted |
| `erp.attachments.upload.size` | histogram, `By` | — | the size of every accepted upload |
| `erp.attachments.downloads` | counter, `{download}` | `erp.attachments.outcome`: `served`, `refused` | every redemption attempt that passes request validation and ends in a download or a 404; a storage exception (500) is not counted |
| `erp.attachments.reservations.swept` | counter, `{reservation}` | — | reservations whose blobs the sweeper removed |
| `erp.attachments.links.purged` | counter, `{link}` | — | download links the sweeper removed after they expired unused |

The download token is in the query string of `GET …/content`. OpenTelemetry's ASP.NET Core instrumentation records `url.query` with every value replaced by `Redacted`; never set `OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION`. The framework's request-start and request-finish lines (category `Microsoft.AspNetCore.Hosting.Diagnostics`) print the query string unredacted, so that category stays at `Warning`: `appsettings.json` sets all of `Microsoft.AspNetCore` to `Warning`, and `appsettings.Development.json`, which raises `Microsoft.AspNetCore` to `Information`, keeps `Microsoft.AspNetCore.Hosting.Diagnostics` at `Warning`. `TelemetryTests.Get_AttachmentContent_ExportsNoSpanOrLogCarryingTheLinkToken` proves the server span carries `url.query` and that neither a span of the trace nor an exported log record carries the token; `Post_Attachment_ExportsTheBlobStorageSpansInTheUploadTrace` proves the storage spans join the upload's trace.

## Health

`storage:attachments` (tag `ready`) calls `GetProperties` on the container through `AttachmentBlobClients.Probe`, a client with no retries and `HealthEndpoints.CheckTimeout` (2 seconds) as network timeout. It is registered with failure status `Degraded`: an unreachable or missing container makes `/healthz/ready` answer 200 `Degraded` (503 only when another check is unhealthy), because only the attachment routes depend on it. With `AzureCliCredential`, the first check after `dotnet run` may report `Degraded` while the first token is obtained.

## Using attachments from another module

A business module references `Dewiride.Erp.BuildingBlocks.Attachments` (never the `Platform/Attachments` module) and injects the scoped `IAttachmentService`:

| Member | Returns |
|---|---|
| `GetUploadPolicy()` | `UploadPolicy(MaxSizeBytes, AllowedContentTypes)` |
| `UploadAsync(AttachmentUpload(fileName, contentType, stream), ct)` | `Result<AttachmentDetails>`: the checks, errors and deduplication of the upload flow above |
| `GetAsync(id, ct)`, `ListAsync(ListRequest, ct)` | the attachment, a page of attachments |
| `DeleteAsync(id, ct)` | soft-deletes the attachment |
| `CreateDownloadLinkAsync(id, ct)` | `DownloadLinkDetails(Token, ExpiresAt)` for the current actor; `attachment.not-found` when the attachment is unknown or deleted or its stored content cannot be opened |
| `OpenDownloadAsync(id, token, ct)` | `Result<AttachmentDownload>`: the decrypted stream, name, type and size; dispose it |

Rules for callers:

- Keep the `AttachmentId` on your own entity. Its `StronglyTypedIdConverter` is discovered because the id type lives in a referenced `Dewiride.Erp.*` assembly, so it maps to `uniqueidentifier` without configuration. Add no foreign key into `files`: a module's migrations never depend on another context's tables.
- Call `UploadAsync` outside your module's unit of work: it saves through `AttachmentsDbContext` in its own steps and streams for up to `TransferTimeout`, so inside a command handler of a module that owns a context it would hold that transaction open for the whole transfer, and an execution-strategy retry would run it again against a consumed stream. Upload first, then run the command that stores the id; when that command fails, delete the attachment it does not need.
- For files a person uploads, the web app posts to `/api/platform/attachments` (with `uploadFile` from `shared/api/upload.ts`) and sends the returned id with the business command; the platform route already carries the body limit, the transfer timeout and the multipart reader. A module that streams its own files into `UploadAsync` (a generated PDF, an imported statement) needs no transport of its own.
- `IAttachmentService` performs no authorization and knows nothing about which entity a file belongs to: before handing out an attachment, a link or a download, the owning module checks its own permission on its own entity. The platform routes enforce `platform.attachments.files.*` from the authentication phase; until then they are anonymous and every caller is the anonymous actor.
- Map a failed `Result` with `ToProblem()`; the codes are in `AttachmentErrors` (`attachment.not-found`, `.multipart-required`, `.file-missing`, `.file-name-invalid`, `.empty`, `.incomplete`, `.too-large`, `.unsupported-type`, `.extension-mismatch`, `.content-mismatch`, `.infected`).
- `MaxConcurrentUploads` caps the platform upload route only (its rate-limiter policy); `UploadAsync` itself does not count callers, so a module that streams its own files into it bounds its own concurrency.
- A virus scanner is a DI registration of `IAttachmentScanner`; `AttachmentUploader` takes it as an optional dependency, and without one every upload records `notScanned`.

## Tests

| Project | Covers |
|---|---|
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.UnitTests/Attachments` | envelope round trip, tampering and header parsing, key parsing and the key ring, the options validator, content checks and the extension of every checked type (`ContentTypesTests`), the streamed text check split at every position (`Utf8TextValidatorTests`), file names, `ReplayedHeadStream`, block staging against a recording block blob client (including a retried commit that meets its own blob, `BlockStagingStreamTests`), the metrics |
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.IntegrationTests/Attachments` (SQL Server and Azurite) | upload, deduplication, abort cleanup, large files across blocks and the exact size limit, scanning, list, delete, download links (tampered, other actor, expiry boundary, deleted attachment, missing or replaced blob), key rotation, the reservation sweep and the purge of unused links (`AttachmentsSweeperTests`), the emulator initializer and the health check |
| `Modules/Platform/Attachments/Tests/UnitTests` | the handlers, the multipart reader and `UploadSizeLimit` |
| `Modules/Platform/Attachments/Tests/IntegrationTests` | every route end to end (the extension and whole-file text checks in `UploadAttachmentTests`), the body limit on a real Kestrel listener, the transfer timeout and upload concurrency policies on the endpoints (`TransferPoliciesTests`), the 429 while the cap is reached (`UploadConcurrencyTests`, holding an upload in flight with `HeldScanner`), the feature gate, link expiry, stored-content damage, deduplication past a missing blob, a key id with other material or differing only in case, and link refusal for content that cannot be opened (`StoredContentTests`), and the scan hook |
| `Tests/Host` | startup refusal of invalid settings, readiness (healthy, and `Degraded` from the storage check only when storage is unreachable), the storage spans and that no span or log carries the link token; `Dewiride.Erp.Host.Migrator.IntegrationTests` proves the migrator creates the `files` tables without any attachment setting |
| `Tests/Architecture` | only `Storage.Blob` uses `Azure.Storage`; the attachment routes are the whitelisted anonymous ones |
| `frontend/e2e/tests/platform/attachments` | the page on desktop and mobile in both themes: upload through the file chooser and by drag and drop, the API's 415 reason, download without leaving the page, delete with focus on the list heading, pagination and malformed `?page=` values, a 12 MiB upload through the web origin, the disabled-flag and unavailable-API states |

Integration projects need `ERP_TEST_BLOB_EMULATOR_HOST` and a running Azurite ([testing](../guides/testing.md)); `BlobTestContainer` builds its client with `AttachmentBlobClients.ServiceVersion`, so the fixture and the product send the same pinned service version.
