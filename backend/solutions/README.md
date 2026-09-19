# Solution filters

One `.slnf` per domain keeps IDE loads small as the module count grows. Open a filter instead of the full solution when working inside one domain.

| Filter | Contents |
|---|---|
| `Platform.slnf` | building blocks, hosts, shared tests and the Platform modules |

`dotnet build solutions/Platform.slnf` and `dotnet test --solution solutions/Platform.slnf` accept filters directly.
