# Platform / SystemInfo

Reference module that proves the module anatomy end to end: one query, one persisted aggregate, two endpoints, one contract, unit and integration tests.

| Aspect | Value |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Platform.SystemInfo` |
| Schema | `platform_system_info` (`SystemInfoDbContext`, table `Startups`) |
| Route group | `/api/platform/system-info` (`GET` identity and uptime; `GET /startups` the 20 most recent starts of the API) |
| Feature flag | `Erp.Modules.Platform.SystemInfo` |
| Contract | `ISystemInfoQueries` provides application name, version and start time to other modules and the web shell |
| Hosted service | `StartupRecorder` records the running API's own start once the host has started (skipped when the module flag is disabled) |
| Events | none |
