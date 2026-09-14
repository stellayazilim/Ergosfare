# Ergosfare end-to-end tests

Black-box tests that exercise Ergosfare the way a real application does: a small
clean-architecture web app wired from the **local `src/` projects** (not NuGet), driven
over HTTP and asserted with the JetBrains HTTP Client CLI (`ijhttp`).

This lives outside `test/` on purpose — it is a separate solution with its own runtime
dependencies (SQLite, Stella.MinimalApi) and must not weigh on the unit-test
build.

## What it covers

The [Todo app](Stella.Ergosfare.E2E.Api) is deliberately split across assemblies so a run
exercises the cross-assembly paths that unit tests don't:

| Layer | Project | What it proves |
|---|---|---|
| Domain | `…E2E.Domain` | plain entities, no framework |
| Contracts | `…E2E.Contracts` | commands / queries / events as messages |
| Use cases | `…E2E.UseCases` | handlers, interceptors and `AddApplication()` selections; metadata-only generation |
| Infrastructure | `…E2E.Infrastructure` | SQLite persistence (ADO.NET) |
| Api | `…E2E.Api` | endpoints and executable plan generation from referenced selections |

A single run touches: a result-producing command, a result-less command, a single-result
query, a collection query, an event (broadcast to a handler in another assembly), a
pre-interceptor (trim + validate), domain-exception → HTTP status mapping, and SQLite
persistence — all discovered at compile time by the source generator scanning **referenced
assemblies**.

### Generator placement

`AddApplication()` and its `AddGenerated()` calls stay in UseCases. Module builders own
these public methods; consumers do not need a generated namespace or extra arguments.
UseCases runs the generator with `ErgosfareGeneratePlans=false`, exporting selection and
dispatch metadata only. The API runs the normal generator, follows `AddApplication()`'s
selection metadata and generates executable plans against the referenced handlers.
Merely referencing a configuration assembly does not select its participants.

### The reserved-prefix opt-in

The app is named under the reserved `Stella.Ergosfare.*` prefix, which the source generator
skips during cross-assembly discovery by default. [`Directory.Build.props`](Directory.Build.props)
opts back in with `ErgosfareSourceGeneratorForceScanReferences=true`, so this run also
exercises the generator's force-scan path.

## Run manually in Rider or a terminal

Use the command matching your terminal's working directory. Both start the same API at
**http://localhost:5099**. These commands work in PowerShell, cmd and bash.

### From the Ergosfare solution directory

Working directory: repository root, beside `Stella.Ergosfare.slnx`.

```sh
dotnet run --project ./examples/e2e/Stella.Ergosfare.E2E.Api/Stella.Ergosfare.E2E.Api.csproj --launch-profile http
```

### From the E2E solution directory

Working directory: `examples/e2e`, beside `Stella.Ergosfare.E2E.slnx`.

```sh
dotnet run --project ./Stella.Ergosfare.E2E.Api/Stella.Ergosfare.E2E.Api.csproj --launch-profile http
```

