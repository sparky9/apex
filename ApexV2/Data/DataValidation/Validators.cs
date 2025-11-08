using ApexV2.Data.MarketData.Engine;

namespace ApexV2.Data.DataValidation;

public class PriceBoundsValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (rawQuote.Last <= 0 || rawQuote.Open < 0 || rawQuote.High < 0 || rawQuote.Low < 0 || rawQuote.Close < 0)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "Negative or zero price", new Dictionary<string, object?>());
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class HighLowConsistencyValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (rawQuote.High < rawQuote.Low)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "High < Low", new Dictionary<string, object?>());
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class RangeCorrectionValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (rawQuote.Last < rawQuote.Low)
        {
            var corrections = new Dictionary<string, object?> { { "Last", rawQuote.Low } };
            return new QuoteValidationResult(QuoteValidationStatus.Corrected, rawQuote.Symbol, rawQuote.Provider, "Last below Low corrected", corrections);
        }
        if (rawQuote.Last > rawQuote.High)
        {
            var corrections = new Dictionary<string, object?> { { "Last", rawQuote.High } };
            return new QuoteValidationResult(QuoteValidationStatus.Corrected, rawQuote.Symbol, rawQuote.Provider, "Last above High corrected", corrections);
        }
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class ZeroRangeValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (rawQuote.High == rawQuote.Low && rawQuote.High > 0)
        {
            // If last is outside, clamp
            if (rawQuote.Last != rawQuote.High)
            {
                var corrections = new Dictionary<string, object?> { { "Last", rawQuote.High } };
                return new QuoteValidationResult(QuoteValidationStatus.Corrected, rawQuote.Symbol, rawQuote.Provider, "Clamped last to zero-range high", corrections);
            }
        }
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class OHLCConsistencyValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (rawQuote.Last < rawQuote.Low || rawQuote.Last > rawQuote.High)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "Last outside range post-correction", new Dictionary<string, object?>());
        if (rawQuote.Open < rawQuote.Low || rawQuote.Open > rawQuote.High)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "Open outside range", new Dictionary<string, object?>());
        if (rawQuote.Close < rawQuote.Low || rawQuote.Close > rawQuote.High)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "Close outside range", new Dictionary<string, object?>());
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class VolumeSanityValidator : IQuoteValidator
{
    private readonly long _maxVolume;
    public VolumeSanityValidator(long maxVolume = 10_000_000) { _maxVolume = maxVolume; }
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (rawQuote.Volume < 0)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "Negative volume", new Dictionary<string, object?>());
        if (rawQuote.Volume > _maxVolume)
        {
            var corrections = new Dictionary<string, object?> { { "Volume", _maxVolume } };
            return new QuoteValidationResult(QuoteValidationStatus.Corrected, rawQuote.Symbol, rawQuote.Provider, "Volume capped", corrections);
        }
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class MonotonicVolumeValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (previous == null) return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
        if (rawQuote.Volume < previous.Volume)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "Volume decreased", new Dictionary<string, object?>());
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class PriceSpikeValidator : IQuoteValidator
{
    private readonly decimal _hardRejectPct; // e.g. 0.50 => 50%
    private readonly decimal _clampPct; // e.g. 0.25 => 25%
    public PriceSpikeValidator(decimal clampPct = 0.25m, decimal hardRejectPct = 0.60m)
    { _clampPct = clampPct; _hardRejectPct = hardRejectPct; }

    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (previous == null) return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
        if (previous.Last <= 0) return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
        var pct = Math.Abs((rawQuote.Last - previous.Last) / previous.Last);
        if (pct >= _hardRejectPct)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, $"Price spike {pct:P0}", new Dictionary<string, object?>());
        if (pct >= _clampPct)
        {
            // clamp to boundary
            var target = previous.Last * (rawQuote.Last > previous.Last ? (1 + _clampPct) : (1 - _clampPct));
            var corrections = new Dictionary<string, object?> { { "Last", decimal.Round(target, 4) } };
            return new QuoteValidationResult(QuoteValidationStatus.Corrected, rawQuote.Symbol, rawQuote.Provider, $"Price spike clamped {pct:P0}", corrections);
        }
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class TimestampFreshnessValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        var age = now - rawQuote.Timestamp.ToUniversalTime();
        if (age > options.MaxQuoteStaleness)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, $"Stale quote age={age.TotalSeconds:F1}s", new Dictionary<string, object?>());
        if (rawQuote.Timestamp.ToUniversalTime() - now > TimeSpan.FromSeconds(5))
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "Quote timestamp in future", new Dictionary<string, object?>());
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public class CrossFieldAnomalyValidator : IQuoteValidator
{
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        if (rawQuote.Open == 0 && rawQuote.High == 0 && rawQuote.Low == 0 && rawQuote.Close == 0 && rawQuote.Volume > 0)
            return new QuoteValidationResult(QuoteValidationStatus.Rejected, rawQuote.Symbol, rawQuote.Provider, "All OHLC zero but volume positive", new Dictionary<string, object?>());
        return new QuoteValidationResult(QuoteValidationStatus.Valid, rawQuote.Symbol, rawQuote.Provider, null, new Dictionary<string, object?>());
    }
}

public static class QuoteValidatorFactory
{
    public static IQuoteValidator CreateDefaultComposite()
    {
        return new CompositeQuoteValidator(new IQuoteValidator[]
        {
            new PriceBoundsValidator(),
            new HighLowConsistencyValidator(),
            new RangeCorrectionValidator(),
            new ZeroRangeValidator(),
            new OHLCConsistencyValidator(),
            new VolumeSanityValidator(),
            new MonotonicVolumeValidator(),
            new PriceSpikeValidator(),
            new TimestampFreshnessValidator(),
            new CrossFieldAnomalyValidator()
        });
    }
}
