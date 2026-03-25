# 0005. License Choice

**Date:** 2026-03-25
**Status:** Accepted (supersedes MPL-2.0 decision)

## Context

Conjecture.NET is a .NET property-based testing library inspired by the Python Hypothesis library. The project is a clean-room C# implementation — no Python source code was ported. Choosing a license affects contributor agreements and how downstream projects may use the library.

## Decision

Use the MIT License.

## Consequences

- Maximum permissiveness; no restrictions on use in proprietary software.
- No requirement for downstream consumers or contributors to share modifications.
- Widest possible enterprise adoption — MIT is unambiguously acceptable in virtually all organizations.
- Aligns with the broader .NET ecosystem norm (most major .NET libraries use MIT).
- No viral or file-scoped copyleft obligations.

## Alternatives Considered

- **MPL-2.0**: File-scoped copyleft; requires modifications to covered files to be shared. Initially chosen to align with upstream Python Hypothesis, but since this is a clean-room implementation with a distinct identity (Conjecture.NET), that alignment is no longer a goal.
- **Apache-2.0**: Adds an explicit patent grant over MIT. The patent grant benefit is marginal for a testing library; MIT is simpler and equally permissive.
