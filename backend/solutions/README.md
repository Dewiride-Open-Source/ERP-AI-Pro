# Solution filters

One `.slnf` per domain keeps IDE loads small as the module count grows. Open a filter instead of the full solution when working inside one domain.

| Filter | Contents |
|---|---|
| `Identity.slnf` | building blocks, hosts, shared tests and the Identity modules |
| `Platform.slnf` | building blocks, hosts, shared tests and the Platform modules |

`dotnet build solutions/Platform.slnf` and `dotnet test --solution solutions/Platform.slnf` accept filters directly.

A filter lists every project outside `Modules/` and its domain's own, in the layout `JSON.stringify` writes with two spaces, which the module scaffolder keeps when it adds a module or a domain ([adding a module](../../docs/architecture/adding-a-module.md)).
