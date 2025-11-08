using ApexV2.Core.Logging;
using ApexV2.Data.Fundamentals;
using FluentAssertions;
using Xunit;

namespace ApexV2.Tests.Fundamentals;

public class FundamentalDataEngineTests
{
    private LogManager CreateLogManager() => new() { MinimumLevel = LogLevel.Error };

    [Fact]
    public async Task Snapshot_Caches_Within_Ttl()
    {
        var provider = new InMemoryFundamentalProvider();
        var engine = new FundamentalDataEngine(provider, CreateLogManager(), new FundamentalDataEngineOptions { SnapshotTtl = TimeSpan.FromMinutes(10), MinRefreshInterval = TimeSpan.FromMinutes(1) });
        var s1 = await engine.GetSnapshotAsync("ABC");
        var s2 = await engine.GetSnapshotAsync("ABC");
        s2.Should().NotBeNull();
        ReferenceEquals(s1, s2).Should().BeTrue("should return cached instance");
        var metrics = engine.GetMetrics();
        metrics.CacheHits.Should().Be(1);
        metrics.CacheMisses.Should().Be(1);
        metrics.ProviderCalls.Should().Be(1);
    }

    [Fact]
    public async Task ForceRefresh_Bypasses_Cache()
    {
        var provider = new InMemoryFundamentalProvider();
        var engine = new FundamentalDataEngine(provider, CreateLogManager(), new FundamentalDataEngineOptions { SnapshotTtl = TimeSpan.FromMinutes(10) });
        var s1 = await engine.GetSnapshotAsync("XYZ");
        var s2 = await engine.GetSnapshotAsync("XYZ", forceRefresh:true);
        ReferenceEquals(s1, s2).Should().BeFalse();
        var m = engine.GetMetrics();
        m.ProviderCalls.Should().Be(2);
    }
}
