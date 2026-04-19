# How to test a regex validator with property-based tests

Use `RegexGenerate.Matching` and `RegexGenerate.NotMatching` to prove that a validator accepts
every well-formed input and rejects every ill-formed one — without hand-writing examples.

## Install

```bash
dotnet add package Conjecture.Regex
```

## Write a positive property

A positive property proves that the validator never rejects what the regex allows.

Suppose you have a `ProductCode` DTO with a `[RegularExpression]` annotation:

```csharp
using System.ComponentModel.DataAnnotations;

public record ProductCode([RegularExpression(@"^[A-Z]{3}-\d{4}$")] string Value);
```

Write a property that feeds only matching strings:

```csharp
using Conjecture.Core;
using Conjecture.Regex;
using System.ComponentModel.DataAnnotations;

[Property]
public bool ProductCode_AcceptsAllMatchingValues()
{
    string value = DataGen.SampleOne(RegexGenerate.Matching(@"^[A-Z]{3}-\d{4}$"));
    IList<ValidationResult> results = [];
    bool isValid = Validator.TryValidateObject(
        new ProductCode(value),
        new ValidationContext(new ProductCode(value)),
        results,
        validateAllProperties: true);
    return isValid;
}
```

Conjecture generates hundreds of matching strings per run — boundary lengths, all-uppercase,
maximum digit values — and the property fails if the validator ever rejects one.

## Write a negative property

A negative property proves the validator never accepts what the regex disallows:

```csharp
[Property]
public bool ProductCode_RejectsAllNonMatchingValues()
{
    string value = DataGen.SampleOne(RegexGenerate.NotMatching(@"^[A-Z]{3}-\d{4}$"));
    IList<ValidationResult> results = [];
    bool isValid = Validator.TryValidateObject(
        new ProductCode(value),
        new ValidationContext(new ProductCode(value)),
        results,
        validateAllProperties: true);
    return !isValid;
}
```

> [!TIP]
> Run positive and negative properties in the same test class so a refactored validator
> that accidentally widens or narrows its regex is caught on both sides simultaneously.

## Use a compiled `Regex` for reuse

If the same pattern appears in both production code and tests, share the compiled instance:

```csharp
using System.Text.RegularExpressions;

// Shared, compiled regex — same object used by production code and tests
[GeneratedRegex(@"^[A-Z]{3}-\d{4}$")]
private static partial Regex ProductCodeRegex();

[Property]
public bool ProductCode_AcceptsMatchingValues()
{
    string value = DataGen.SampleOne(RegexGenerate.Matching(ProductCodeRegex()));
    return ProductCodeRegex().IsMatch(value);
}
```

Passing a `Regex` instance ensures that `RegexOptions` set on the compiled regex (such as
`IgnoreCase`) are carried through to the generator automatically.

## Use well-known pattern shortcuts

For common formats, skip the raw pattern and call the named factory method directly:

```csharp
[Property]
public bool EmailService_SendsToMatchingAddresses()
{
    string address = DataGen.SampleOne(RegexGenerate.Email());
    return new MailAddress(address) is not null;
}

[Property]
public bool OrderId_MustBeUuid()
{
    string id = DataGen.SampleOne(RegexGenerate.Uuid());
    return Guid.TryParse(id, out _);
}
```

Available shortcuts: `Email`, `Url`, `Uuid`, `IsoDate`, `CreditCard` — and `Not*` variants for each.

## See also

- [Reference: Regex strategies](../reference/regex-strategies.md) — full API surface
- [Explanation: How Conjecture.Regex works](../explanation/regex-engine.md) — parser, shrinking, and lookaround
