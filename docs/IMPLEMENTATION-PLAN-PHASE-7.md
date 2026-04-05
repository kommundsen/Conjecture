# Phase 7 Implementation Plan: Release Infrastructure (v0.6.0-alpha.1)

## Context

Phases 0–6 delivered the full Conjecture.NET engine: core generation and shrinking, rich strategies, production quality, developer tooling (Roslyn analyzers, source generator, framework adapters), stateful testing, targeted property testing, and structured logging. The framework has no published NuGet packages yet.

The versioning (MinVer), API tracking (PublicApiAnalyzers), and breaking-change detection (ApiCompat) infrastructure exists in ADRs and partial configuration, but the actual release pipeline is missing. Phase 7 builds the end-to-end release workflow: fix a MinVer bug, add SourceLink, add package metadata, create per-package READMEs, bundle analyzers/generator into Core, create the GitHub Actions release workflow, and rotate the PublicAPI files for the first alpha.

**Version target:** `0.6.0-alpha.1` — ~60% progress toward ADR-0004's 1.0.0 criteria.

**Packages (6):**

| Package | Type |
|---------|------|
| Conjecture.Core | Library (bundles analyzers + generator) |
| Conjecture.Xunit | Adapter |
| Conjecture.Xunit.V3 | Adapter |
| Conjecture.NUnit | Adapter |
| Conjecture.MSTest | Adapter |
| Conjecture.Mcp | dotnet tool |

## Dependency Graph

```
7.0 ADR-0038 (analyzer bundling) ────────────────────────────────────────┐
                                                                          │
7.1 Build infrastructure (MinVer + SourceLink) ──────────────────────────┤
         │                                                                │
7.2 Package metadata (global + descriptions + READMEs) ──────────────────┤
         │                                                                │
7.3 Analyzer bundling ───────────────────────────────────────────────────┤
         │                                                                │
7.4 Release workflow ────────────────────────────────────────────────────┘
         │
         v
7.5 PublicAPI rotation ──► Verification ──► Release tag
         │
         v
7.6 ApiCompat baseline (post-release)
```

## TDD Execution Plan

Each cycle: `/implement-cycle` (Red -> Green -> Refactor -> Verify -> Mark done). 10 sub-phases.

---

### 7.0 Pre-requisites

#### Cycle 7.0.1 -- ADR amendment: bundle analyzers into Core
- [x] `/decision` -- ADR-0038: Bundle analyzers + generator into Core package (supersedes ADR-0023)
  - **Decision**: ship `Conjecture.Analyzers`, `Conjecture.Analyzers.CodeFixes`, and `Conjecture.Generators` DLLs inside `Conjecture.Core.nupkg` under `analyzers/dotnet/cs/`
  - **Rationale**: `dotnet add package Conjecture.Core` gives users analyzers, code fixes, and source generator with zero additional configuration; better first-use experience than requiring a separate `Conjecture.Analyzers` install
  - **Mechanism**: `ProjectReference` with `ReferenceOutputAssembly="false" OutputItemType="Analyzer"` in `Conjecture.Core.csproj`; `IsPackable=false` on analyzer/generator projects so they don't ship as standalone packages
  - **Supersedes**: ADR-0023 (which specified a separate package)
  - Alternatives considered: separate `Conjecture.Analyzers` package (rejected: extra install step hurts first-use experience), opt-in via package tag (rejected: unnecessary complexity for majority use case)

---

### 7.1 Build Infrastructure

#### Cycle 7.1.1 -- MinVer PackageReference fix
- [x] `/implement-cycle`
  - **Impl** -- `src/Directory.Build.props`
    - Add `<PackageReference Include="MinVer" PrivateAssets="all" />` to the existing `ItemGroup Condition="'$(IsPackable)' != 'false'"`
  - **Verify** -- `dotnet build src/Conjecture.Core/Conjecture.Core.csproj -c Release`; version shows `0.0.0-alpha.0` (not `1.0.0`)

#### Cycle 7.1.2 -- SourceLink + deterministic builds
- [x] `/implement-cycle`
  - **Impl**
    - `src/Directory.Packages.props` -- add `<PackageVersion Include="Microsoft.SourceLink.GitHub" Version="9.0.0" />`
    - `src/Directory.Build.props` -- add to existing `PropertyGroup`: `<PublishRepositoryUrl>true</PublishRepositoryUrl>`, `<EmbedUntrackedSources>true</EmbedUntrackedSources>`, `<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>`
    - `src/Directory.Build.props` -- add `<PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />` to packable `ItemGroup`
  - **Verify** -- `dotnet build src/Conjecture.slnx -c Release` produces no SourceLink warnings

