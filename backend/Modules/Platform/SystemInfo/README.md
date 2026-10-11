# Platform / SystemInfo

Reference module that proves the module anatomy end to end: one query, one persisted aggregate, two endpoints, its contracts, unit and integration tests.

| Aspect | Value |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Platform.SystemInfo` |
| Schema | `platform_system_info` (`SystemInfoDbContext`, table `Startups`) |
| Route group | `/api/platform/system-info` (`GET` identity and uptime; `GET /startups?take=` the most recent starts of the API, 1 to 100, 20 when `take` is absent, through the public request record `ListRecentStartupsRequest`) |
| Feature flag | `Erp.Modules.Platform.SystemInfo` |
| Permissions | `platform.system-info.info.read` (`GET /`) and `platform.system-info.startups.read` (`GET /startups`), declared in `SystemInfoPermissions` for Administrator alone; anyone else answers 403 `request.forbidden` ([ADR-0041](../../../../docs/adr/0041-roles-and-permissions.md)) |
| Contract | `ISystemInfoQueries` provides application name, version and start time to other modules and the web shell; `SystemInfoPermissions` names the module's permissions |
| Hosted service | `StartupRecorder` records the running API's own start once the host has started (skipped when the module flag is disabled) |
| Events | none |
