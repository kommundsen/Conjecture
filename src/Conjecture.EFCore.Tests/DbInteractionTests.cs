// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Linq;

using Conjecture.EFCore;
using Conjecture.Interactions;

namespace Conjecture.EFCore.Tests;

public class DbInteractionTests
{
    private sealed class Order
    {
        public int Id { get; set; }
    }

    [Fact]
    public void DbInteraction_IsIInteraction()
    {
        Assert.True(typeof(IInteraction).IsAssignableFrom(typeof(DbInteraction)));
    }

    [Fact]
    public void DbInteraction_HasResourceNameOpAndPayload()
    {
        Order payload = new() { Id = 1 };
        DbInteraction interaction = new("orders-db", DbOpKind.Add, payload);

        Assert.Equal("orders-db", interaction.ResourceName);
        Assert.Equal(DbOpKind.Add, interaction.Op);
        Assert.True(interaction.Payload is Order { Id: 1 });
    }

    [Fact]
    public void DbInteraction_OpKinds_HaveExpectedValues()
    {
        DbOpKind[] values = Enum.GetValues<DbOpKind>();

        Assert.Equal(5, values.Length);
        Assert.Contains(DbOpKind.Add, values);
        Assert.Contains(DbOpKind.Update, values);
        Assert.Contains(DbOpKind.Remove, values);
        Assert.Contains(DbOpKind.SaveChanges, values);
        Assert.Contains(DbOpKind.Query, values);
    }

    [Fact]
    public void DbInteraction_StructuralEquality()
    {
        Order payload = new() { Id = 42 };
        DbInteraction a = new("db", DbOpKind.Update, payload);
        DbInteraction b = new("db", DbOpKind.Update, payload);

        Assert.Equal(a, b);
    }

    [Fact]
    public void DbInteraction_NullPayload_AllowedForSaveChanges()
    {
        DbInteraction interaction = new("db", DbOpKind.SaveChanges, null);

        Assert.Null(interaction.Payload);
    }
}