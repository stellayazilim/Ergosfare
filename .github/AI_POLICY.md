# AI Policy

Ergosfare is built with AI assistance, and this document assumes yours will be too. It is not
a permission slip and not a ban. It says what an assistant is here, what it is not allowed to
decide, and what a change has to carry before it lands.

## The four rules

### 1. An assistant, not an author

An AI is a co-developer at most: a tool that types quickly and reads widely. Responsibility for
a change belongs to the human developer who submits it, entirely and without dilution. "The
model wrote it" describes how a defect was produced; it never explains or excuses one.

Some tools add a `Co-Authored-By:` trailer automatically. Leave it if you like — it is a
provenance marker, not a claim of authorship and not a transfer of responsibility.

### 2. Architecture is the developer's decision

Where code goes, how it is shaped, which seam it attaches to, what the public surface becomes —
these are the developer's calls. An assistant may propose options and argue for one; it may
then *implement* the decision. It does not get to make it.

This matters most where it is least visible. A model asked to "make this work" will happily
invent a new lane beside an existing one, widen a public type, or add a dependency, and the
result compiles and passes. Decide the shape first, then let the tool fill it in.

### 3. Disclosure is recommended, not required

You are not obliged to declare AI assistance. You are encouraged to, and for one practical
reason: it tells a reviewer where the risk is concentrated. A reviewer who knows a 400-line
migration was model-generated reads it differently — and better — than one who assumes it was
typed by someone who held the whole thing in their head.

A sentence in the pull request is enough.

### 4. Every line is reviewed by a human

An assistant may write code. A maintainer or contributor reads all of it before it merges,
with the same eyes they would bring to a stranger's patch. Nothing lands because the tool
sounded confident, and nothing lands unread because it was "just a refactor".

---

## What review is looking for

The rules above are only as strong as the review behind rule 4. The rest of this document is
what that review checks.

### Claims must be traceable

Models produce fluent, plausible descriptions of behaviour that the code does not have. This
project has already had fabricated behaviour claims reach review; they were caught by reading
the code, not by the prose looking wrong. Treat prose as a hypothesis until something executes.

| Claim | What has to accompany it |
| --- | --- |
| "This behaves like X" | A test that fails without the change |
| "This is faster" | A measurement, with the benchmark's drift probes reported alongside |
| "This lane / this plan now serves the dispatch" | The lane-map diff, or an executable test that pins it |
| "This is what the runtime does today" | A file and a line, not a recollection |

Estimates are fine when labelled as estimates. "About 5 ns" with a measurement is a result;
"about 5 ns" without one is a guess wearing a result's clothes.

### Verification traps this repository has actually hit

Each of these produced a green-looking result that was false. They are cheap to re-check and
expensive to miss.

* **A commit is not your working tree.** `git commit <path>` records changes to *tracked* files
  only — a new file needs an explicit `git add`. A commit that omits one still builds for you,
  because the file is on your disk. Verify from a clean checkout:

  ```bash
  git worktree add --detach /tmp/verify HEAD && cd /tmp/verify && dotnet build && dotnet test
  ```

* **Filtered test output hides a suite that never ran.** Grepping for pass lines cannot show you
  a project that failed to build and was skipped. Check that every test assembly you expect
  appears, and check the exit code.

* **The solution build does not compile embedded source.** The generator tests compile C# held
  in string literals. A surface change can leave those broken while `dotnet build` reports
  success; only the generator suite catches it.

* **Generated code lives in the consumer's assembly.** Anything it binds to is public by
  necessity and is effectively ABI. Changing it is a compatibility decision — rule 2 territory,
  not an implementation detail.

* **A benchmark window drifts.** Compare rows within one run, and report the competitor probe
  rows so a reader can tell a real change from a noisy host.

## Provenance and licensing

* Do not paste code of unknown origin into this repository, whether a model or a search result
  produced it. Contributions are accepted under the [MIT License](../LICENSE); you are asserting
  you have the right to contribute what you submit.
* Do not add a dependency because a model suggested it. A dependency is a maintenance
  commitment and gets discussed on its own — rule 2 again.

## Secrets

Never paste credentials, tokens or private customer data into an AI tool. Local assistant state
stays out of version control — `.codex/` keeps its own `.gitignore` admitting only the shared
configuration for this reason.

## Scope

This policy covers documentation, changelog entries and benchmark reports as well as code. An
invented API name in a doc page costs a reader more than a bug costs a test, because nothing
executes a doc page.

---

If a rule here gets in the way of an obviously good change, say so in the pull request. The
policy is meant to catch confident mistakes, not careful ones.