Wait for `Now listening on: http://localhost:5099` and `Application started`.
The terminal remaining open is normal: it is hosting the API. Open
[http://localhost:5099/health](http://localhost:5099/health); expect `{"status":"ok"}`.
The root `/` has no endpoint. Stop the API with Ctrl+C when finished.

In Rider, select the API project's **http** launch profile and press **Run/Debug**.
It uses the same port 5099 as the commands above. No Node.js process is involved. If an existing run
configuration launches the `.exe` directly instead of that profile, give it the application
arguments `--urls http://localhost:5099` and use the API project as its working directory.

### Streaming examples

With the API running, open [http://localhost:5099/streams](http://localhost:5099/streams)
or run the requests in [`http/streams.http`](http/streams.http) from Rider.

- `WEBSOCKET /streams/input`: send text frames followed by the reserved `__END__` frame.
  A single command dispatch consumes the input and returns the complete text once.
- `WEBSOCKET /streams/duplex`: each text frame produces the accumulated text immediately.
  `__END__` completes the input and closes the connection normally.
- `GET /streams/events`: returns `text/event-stream`. Eleven `character` events spell
  `Hello world`, with a 150 ms delay per character and a flush after every event. A `done`
  event finishes the response; browser clients should close their `EventSource` on it to
  prevent automatic reconnection.

The browser page sends Unicode code points one at a time and supports cancellation.
Rider supports both WebSocket requests (including `=== wait-for-server`) and SSE responses.
SignalR is a separate protocol and is not used by these examples.

Input uses the experimental `CommandStream<TChunk, TSelf>` and `QueryStream<TChunk, TSelf>`
contracts. Metadata is declared directly on the message. Handlers enumerate the message
itself; output is still an `IAsyncEnumerable<T>`. The input is limited to 16 KiB per
connection; binary frames are rejected.

```csharp
await using var input = new CollectText().Pipe(source, cancellationToken);
var text = await commands.SendAsync<string>(input, cancellationToken);

await using var query = new AccumulateText().Pipe(source, cancellationToken);
await foreach (var textSoFar in queries.StreamAsync(query, cancellationToken))
{
    // Output can arrive while input is still being produced.
}
```

`Pipe` starts producing immediately, including before dispatch. Capacity limits queued
items, not bytes or the memory reachable through an item. The producer waits when the queue
is full. Dispose an input that will not be dispatched; `IAsyncDisposable` belongs to the
root `ErgosfareStream` contract. Disposal stops production and awaits source cleanup.
Cancellation is cooperative; custom sources and converters must honor their token.

`Pipe(source, Func<T,T>)`, `TransformPipe<S>(source, Func<S,T>)` and
`IPipeConverter<S,T>` transform individual items. A `Func<T>` overload produces one output
per source item without receiving its value. For byte streams, `Func<byte,T>` converts each
byte independently; `IPipeConverter<T>` can instead decode a whole source incrementally.
`PipeConverters.Lines()` preserves UTF-8 decoding state and strips line terminators;
`PipeConverters.ByteBlocks(size)` produces independently owned memory blocks. Byte sources
remain caller-owned and open. Converters are helpers, not DI services.

Manual writers can use the explicit `IBufferWriter<T>` interface. Its staging buffer grows
to honor `GetMemory(sizeHint)` / `GetSpan(sizeHint)` independently of queue capacity.
`Advance` commits staging, `FlushAsync` publishes it with backpressure, and `Complete`
signals EOF after flushing. Manual writing cannot be combined with a bound pipe source.

Input-to-single-result
uses the ordinary command pipeline. In the current generated output-stream pipeline, pre runs
once when enumeration starts, post runs after successful exhaustion and receives the enumerator,
and failures bypass exception interceptors. Final interceptors observe failures and early output
enumerator disposal; pending input writers receive the terminal error. Early disposal uses
`StreamOutputDisposedException`, derived from `ExecutionAbortedException`. Explicit participant
aborts still skip finals. Per-item interception and dedicated stream interceptor signatures
remain stream-contract design questions.
The endpoint keeps its request scope alive while enumerating and propagates request cancellation.

For automated transport checks against an already running API (Node.js 22 or later):

```shell
# Repository root
node examples/e2e/scripts/test-streams.mjs

# E2E solution directory
node scripts/test-streams.mjs
```

An optional first argument changes the base URL, for example `http://localhost:5101`.
The script checks complete/empty input, incremental duplex responses, Unicode, isolated clients,
binary rejection, incremental SSE delivery, cancellation and the browser page. It does not start
the API; launch it directly with the dotnet commands above.

### Multipart file upload

Open **http://localhost:5099/streams/upload**, choose a large file and select **Start upload**.
The page uses a regular multipart form with browser upload progress and cancellation.
It stays HTML + JavaScript to preserve the API's NativeAOT configuration.

`POST /streams/upload` pipes the raw `Request.Body` into `UploadFile`; it never calls
`ReadFormAsync` or binds `IFormFile`. Header values are message metadata, without an
`HttpContext` or `IHttpContextAccessor` dependency. `ValidateUploadInterceptor` checks
headers, opens a `MultipartReader` over `message.AsStream()`, validates the first file
section and reads its first payload chunk. Missing or invalid file sections are rejected
before the handler runs; a present zero-length file is valid.

The pre interceptor stores the first chunk, open parser and section in `ErgosfareContext`.
The handler writes that chunk once, then continues reading the same section stream into
`examples/e2e/upload`. It never enumerates the message a second time. Multipart framing
and any parser read-ahead remain owned by the same reader. A final interceptor disposes
the stream adapter; failed pre-validation disposes its own adapter before propagating
the error. This pre stage deliberately advances the input, rather than being metadata-only.
There is one HTTP request and one dispatch per file, not an application-level resumable
upload protocol. HTTP transport framing is chosen by the client.

```csharp
await using var input = new UploadFile(http.Request.ContentType, http.Request.ContentLength)
    .Pipe(http.Request.Body, chunkSize: 65536, cancellationToken: http.RequestAborted);
var receipt = await mediator.SendAsync<UploadReceipt>(input, http.RequestAborted);
```

The converter reads at most 64 KiB per chunk; short reads may produce smaller chunks.
The input queue holds up to four chunks, with one in-flight item and stream buffers in
addition. File size does not determine the queue's memory footprint. Requests are limited
to 2 GiB including multipart framing. Files receive unique server-generated names;
the response contains the original display name, stored name, declared file MIME type,
byte/chunk counts and SHA-256. A companion `<storedName>.json` preserves the same metadata
on disk without altering the uploaded file. The MIME value comes from the file section's
`Content-Type`, including parameters; it is null when absent. It is not inferred from the
extension or verified by content sniffing. The outer `multipart/form-data` type is separate.
Cancellation and malformed bodies remove incomplete files. Successful uploads remain on
disk for inspection and are ignored by Git.

With the local API already running, run the 64 MiB transport test:

```shell
# Repository root
task e2e:test:upload
# Or without Task
node examples/e2e/scripts/test-upload.mjs

# From examples/e2e
task test:upload
node scripts/test-upload.mjs
```

Pass a different URL as the script's first argument, or after `--` with Task. The test
expects the API's local `examples/e2e/upload` directory. It pauses the HTTP producer before
EOF to assert that the handler is already writing to disk, compares the complete file's
SHA-256 and byte count (including the chunk handed off by pre), and checks missing file
fields, empty files, extra sections and cancellation/malformed-body cleanup. Valid PCM WAV
and UTF-8 samples are compared byte-for-byte after upload; their MIME values, SHA-256 and
persisted metadata are also checked, including absent MIME headers. Its successful test
file is removed afterward; manually uploaded files are preserved.

### Todo HTTP flow

Open [`http/run-all.http`](http/run-all.http) and press **Play** next to
`run ./todos.http`. That one action runs the complete flow in order, including creating
the todo before completing it. This uses Rider's [HTTP request include support](https://www.jetbrains.com/help/rider/Http_client_in__product__code_editor.html#import-http-requests-from-other-http-files).

Alternatively, open [`http/todos.http`](http/todos.http) and choose **Run All Requests**.
No HTTP Client environment selection is needed: the file declares `@host` itself.

`todoId` is returned by **Create a todo** and saved in the HTTP client's session. It is not
an environment setting. Clicking only **Complete it** in a fresh session cannot supply
that ID: run Create first, or use `run-all.http`. If the ID is missing, the pre-request
script now explains this before sending the request. A health check does not clear the ID.
Each full run creates a new todo; restarting Rider clears the HTTP client's saved ID.

The file path is `examples/e2e/http/todos.http` from the root solution, or `http/todos.http`
from the E2E solution. The URL is the same in both cases. Restarting the API recreates the
sample database, so rerun the whole HTTP file after a restart.

Requirements for manual use: .NET 10 SDK and Rider's HTTP Client (or another client that
supports JetBrains response-handler scripts). Node.js and Java are only needed for the
automated runner below.

## Start through Taskfile

From **either** solution directory, run:

```sh
task e2e
```

The root Taskfile includes the E2E Taskfile with `examples/e2e` as its working directory.
This task runs
`dotnet run --launch-profile http` directly and keeps the API running. Press Ctrl+C to
stop it. Execute `http/run-all.http` from Rider while the API runs. Node.js is not needed.

The E2E Taskfile also exposes the standalone solution build and stream checks:

| Operation | Repository root | `examples/e2e` |
|---|---|---|
| Start API | `task e2e:run` | `task run` |
| Build solution | `task e2e:build` | `task build` |
| Automated Todo suite | `task e2e:test` | `task test` |
| Stream checks against running API | `task e2e:test:streams` | `task test:streams` |

For another running API, append its URL: `task e2e:test:streams -- http://localhost:5101`.
The existing `task e2e` and `task e2e:test` commands remain available from both directories.
Project helper scripts live in `scripts/`; HTTP Client pre-request scripts live in
`scripts/http/`. HTTP request files remain in `http/`.

## Optional automated suite

Stop any manually started API on port 5099 first. The runner builds the API, starts its own
process, waits for `/health`, executes the same HTTP file and stops its process afterwards.
A failed build, startup or HTTP assertion produces a nonzero exit code.

From the **Ergosfare solution directory**:

```sh
node ./examples/e2e/scripts/run.mjs
```

From the **E2E solution directory**:

```sh
node ./scripts/run.mjs
```

`task e2e:test` runs this optional automation from either solution directory when Go Task
is installed. Unlike `task e2e`, it also starts and stops the API for the test run.
The runner resolves its files relative to `run.mjs`, so both commands write the report to
[`reports/report.xml`](reports/report.xml) under `examples/e2e`.

The automated runner requires .NET 10 SDK, Node.js and JDK 17+. The first run downloads
the JetBrains HTTP Client CLI (`ijhttp`) into `examples/e2e/tools/`. Set `JAVA_HOME` or put
Java on PATH; the runner also checks standard Windows JDK installation directories.

If port 5099 is busy, the runner fails immediately rather than testing a different running
application. Either stop that application or use the manual HTTP workflow above.
