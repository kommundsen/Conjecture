// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Money.Internal;

namespace Conjecture.Money.Tests;

public class Iso4217DataTests
{
    [Fact]
    public void DecimalPlacesByCurrency_ContainsAtLeast100Entries()
    {
        Assert.True(Iso4217Data.DecimalPlacesByCurrency.Count >= 100);
    }

    [Fact]
    public void DecimalPlacesByCurrency_JpyMapsToZero()
    {
        Assert.Equal(0, Iso4217Data.DecimalPlacesByCurrency["JPY"]);
    }

    [Fact]
    public void DecimalPlacesByCurrency_UsdMapsToTwo()
    {
        Assert.Equal(2, Iso4217Data.DecimalPlacesByCurrency["USD"]);
    }

    [Fact]
    public void DecimalPlacesByCurrency_BhdMapsToThree()
    {
        Assert.Equal(3, Iso4217Data.DecimalPlacesByCurrency["BHD"]);
    }

    [Fact]
    public void DecimalPlacesByCurrency_AllValuesAreInRange0To3()
    {
        foreach (KeyValuePair<string, int> entry in Iso4217Data.DecimalPlacesByCurrency)
        {
            Assert.True(entry.Value >= 0 && entry.Value <= 3,
                $"Currency {entry.Key} has decimal places {entry.Value}, expected 0–3");
        }
    }

    [Fact]
    public void DecimalPlacesByCurrency_DoesNotContainWithdrawnDem()
    {
        Assert.False(Iso4217Data.DecimalPlacesByCurrency.ContainsKey("DEM"));
    }

    [Fact]
    public void DecimalPlacesByCurrency_DoesNotContainWithdrawnFrf()
    {
        Assert.False(Iso4217Data.DecimalPlacesByCurrency.ContainsKey("FRF"));
    }
}