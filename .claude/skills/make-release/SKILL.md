---
name: make-release
description: >
  Run the full release preparation checklist for Conjecture: promote PublicAPI files,
  update CHANGELOG, run tests, run benchmarks, update api-baseline DLLs, then output the
  commit message and tag commands. Use whenever the user wants to cut a new release.
  Triggers on "make-release", "prepare release", "cut a release", or "/make-release <version>".
---

# make-release

Prepare a Conjecture release. Run every step in order — stop and report if anything fails.

## Input

`/make-release <version>` — e.g. `/make-release v0.7.0-alpha.1`

The version must start with `v` and follow the project's MinVer tag convention.

## Steps

### 1. Create a release branch

```bash
git checkout -b release/<version>
```

e.g. `release/v0.7.0-alpha.1`. All subsequent changes are made on this branch.

### 2. Promote PublicAPI files

Find every `src/**/PublicAPI.Unshipped.txt` that contains more than just `#nullable enable`.

For each such file:
1. Read its entries (all lines after `#nullable enable`).
2. Append those entries to the matching `PublicAPI.Shipped.txt`.
3. Reset `Unshipped.txt` to exactly:
   ```
   #nullable enable
   ```

### 3. Update CHANGELOG.md

Open `CHANGELOG.md` and:
1. Replace the `## [Unreleased]` heading with `## [<version stripped of leading v>] — <today's date YYYY-MM-DD>`.
2. Insert a new empty `## [Unreleased]` section (with a blank line and `---` separator) above it.
3. Under the new version heading, summarise the public API additions drawn from the Unshipped entries you just promoted — group by project (`Core`, `Xunit`, `Formatters`, `Tool`, etc.) using bullet points. Keep it human-readable, not a raw API dump.

### 4. Run tests

```bash
dotnet test src/
```

- If the build fails, stop immediately and report the errors.
- If any test fails, stop immediately and report the failures.
- On success, report the total passed/skipped counts per assembly.

### 5. Run benchmarks (sanity check — not a hard gate)

Benchmarks take 30+ minutes. Run them in the background and poll for progress.

**5a. Start benchmarks in the background** using `run_in_background: true`:

```bash
cd src/Conjecture.Benchmarks && dotnet run -c Release -- --filter "*" --job short
```

**5b. Poll with Monitor every 30 seconds** — pass the background command's output path to Monitor and report progress to the user each time a new benchmark completes (look for lines like `| Method |` table rows or `// * Summary *` in the output). Keep reporting until the process finishes.

**5c. Once complete:**
- Summarise results: ops/sec and allocated bytes per operation for each benchmark.
- Flag any benchmark generating < 100k ops/sec or allocating > 1 KB/op as worth reviewing.
- Tell the user the results and ask whether to continue if anything looks regressed.

### 6. Update api-baseline DLLs

**Wait for explicit user approval before this step.** After presenting benchmark results in Step 5, ask:

> Benchmark results summarised above. Proceed to update api-baseline DLLs?

Do not continue until the user says yes (or equivalent). If the user says no or asks for changes, stop here.

The Release build from step 3/4 produces updated DLLs. Copy them:

```bash
cp src/Conjecture.Core/bin/Release/net10.0/Conjecture.Core.dll         src/api-baseline/
cp src/Conjecture.Xunit/bin/Release/net10.0/Conjecture.Xunit.dll       src/api-baseline/
cp src/Conjecture.Xunit.V3/bin/Release/net10.0/Conjecture.Xunit.V3.dll src/api-baseline/
cp src/Conjecture.NUnit/bin/Release/net10.0/Conjecture.NUnit.dll       src/api-baseline/
cp src/Conjecture.MSTest/bin/Release/net10.0/Conjecture.MSTest.dll     src/api-baseline/
```

If a Release build hasn't been produced yet (step 3 ran Debug only), trigger one first:
```bash
dotnet build src/ -c Release
```

### 7. Output the release commit message

Print a ready-to-copy commit message — do NOT commit:

```
Prepares <version> release

- Promotes PublicAPI.Unshipped → PublicAPI.Shipped across all packages
- Updates CHANGELOG for <version>
- Updates api-baseline DLLs to <version> build
```

### 8. Publish branch and open PR

After the user commits:

1. Push the branch:
   ```bash
   git push -u origin release/<version>
   ```
2. Open a PR against `main` using the repo's PR template (`.github/pull_request_template.md`). Read the template first, then fill it in with the release context:
   ```bash
   gh pr create --base main --head release/<version> --title "Release <version>" --body "$(cat <<'EOF'
   <filled-in template>
   EOF
   )"
   ```
3. Output the PR URL.

### 9. Output the tag commands

Once the PR is merged, print the commands to tag and trigger the release workflow:

```bash
git checkout main
git pull origin main
git tag <version>
git push origin <version>
```

Remind the user: pushing the tag triggers the GitHub Actions `release.yml` workflow — build → test → pack → NuGet push → GitHub Release (with auto-generated release notes).

## Guidelines

- Never commit or tag — committing, merging, and tagging are the user's responsibility. The skill pushes the branch and opens the PR.
- Never skip a step even if the user says "just do the important bits".
- If tests fail, do not proceed to benchmarks or baseline update.
- Always wait for explicit user approval (Step 6 gate) before updating api-baseline DLLs — even if benchmarks look fine.
