using System;
using ApexV2.Core.Logging;
using ApexV2.Data.DataValidation;
using ApexV2.Data.MarketData.Engine;
using FluentAssertions;
using Xunit;

namespace ApexV2.Tests.DataValidation;

public class QuoteValidatorTests
{
    private readonly MarketDataEngineOptions _options = new();
    private Quote NewQuote(decimal last, decimal open, decimal high, decimal low, decimal close, long vol, DateTime? ts=null)
        => new("TEST.TO", last, open, high, low, close, vol, (ts?? DateTime.UtcNow), "TestProvider");

    [Fact]
    public void PriceBoundsValidator_Rejects_NonPositive()
    {
        var v = new PriceBoundsValidator();
        var q = NewQuote(0m,0m,0m,0m,0m,0);
        v.Validate(q, null, DateTime.UtcNow, _options).Status.Should().Be(QuoteValidationStatus.Rejected);
    }

    [Fact]
    public void RangeCorrectionValidator_Corrects_Last_Below_Low()
    {
        var v = new RangeCorrectionValidator();
        var q = NewQuote(9m, 10m, 15m, 10m, 12m, 1000);
        var res = v.Validate(q, null, DateTime.UtcNow, _options);
        res.Status.Should().Be(QuoteValidationStatus.Corrected);
        res.Corrections.Should().ContainKey("Last").WhoseValue.Should().Be(10m);
    }

    [Fact]
    public void MonotonicVolumeValidator_Rejects_Decrease()
    {
        var v = new MonotonicVolumeValidator();
        var prev = NewQuote(10m,10m,12m,9m,10m,1000);
        var cur = NewQuote(10.5m,10m,12m,9m,10.5m,900); // lower volume
        v.Validate(cur, prev, DateTime.UtcNow, _options).Status.Should().Be(QuoteValidationStatus.Rejected);
    }

    [Fact]
    public void PriceSpikeValidator_Clamps_When_Above_Clamp_Threshold()
    {
        var v = new PriceSpikeValidator(clampPct:0.25m, hardRejectPct:0.60m);
        var prev = NewQuote(100m,100m,105m,95m,100m,1000);
        var cur = NewQuote(140m,100m,145m,95m,140m,1100); // 40% spike
        var res = v.Validate(cur, prev, DateTime.UtcNow, _options);
        res.Status.Should().Be(QuoteValidationStatus.Corrected);
        res.Corrections.Should().ContainKey("Last");
    }

    [Fact]
    public void PriceSpikeValidator_Rejects_Hard_Spike()
    {
        var v = new PriceSpikeValidator(clampPct:0.25m, hardRejectPct:0.60m);
        var prev = NewQuote(100m,100m,105m,95m,100m,1000);
        var cur = NewQuote(170m,100m,175m,95m,170m,1100); // 70% spike
        v.Validate(cur, prev, DateTime.UtcNow, _options).Status.Should().Be(QuoteValidationStatus.Rejected);
    }

    [Fact]
    public void TimestampFreshnessValidator_Rejects_Stale()
    {
        var v = new TimestampFreshnessValidator();
        var staleTs = DateTime.UtcNow - (_options.MaxQuoteStaleness + TimeSpan.FromMinutes(1));
        var q = NewQuote(10m,10m,12m,9m,10m,1000, staleTs);
        v.Validate(q, null, DateTime.UtcNow, _options).Status.Should().Be(QuoteValidationStatus.Rejected);
    }

    [Fact]
    public void Composite_Applies_Corrections_And_Preserves_Rejection_First()
    {
        var composite = QuoteValidatorFactory.CreateDefaultComposite();
        var bad = NewQuote(-1m, -1m, -1m, -1m, -1m, 0); // should reject immediately
        var res = composite.Validate(bad, null, DateTime.UtcNow, _options);
        res.Status.Should().Be(QuoteValidationStatus.Rejected);
    }
}
