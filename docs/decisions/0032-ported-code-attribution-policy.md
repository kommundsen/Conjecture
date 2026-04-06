# 0032. Ported Code Attribution Policy

**Date:** 2026-04-01
**Status:** Accepted (supersedes 0026)

## Context

ADR-0026 established a separate MPL-2.0 header for files ported from Python Hypothesis, while the rest of the project was MIT. Now that the entire repository is licensed under MPL-2.0 (ADR-0031), the licensing distinction is moot. However, provenance attribution for Hypothesis-derived code remains important for transparency and upstream credit.

## Decision

Files derived from Python Hypothesis source get an additional attribution comment below the standard project header:

```csharp
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.
```

A blank line (not `//`) separates the license header from the attribution. This ensures IDE0073 (`file_header_template`) only inspects the two-line license block and does not flag the attribution as a header mismatch.

All other files use only the standard two-line header.

## Consequences

- Clear provenance tracking for ported code without a separate licensing model.
- Contributors who port code know exactly what header to use.
- Upstream Hypothesis authors receive proper attribution.
- Attribution is IDE0073-compatible — no `.editorconfig` changes or per-file suppressions needed.
