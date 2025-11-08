using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Core.Logging;
using ApexV2.Data.MarketData.Engine;
using ApexV2.Tests.MarketData;
using FluentAssertions;
using Xunit;

namespace ApexV2.Tests.MarketData;

public class MarketDataEngineTests
{
    private static string Normalize(string raw)
    {
        var s = raw.Trim().ToUpperInvariant();
        if (!s.EndsWith(".TO") && s.Length <= 5) s += ".TO";
        return s;
    }

    private LogManager CreateLogManager()
    {
        var lm = new LogManager { MinimumLevel = LogLevel.Error }; // keep quiet
        return lm;
    }

    [Fact]
    public async Task Subscribe_Unsubscribe_ReferenceCounting_Works()
    {
        var provider = new FakeMarketDataProvider();
        var engine = new MarketDataEngine(provider, CreateLogManager(), new MarketDataEngineOptions { PollInterval = TimeSpan.FromMilliseconds(50) });
        await engine.StartAsync();

        engine.Subscribe("ABC").Should().Be(1); // becomes ABC.TO internally
        engine.Subscribe("ABC").Should().Be(2);
        engine.Unsubscribe("ABC").Should().Be(1);
        engine.Unsubscribe("ABC").Should().Be(0);

        await engine.StopAsync();
    }

    [Fact]
    public async Task Engine_Publishes_Initial_And_Changed_Updates()
    {
        var provider = new FakeMarketDataProvider();
        var baseSymbol = "XYZ";
        var norm = Normalize(baseSymbol);
        provider.SetPrice(norm, 100m); // set price for normalized symbol
        var engine = new MarketDataEngine(provider, CreateLogManager(), new MarketDataEngineOptions { PollInterval = TimeSpan.FromMilliseconds(30) });
        await engine.StartAsync();
        engine.Subscribe(baseSymbol); // subscribes to XYZ.TO

        var received = new List<QuoteUpdate>();

        // Collect initial update
        var cts1 = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        try
        {
            await foreach (var upd in engine.GetUpdatesAsync(cts1.Token))
            {
                received.Add(upd);
                if (received.Count >= 1) break; // initial snapshot
            }
        }
        catch (OperationCanceledException) { }

        // Change price and collect diff update
        provider.SetPrice(norm, 101m);
        await Task.Delay(200); // allow poll cycle
        var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        try
        {
            await foreach (var upd in engine.GetUpdatesAsync(cts2.Token))
            {
                received.Add(upd);
                if (received.Any(r => r.Fields.HasFlag(QuoteUpdateFields.Last) && r.Last == 101m)) break;
            }
        }
        catch (OperationCanceledException) { }

        received.Should().NotBeEmpty();
        received[0].Fields.HasFlag(QuoteUpdateFields.All).Should().BeTrue();
        received.Any(r => r.Fields.HasFlag(QuoteUpdateFields.Last) && r.Last == 101m).Should().BeTrue();

        await engine.StopAsync();
    }

    [Fact]
    public async Task Metrics_Report_Success_After_Fetch()
    {
        var provider = new FakeMarketDataProvider();
        var baseSymbol = "AAA";
        var norm = Normalize(baseSymbol);
        provider.SetPrice(norm, 50m);
        var engine = new MarketDataEngine(provider, CreateLogManager(), new MarketDataEngineOptions { PollInterval = TimeSpan.FromMilliseconds(40) });
        await engine.StartAsync();
        engine.Subscribe(baseSymbol);
        await Task.Delay(180);
        var metrics = engine.GetMetrics();
        metrics.SuccessCount.Should().BeGreaterThan(0);
        metrics.HealthStatus.Should().Be(ProviderHealthStatus.Healthy);
        await engine.StopAsync();
    }
}