---

### 7.2 Package Metadata

#### Cycle 7.2.1 -- Global NuGet metadata
- [x] `/implement-cycle`
  - **Impl** -- `src/Directory.Build.props`
    - Add packable `PropertyGroup`: `Authors`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`, `PackageTags`, `PackageReadmeFile`
    - Add to packable `ItemGroup`: README fallback `<None Include="$(MSBuildThisFileDirectory)..\README.md" Pack="true" PackagePath="\" Visible="false" Condition="!Exists('$(MSBuildProjectDirectory)\README.md')" />`
  - **Verify** -- `dotnet build src/ -c Release` no warnings

#### Cycle 7.2.2 -- Per-project Description overrides
- [x] `/implement-cycle`
  - **Impl** -- add `<Description>` to each packable project's `PropertyGroup`:
    - `src/Conjecture.Core/Conjecture.Core.csproj` -- `Property-based testing for .NET, inspired by Hypothesis. Includes analyzers and source generator.`
    - `src/Conjecture.Xunit/Conjecture.Xunit.csproj` -- `xUnit v2 adapter for Conjecture.NET property-based testing`
    - `src/Conjecture.Xunit.V3/Conjecture.Xunit.V3.csproj` -- `xUnit v3 adapter for Conjecture.NET property-based testing`
    - `src/Conjecture.NUnit/Conjecture.NUnit.csproj` -- `NUnit adapter for Conjecture.NET property-based testing`
    - `src/Conjecture.MSTest/Conjecture.MSTest.csproj` -- `MSTest adapter for Conjecture.NET property-based testing`
    - `src/Conjecture.Mcp/Conjecture.Mcp.csproj` -- `MCP server exposing Conjecture.NET property-based testing tools to AI assistants`
  - **Verify** -- `dotnet build src/ -c Release` no warnings

#### Cycle 7.2.3 -- Per-package READMEs
- [x] `/implement-cycle`
  - **Impl** -- create `README.md` for each packable project (install command, minimal usage example, link to repo):
    - `src/Conjecture.Core/README.md` -- overview, `dotnet add package Conjecture.Core`, basic `[Property]` + `Generate.*` example, note about bundled analyzers
    - `src/Conjecture.Xunit/README.md` -- xUnit v2 install + `[Property]` example
    - `src/Conjecture.Xunit.V3/README.md` -- xUnit v3 install + `[Property]` example
    - `src/Conjecture.NUnit/README.md` -- NUnit install + `[Property]` example
    - `src/Conjecture.MSTest/README.md` -- MSTest install + `[Property]` example
  - Add `<None Include="README.md" Pack="true" PackagePath="\" />` to each of the above `.csproj` files (`Conjecture.Mcp` already has this)
  - **Verify** -- `dotnet pack src/Conjecture.slnx -c Release -o ./artifacts`; `unzip -l ./artifacts/Conjecture.Core.*.nupkg` shows `README.md`

---

### 7.3 Analyzer Bundling

#### Cycle 7.3.1 -- Bundle analyzers + generator into Core
- [x] `/implement-cycle`
  - **Impl**
    - `src/Conjecture.Core/Conjecture.Core.csproj` -- add three `ProjectReference` items:
      - `Conjecture.Analyzers` with `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`
      - `Conjecture.Analyzers.CodeFixes` with `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`
      - `Conjecture.Generators` with `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`
    - `src/Conjecture.Analyzers/AnalyzerReleases.Unshipped.md` -- move content to `AnalyzerReleases.Shipped.md`; reset Unshipped to header only
    - `src/Conjecture.Generators/AnalyzerReleases.Unshipped.md` -- move content to `AnalyzerReleases.Shipped.md`; reset Unshipped to header only
  - **Verify** -- `dotnet pack src/Conjecture.Core/Conjecture.Core.csproj -c Release -o ./artifacts && unzip -l ./artifacts/Conjecture.Core.*.nupkg | grep analyzers/` shows:
    - `analyzers/dotnet/cs/Conjecture.Analyzers.dll`
    - `analyzers/dotnet/cs/Conjecture.Analyzers.CodeFixes.dll`
    - `analyzers/dotnet/cs/Conjecture.Generators.dll`

