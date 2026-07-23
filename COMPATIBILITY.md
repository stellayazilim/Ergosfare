# Ergosfare Versioning & Compatibility Policy

Ergosfare optimizes for **correctness and development velocity** over bug-for-bug
compatibility. This document tells you exactly what you can rely on — and what you cannot.

> **SemVer deviations, stated up front.** Ergosfare uses SemVer-style version numbers but
> deliberately deviates from strict SemVer in two ways:
>
> 1. **Defective APIs may be fixed or removed in any release, without an obsolete step.**
>    Behavior that only exists because of a bug is not part of the contract.
> 2. **APIs marked `[Obsolete]` may be removed in a minor release** — at the earliest, the
>    minor release after the one that marked them (section 4).

## 1. Scope

This policy covers the supported public surface of all Ergosfare packages: message and
handler contracts, mediator facades, interceptors, mediation strategies, and module
registration APIs.

## 2. Surface tiers

| Tier | What | Promise |
|------|------|---------|
| **Stable** | Public APIs of the module packages (Commands, Queries, Events, Contracts) and the documented registration/dispatch surface | Covered by sections 3–4 |
| **Internal surface** | `Stella.Ergosfare.Core` / `Stella.Ergosfare.Core.Abstractions` implementation machinery — public only so first-party modules can consume it across assembly boundaries | **No promise.** May change in any release; not a third-party plugin contract |
| **Experimental** | APIs marked `[Experimental]` (diagnostic IDs prefixed `ERGOEXP`) | **No promise.** May change or disappear in any release; consuming one is a compile-time error until you suppress its diagnostic — opting in is always deliberate |

## 3. Versioning rules

1. **Major releases (`vX.0.0`)** may change anything. **Major transitions sit entirely
   outside the compatibility promise.** A major version is a new line: migrate
   deliberately, or stay on the previous line — previous lines keep receiving fixes for
   as long as they are maintained, and staying on one is a fully supported choice.
2. **Minor releases (`vX.Y.0`)** add features and improvements. They do not break healthy,
   non-obsolete stable APIs — but they may (a) fix or remove **defective** APIs and
   (b) remove APIs that an earlier minor marked `[Obsolete]`.
3. **Patch releases (`vX.Y.Z`)** contain fixes only — including fixes that change
   defective behavior. **Patches never remove APIs.**
4. **Pre-releases (`vX.Y.Z-preview.N`)** carry no promises of any kind, including between
   two consecutive previews.

Releases are driven by API changes, not a calendar: a major version ships whenever a
breaking change is worth shipping.

## 4. API lifecycle

**Defective APIs.** An API that works incorrectly, is unsafe, or cannot fulfil its own
documented contract may be corrected or removed **immediately, in any release, without an
obsolete step**. Correctness beats compatibility; bug-for-bug compatibility is never kept.

**Healthy but superseded APIs** follow the deprecation lifecycle:

1. The API is marked `[Obsolete]`; the attribute message always names the replacement.
2. It remains present and functional for the rest of its minor line — **patches never
   remove APIs**.
3. **The next minor release is the earliest point it may be removed**; any later minor or
   major release may also remove it. There is no time-based window — an obsolete API may
   well survive longer, but plan as if it will not.

The compiler is the contract: aside from the defective-API exception, **a warning-free
build is safe through every patch update and through the next minor release.**

## 5. Experimental APIs

* APIs marked `[Experimental]` (diagnostic IDs prefixed `ERGOEXP`, e.g. `ERGOEXP001`) sit
  **entirely outside this policy** — even when they ship in a stable release.
* They may change or be removed in **any** release without an obsolete step.
* Consuming an experimental API is a **compile-time error** unless the consumer explicitly
  suppresses its diagnostic ID (e.g. `#pragma warning disable ERGOEXP001` or `<NoWarn>`).
* An experimental API graduates by having the attribute removed in a stable release; from
  that release on, it is a stable API covered by this policy.
