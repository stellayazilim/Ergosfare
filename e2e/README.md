# Ergosfare end-to-end tests

Black-box tests that exercise Ergosfare the way a real application does: a small
clean-architecture web app wired from the **local `src/` projects** (not NuGet), driven
over HTTP and asserted with the JetBrains HTTP Client CLI (`ijhttp`).

This lives outside `test/` on purpose — it is a separate solution with its own runtime
dependencies (EF Core + SQLite, Stella.MinimalApi) and must not weigh on the unit-test
build.

## What it covers

The [Todo app](Stella.Ergosfare.E2E.Api) is deliberately split across assemblies so a run
exercises the cross-assembly paths that unit tests don't:

| Layer | Project | What it proves |
|---|---|---|
| Domain | `…E2E.Domain` | plain entities, no framework |
| Contracts | `…E2E.Contracts` | commands / queries / events as messages |
| Use cases | `…E2E.UseCases` | handlers + a pre-interceptor, **one assembly away** from the Api |
| Infrastructure | `…E2E.Infrastructure` | EF Core + SQLite persistence |
| Api | `…E2E.Api` | Stella.MinimalApi endpoints, `RegisterGenerated()` |

A single run touches: a result-producing command, a result-less command, a single-result
query, a collection query, an event (broadcast to a handler in another assembly), a
pre-interceptor (trim + validate), domain-exception → HTTP status mapping, and EF Core
persistence — all discovered at compile time by the source generator scanning **referenced
assemblies**.

### The reserved-prefix opt-in

The app is named under the reserved `Stella.Ergosfare.*` prefix, which the source generator
skips during cross-assembly discovery by default. [`Directory.Build.props`](Directory.Build.props)
opts back in with `ErgosfareSourceGeneratorForceScanReferences=true`, so this run also
exercises the generator's force-scan path.

## Running

From the repo root:

```bash
task e2e
```

That builds and boots the app, waits for `/health`, runs [`http/todos.http`](http/todos.http)
with the [JetBrains HTTP Client CLI](https://www.jetbrains.com/help/idea/http-client-cli.html),
and tears the app down — failing the run on any failed assertion. The JUnit report is written
to `e2e/reports/`.

The CLI itself is self-provisioned: the first run downloads it from `https://jb.gg/ijhttp/latest`
into `e2e/tools/` (gitignored). `ijhttp` is a Java app, so a JDK 17+ must be installed
(`winget install Microsoft.OpenJDK.21` works); the runner finds it via `JAVA_HOME`, PATH,
or the standard install locations.

To iterate on the `.http` file by hand, run the app yourself and point your editor's HTTP
client at it:

```bash
dotnet run --project e2e/Stella.Ergosfare.E2E.Api
```
