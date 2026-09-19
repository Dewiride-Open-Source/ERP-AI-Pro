# Platform / SystemInfo

Reference module that proves the module anatomy end to end: one query, one endpoint, one contract, unit and integration tests.

| Aspect | Value |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Platform.SystemInfo` |
| Schema | none (`Schema: null`, no persistence) |
| Route group | `/api/platform/system-info` |
| Feature flag | `Erp.Modules.Platform.SystemInfo` |
| Contract | `ISystemInfoQueries` provides application name, version and start time to other modules and the web shell |
| Events | none |
