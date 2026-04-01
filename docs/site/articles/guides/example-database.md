# Example Database

Conjecture can persist failing test inputs in a local SQLite database, ensuring known failures are always re-checked.

## How It Works

1. A `[Property]` test fails with some input
2. Conjecture saves the failing byte buffer to the database
3. On the next test run, stored examples are replayed **before** generating new random inputs
4. If the test now passes, the example stays in the database as a regression test
5. Future runs continue to replay it

This means once you find a bug, Conjecture re-checks it every time — even if random generation wouldn't rediscover it.

## Configuration

| Setting | Default | Description |
|---|---|---|
| `UseDatabase` | `true` | Enable/disable database persistence |
| `DatabasePath` | `".conjecture/examples/"` | Directory for database files |

### Disable Globally

```csharp
[assembly: ConjectureSettings(UseDatabase = false)]
```

### Disable Per-Test

```csharp
[Property(UseDatabase = false)]
public bool Stateless_test(int value) => /* ... */;
```

### Custom Path

```csharp
[assembly: ConjectureSettings(DatabasePath = ".test-data/conjecture/")]
```

## Version Control

### Option A: Commit the Database

Add `.conjecture/examples/` to your repo. Benefits:

- CI replays the same known failures
- Team members share discovered edge cases
- Failures become permanent regression tests

### Option B: Ignore the Database

Add to `.gitignore`:

```gitignore
.conjecture/
```

Each developer and CI run starts fresh. Useful if you want stateless, independent test runs.

### Option C: CI-Only Disable

Keep the database locally but disable in CI:

```csharp
#if CI
[assembly: ConjectureSettings(UseDatabase = false)]
#endif
```

Or set the database path to a temp directory via an environment variable in your CI config.

## Storage Format

The database is a SQLite file. Each entry stores:

- The fully qualified test method name
- The byte buffer that reproduces the failure
- A timestamp

The byte buffer is the raw input to Conjecture's engine — it's replayed through the same strategy to reproduce the exact value. This means the stored data is compact and framework-agnostic.
