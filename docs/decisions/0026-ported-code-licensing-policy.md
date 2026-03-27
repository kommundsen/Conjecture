# 0026. Ported Code Licensing Policy

**Date:** 2026-03-27
**Status:** Accepted

## Context

Conjecture.NET is licensed under MIT (ADR-0005). The Python Hypothesis library, which inspired this project, is licensed under MPL-2.0. MPL-2.0 is file-level copyleft: any file that is a derivative work of an MPL-2.0 source file must itself be distributed under MPL-2.0, but this obligation does not extend to other files in the same project.

Conjecture.NET is currently a clean-room implementation. However, future contributors may wish to port specific algorithms or data structures directly from the Python source. Without a documented policy, the correct licensing of such files is unclear.

## Decision

Any file in this repository that is derived directly from Python Hypothesis source code must:

1. Be distributed under MPL-2.0, not MIT.
2. Include the following header comment at the top of the file:

```
// This file is derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.
// Licensed under the Mozilla Public License 2.0: https://mozilla.org/MPL/2.0/
```

3. Preserve the original copyright notice.

All other files remain under the MIT License as stated in LICENSE.txt.

## Consequences

- Contributors who port code from Python Hypothesis know exactly what to do without needing to ask.
- The project remains legally compliant with MPL-2.0's file-level copyleft.
- The mixed MIT/MPL-2.0 model is well-supported — MPL-2.0 explicitly permits this (§ 3.3 "Distribution of a Larger Work").
- Users of Conjecture.NET as a library are unaffected: MPL-2.0 file-level copyleft does not propagate to their code.

## Alternatives Considered

- **Rewrite all ported code from scratch**: Avoids MPL-2.0 entirely but may produce inferior implementations for well-studied algorithms (e.g., specific shrink passes). Porting with attribution is a better trade-off.
- **Relicense the entire project under MPL-2.0**: Unnecessary — the project is predominantly original work. MIT is more appropriate for the ecosystem.
