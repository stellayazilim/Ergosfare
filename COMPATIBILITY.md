# Ergosfare Versioning & Compatibility Policy

Ergosfare optimizes for **correctness and development velocity** over bug-for-bug
compatibility. This document tells you exactly what you can rely on — and what you cannot.

> **SemVer deviations, stated up front.** Ergosfare uses SemVer-style version numbers but
> deliberately deviates from strict SemVer in two ways:
>
> 1. **Defective APIs may be fixed or removed in any release, without an obsolete step.**
>    Behavior that only exists because of a bug is not part of the contract.
> 2. **APIs marked `[Obsolete]` may be removed in a minor release** — at the earliest, the
>    minor release after the one that marked them, or the stable minor release whose preview deprecated or removed them (section 4).

## 1. Scope

This policy covers the supported public surface of all Ergosfare packages: message and
handler contracts, mediator facades, interceptors, mediation strategies, and module
registration APIs.

## 2. Surface tiers

| Tier | What | Promise |
|------|------|---------|
| **Stable** | Public APIs of the module packages (Commands, Queries, Events, Contracts) and the documented registration/dispatch surface | Covered by sections 3–4 |
| **Internal surface** | `Stella.Ergosfare.Core` / `Stella.Ergosfare.Core.Abstractions` implementation machinery — public only so first-party modules can consume it across assembly boundaries | **No promise.** May change in any release; not a third-party plugin contract |
| **Experimental** | Experimental APIs identified by `ERGOEXP` diagnostics (diagnostic IDs prefixed `ERGOEXP`) | **No promise.** May change or disappear in any release; consuming one is a compile-time error until you suppress its diagnostic — opting in is always deliberate |

## 3. Versioning rules

1. **Major releases (`vX.0.0`)** may change anything. **Major transitions sit entirely
   outside the compatibility promise.** A major version is a new line: migrate
   deliberately, or stay on the previous line — previous lines keep receiving fixes for
   as long as they are maintained, and staying on one is a fully supported choice.
2. **Minor releases (`vX.Y.0`)** add features and improvements. They do not break healthy,
   non-obsolete stable APIs — but they may (a) fix or remove **defective** APIs and
   (b) remove APIs deprecated in an earlier minor or deprecated/removed in a preview of that stable minor.
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

**Healthy but superseded APIs.** Deprecation messages identify the replacement or migration path. An API deprecated or removed in a preview may be absent from the corresponding stable minor release, even if no earlier stable release marked it `[Obsolete]`. A separate stable deprecation release is not required. APIs deprecated in an earlier stable minor may also be removed in a later minor. Patch releases do not remove healthy APIs.

A warning-free build against a previous stable release does not guarantee compatibility with the next minor. Review its preview migration notes before upgrading.

## 5. Experimental APIs

* Experimental APIs carry `ERGOEXP001`, `ERGOEXP002` or `ERGOEXP003` diagnostics and sit **entirely outside this policy**, including in stable releases.
* They may change or disappear in any release without a deprecation period.
* They use `[Obsolete(..., false, DiagnosticId = "ERGOEXP...")]` to emit a **warning**, not a default compilation error. The message identifies an experimental surface, not a discontinued API. IDEs may still display obsolete styling.
* Existing `#pragma warning disable ERGOEXP001` and `<NoWarn>` suppressions remain supported. Consumers that promote warnings to errors must suppress or downgrade the diagnostic themselves.
* Removing the experimental marker in a stable release graduates the API into the stable contract.
