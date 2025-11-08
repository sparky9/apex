using ApexV2.Data.MarketData.Engine;

namespace ApexV2.Data.DataValidation;

public interface IQuoteValidator
{
    QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options);
}

public sealed class CompositeQuoteValidator : IQuoteValidator
{
    private readonly List<IQuoteValidator> _validators;
    public CompositeQuoteValidator(IEnumerable<IQuoteValidator> validators) { _validators = validators.ToList(); }
    public QuoteValidationResult Validate(Quote rawQuote, Quote? previous, DateTime now, MarketDataEngineOptions options)
    {
        Dictionary<string, object?> corrections = new();
        var working = rawQuote;
        foreach (var v in _validators)
        {
            var result = v.Validate(working, previous, now, options);
            if (result.Status == QuoteValidationStatus.Rejected)
                return result with { Corrections = new Dictionary<string, object?>(corrections) };
            if (result.Status == QuoteValidationStatus.Corrected)
            {
                foreach (var kv in result.Corrections)
                    corrections[kv.Key] = kv.Value;
                working = ApplyCorrections(working, result.Corrections);
            }
        }
        return corrections.Count == 0
            ? new QuoteValidationResult(QuoteValidationStatus.Valid, working.Symbol, working.Provider, null, corrections)
            : new QuoteValidationResult(QuoteValidationStatus.Corrected, working.Symbol, working.Provider, null, corrections);
    }

    private Quote ApplyCorrections(Quote q, IReadOnlyDictionary<string, object?> corr)
    {
        string symbol = q.Symbol;
        decimal last = q.Last;
        decimal open = q.Open;
        decimal high = q.High;
        decimal low = q.Low;
        decimal close = q.Close;
        long volume = q.Volume;
        if (corr.TryGetValue("Last", out var v) && v is decimal d) last = d;
        if (corr.TryGetValue("Open", out v) && v is decimal d2) open = d2;
        if (corr.TryGetValue("High", out v) && v is decimal d3) high = d3;
        if (corr.TryGetValue("Low", out v) && v is decimal d4) low = d4;
        if (corr.TryGetValue("Close", out v) && v is decimal d5) close = d5;
        if (corr.TryGetValue("Volume", out v) && v is long l) volume = l;
        return new Quote(symbol, last, open, high, low, close, volume, q.Timestamp, q.Provider, q.Currency);
    }
}
