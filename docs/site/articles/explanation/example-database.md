# Understanding the example database

## The regression problem in property testing

Property tests run differently every time: each run generates fresh random inputs. This is good for exploration, but bad for regression detection. A test that fails with input `[2, 1]` on Monday might not rediscover that same failure on Tuesday — random generation might take thousands of runs to hit the same region again.

Traditional unit tests have no equivalent problem: you write the failing case as an `[Example]`, and it runs forever. Property tests need something analogous.

## What the database stores

When a property test fails and is shrunk to a minimal counterexample, Conjecture saves the **byte buffer** that produced that counterexample to a SQLite database.

It does not store the counterexample value itself. The byte buffer is the underlying representation that the engine reads to produce the value through the strategy. Replaying the same buffer through the same strategy always produces the same value — that's the guarantee the database relies on.

Storing the buffer rather than the value has two advantages:
- **Compactness** — a byte buffer for a complex object is typically much smaller than a serialized representation of the object
- **Framework independence** — the buffer doesn't depend on any serialization format, namespace, or type structure

## What happens on subsequent runs

1. Before generating any new random examples, Conjecture reads all stored buffers for this test from the database
2. Each buffer is replayed through the current strategy
3. If any replayed input fails, the test fails immediately with the stored counterexample
4. If all replayed inputs pass (because you fixed the bug), the test continues with fresh random generation

The stored example stays in the database permanently — it's a regression test. If you accidentally reintroduce the bug, the database catches it on the next run without waiting for random generation to rediscover it.

## Why SQLite

The database is a single SQLite file per test project. SQLite was chosen because:
- **No server required** — the database is a plain file that ships with your repo or lives in `.gitignore`
- **Fast reads** — replaying stored examples is negligible overhead
- **Wide support** — every CI environment can read it

The schema is simple: test name hash, byte buffer, and timestamp. The test name is hashed to avoid path-length issues with fully qualified names.

## The version control question

Whether to commit the database is a team decision with genuine tradeoffs:

**Commit it** — CI and teammates run the same regression tests. Any known failure is checked for every developer. The database grows over time as more failures are discovered and fixed.

**Ignore it** — Each developer and CI run starts fresh. Tests are stateless and independent. The database never grows. The downside: a fixed bug that gets reintroduced will take random generation time to rediscover.

**Hybrid** — Commit the database to your repo, but disable it in CI. Developers benefit from persistent regression tests; CI runs are stateless and fast.

There's no universal right answer. If your team has a discipline of fixing bugs rather than filtering them, committing the database gives the strongest regression guarantee. If your tests run for a long time and fresh random exploration is valuable, disabling the database in CI preserves that.

## Interaction with seeds

The database and the `Seed` setting are independent:

- A pinned `Seed` replays a specific failure via PRNG replay — the same buffer, the same value
- The database stores the buffer directly and replays it without the PRNG

When you fix a bug and remove the `Seed`, the database takes over: the stored buffer is replayed to verify the fix holds, even without the pinned seed.

## See also

- [How to manage the example database](../how-to/manage-example-database.md) — configuration and version control options
- [Reference: Settings](../reference/settings.md) — `Database`, `DatabasePath`
- [How to reproduce a failure](../how-to/reproduce-a-failure.md) — seed-based reproduction
