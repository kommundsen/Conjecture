# How to manage the example database

Conjecture can persist failing test inputs in a local SQLite database, ensuring known failures are always re-checked on subsequent runs.

> [!NOTE]
> For background on why the database exists and what it stores internally, see [Understanding the example database](../explanation/example-database.md).

## How it works

1. A `[Property]` test fails with some input
2. Conjecture saves the failing byte buffer to the database
3. On the next run, stored examples are replayed **before** generating new random inputs
4. If the test now passes, the example stays as a permanent regression test
5. Future runs continue to replay it

## Configure the database path

The default path is `.conjecture/examples/`. Override it at the assembly or test level:

```csharp
// Assembly-level
[assembly: ConjectureSettings(DatabasePath = ".test-data/conjecture/")]

// Per-test
[Property(UseDatabase = true)]
public bool My_property(int value) => ...;
```

## Disable the database

**Globally** — for CI or stateless test runs:

```csharp
[assembly: ConjectureSettings(UseDatabase = false)]
```

**Per-test:**

```csharp
[Property(UseDatabase = false)]
public bool Stateless_test(int value) => ...;
```

**CI only:**

```csharp
#if CI
[assembly: ConjectureSettings(UseDatabase = false)]
#endif
```

## Version control options

### Option A: Commit the database

Add `.conjecture/examples/` to your repo:

- CI replays the same known failures
- Team members share discovered edge cases
- Failures become permanent regression tests

### Option B: Ignore the database

Add to `.gitignore`:

```gitignore
.conjecture/
```

Each developer and CI run starts fresh. Good for projects that want stateless, independent test runs.

### Option C: CI-only disable

Keep the database locally, disable in CI via an environment variable and a conditional directive (see above).

## Settings reference

| Setting | Type | Default | Description |
|---|---|---|---|
| `UseDatabase` | `bool` | `true` | Enable/disable database persistence |
| `DatabasePath` | `string` | `".conjecture/examples/"` | Directory for database files |

See [Reference: Settings](../reference/settings.md) for the full settings table.
