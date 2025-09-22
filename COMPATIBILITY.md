# Ergosfare Versioning & Backward Compatibility Policy


## 1. Scope

This document applies to all Ergosfare APIs, including:

- Commands, queries, and handlers
- Mediation strategies
- Snapshot and caching mechanisms
- Interceptors and oficial plugins

It defines how versions are incremented, backward compatibility is maintained, and obsolete APIs are handled.

## 2. Versioning Strategy

1. **Major releases (`vX.0.0`)**
    - Introduce breaking changes or obsolete APIs that may be removed.
    - Backward compatibility for previously stable APIs is guaranteed only for the 3-month period after marking them obsolete.

2. **Minor releases (`vX.Y.Z`)**
    - Add new features or enhancements.
    - Must **not introduce breaking changes** to stable APIs. Obsolete APIs remain functional.

3. **Patch releases (`vX.Y.Z+`)**
    - Bug fixes, performance improvements, and security patches only.
    - Do not introduce new APIs or remove existing ones.

**Note:** Ergosfare does **not follow a strict roadmap-based release schedule**. Release type is determined by **API changes**: if a breaking change occurs, the next major version may be released immediately.


## 3. Backward Compatibility

### 3.1 Definitions

* **Stable APIs**: Public types, methods, and properties that are published in a stable release (`vX.Y.Z`).
* **Obsolete APIs**: Stable APIs that are marked as obsolete in a stable release. This starts a **3-month backward compatibility window**.
* **Preview/RC APIs**: APIs released in preview or release candidate versions that are obsolete before becoming stable **do not receive backward compatibility guarantees**.

### 3.2 Obsolete API Lifecycle

1. When a new API is introduced or an existing API is being replaced, the old API may be marked as **obsolete** in a stable release.
2. Once marked obsolete in a stable release:

    * It **remains fully functional** during the backward compatibility window.
    * Developers are **advised to migrate** to the new API.
    * The 3-month timer starts **from the stable release date** where the API is marked obsolete.
3. After the backward compatibility window:

    * The obsolete API **may be removed in the next major release**.
    * Multiple major, minor or patch releases may occur within these 3 months; removal is only restricted by the backward compatibility duration.

### 3.3 Exceptions

* APIs obsolete in **preview or RC** versions **before a stable release** do **not** get any backward compatibility guarantee.
* Security patches may require breaking the obsolete API before the 3-month period; in such cases, migration is strongly recommended.

### 3.4 Example

* Suppose `v1.0.0` is stable.
* `v1.4.0` marks `OldMethod()` as obsolete.
* Backward compatibility period starts from `v1.4.0`.
* Any stable release (v1.4.x, v1.5.x, etc.) during the next 3 months must keep `OldMethod()` functional.
* After 3 months, the **next major release (v2.0.0 or later)** may remove `OldMethod()`.