---

### 7.4 Release Workflow

#### Cycle 7.4.1 -- GitHub Actions release workflow
- [ ] `/implement-cycle`
  - **Impl** -- `.github/workflows/release.yml`
    - Trigger: `on: push: tags: ['v*']`
    - `permissions: contents: write`
    - Steps: `actions/checkout@v6` with `fetch-depth: 0` (MinVer needs full history), `actions/setup-dotnet@v5` with `dotnet-version: '10.0.x'`
    - Build → test → pack → `dotnet nuget push` with `--skip-duplicate` → `softprops/action-gh-release@v2` with `prerelease: true` and `generate_release_notes: true`
  - **Verify** -- `dotnet build src/ -c Release` succeeds (workflow is inert until a tag is pushed)

---

### 7.5 API Surface Rotation

#### Cycle 7.5.1 -- PublicAPI rotation (all projects)
- [ ] `/implement-cycle`
  - **Impl** -- For each of Core, Xunit, Xunit.V3, NUnit, MSTest:
    - Append `PublicAPI.Unshipped.txt` content (excluding the `#nullable enable` header line) to `PublicAPI.Shipped.txt`
    - Reset `PublicAPI.Unshipped.txt` to contain only `#nullable enable`
  - **Verify** -- `dotnet build src/Conjecture.slnx -c Release` produces zero RS0016/RS0017 warnings

---

### 7.6 Post-Release Baseline

#### Cycle 7.6.1 -- ApiCompat baseline
- [ ] `/implement-cycle`
  - **Note**: run this cycle AFTER the first release tag has been pushed and artifacts verified on NuGet
  - **Impl** -- `src/api-baseline/`: copy Release DLLs for Core, Xunit, Xunit.V3, NUnit, MSTest from `bin/Release/net10.0/`
  - **Verify** -- baseline DLLs committed; `dotnet build src/ -c Release` uses them for breaking-change detection on future releases

---

## Key Constraints

- **MinVer requires `fetch-depth: 0`** in GitHub Actions -- MinVer walks git history to find the nearest tag; a shallow clone produces the wrong version
- **Release trigger is tag-only** -- `push: tags: ['v*']` aligns with MinVer (tag IS the version); prevents accidental releases on branch push
- **`--skip-duplicate`** on NuGet push -- prevents workflow failures if re-triggered after partial success
- **`-c Release` in workflow** -- ensures `ContinuousIntegrationBuild=true` takes effect (GitHub Actions sets `CI=true`)
- **`prerelease: true` on GitHub Release** -- all pre-1.0.0 releases are marked pre-release
- **Analyzer bundling before PublicAPI rotation** -- 7.3.1 before 7.5.1 to avoid analyzer ordering issues during build
- **ApiCompat baseline** (7.6.1) is post-release only -- no prior DLLs exist for the first release
- **`IsPackable=false`** on Analyzers/Generators/CodeFixes -- their DLLs go into Core via ProjectReference, not as standalone packages
- **`NUGET_API_KEY` secret** must be added to GitHub repo settings before the workflow can push to NuGet

## New ADRs Needed

- **ADR-0038** -- Bundle analyzers + generator into Core package (supersedes ADR-0023)

## Verification (End-to-End)

```bash
dotnet build src/Conjecture.slnx -c Release
dotnet test src/Conjecture.slnx -c Release --no-build
dotnet pack src/Conjecture.slnx -c Release --no-build -o ./artifacts

# Inspect Core package (should have analyzers + README + LICENSE-MIT.txt):
unzip -l ./artifacts/Conjecture.Core.*.nupkg | grep -E 'analyzers/|README|LICENSE'

# Inspect an adapter (should have README + LICENSE-MIT.txt):
unzip -l ./artifacts/Conjecture.Xunit.*.nupkg | grep -E 'README|LICENSE'
```

## Release Day Sequence

1. Implement Cycles 7.0.1 through 7.5.1 on `build/release-flow`
2. Merge `build/release-flow` → `main`
3. `git tag v0.6.0-alpha.1 main && git push origin v0.6.0-alpha.1`
4. Watch release workflow → verify NuGet + GitHub Release
5. Post-release: implement Cycle 7.6.1 (ApiCompat baseline), commit to `main`
