// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.EFCore;

using Xunit;

namespace Conjecture.EFCore.Tests;

public class DbInvariantExceptionHierarchyTests
{
    [Fact]
    public void DbInvariantException_TypeExists()
    {
        Type type = typeof(DbInvariantException);
        Assert.Equal("Conjecture.EFCore", type.Assembly.GetName().Name);
    }

    [Fact]
    public void DbInvariantException_IsNotSealed()
    {
        Assert.False(typeof(DbInvariantException).IsSealed);
    }

    [Fact]
    public void DbInvariantException_DerivesFromException()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(DbInvariantException)));
    }

    [Fact]
    public void DbInvariantException_Message_Roundtrips()
    {
        DbInvariantException ex = new("hello");
        Assert.Equal("hello", ex.Message);
    }

    [Fact]
    public void DbInvariantException_InnerException_Roundtrips()
    {
        DbInvariantException ex = new("outer", new InvalidOperationException("inner"));
        Assert.True(ex.InnerException is InvalidOperationException ioe && ioe.Message == "inner");
    }

    [Fact]
    public void RoundtripAssertionException_IsDbInvariantException()
    {
        Assert.True(typeof(DbInvariantException).IsAssignableFrom(typeof(RoundtripAssertionException)));
    }

    [Fact]
    public void MigrationAssertionException_IsDbInvariantException()
    {
        Assert.True(typeof(DbInvariantException).IsAssignableFrom(typeof(MigrationAssertionException)));
    }

    [Fact]
    public void Catch_DbInvariantException_HandlesBothDerivedTypes()
    {
        static void ThrowRoundtrip() => throw new RoundtripAssertionException("rt");
        static void ThrowMigration() => throw new MigrationAssertionException("mg");

        DbInvariantException caught1 = Assert.ThrowsAny<DbInvariantException>((Action)ThrowRoundtrip);
        Assert.Equal("rt", caught1.Message);

        DbInvariantException caught2 = Assert.ThrowsAny<DbInvariantException>((Action)ThrowMigration);
        Assert.Equal("mg", caught2.Message);
    }

    [Fact]
    public void RoundtripAssertionException_StillSealed()
    {
        Assert.True(typeof(RoundtripAssertionException).IsSealed);
    }

    [Fact]
    public void MigrationAssertionException_StillSealed()
    {
        Assert.True(typeof(MigrationAssertionException).IsSealed);
    }
}