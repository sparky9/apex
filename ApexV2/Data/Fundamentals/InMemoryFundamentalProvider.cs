namespace ApexV2.Data.Fundamentals;

/// <summary>
/// Temporary simple provider implementation returning fabricated data (placeholder until real API wired).
/// Note: Will be removed once real provider integrated. Used only for engine test scaffolding, not production.
/// </summary>
public class InMemoryFundamentalProvider : IFundamentalDataProvider
{
    public string ProviderName => "InMemoryFundamentals";
    public bool RequiresApiKey => false;

    public Task<FundamentalSnapshot?> GetSnapshotAsync(string symbol, FundamentalDataScope scope = FundamentalDataScope.Core)
    {
        // Fabricated deterministic sample
        var core = new CoreRatios(
            PERatio: 15.2m,
            EPS: 3.45m,
            DebtToEquity: 0.40m,
            PriceToBook: 2.1m,
            ReturnOnEquity: 14.5m,
            DividendYield: 1.25m,
            MarketCap: 12_500_000_000m);
        ExtendedRatios? ext = null; FullRatios? full = null;
        if (scope is FundamentalDataScope.Extended or FundamentalDataScope.Full)
        {
            ext = new ExtendedRatios(0.52m, 0.23m, 0.18m, 0.08m, 1_200_000_000m, 750_000_000m);
        }
        if (scope == FundamentalDataScope.Full)
        {
            full = new FullRatios(1.8m, 1.4m, 12.5m, 0.65m, 4.2m, 7m);
        }
        var snap = new FundamentalSnapshot(symbol, DateTime.UtcNow, core, ext, full);
        return Task.FromResult<FundamentalSnapshot?>(snap);
    }

    public Task<FinancialStatements?> GetFinancialStatementsAsync(string symbol)
    {
        var now = DateTime.UtcNow;
        var income = new List<IncomeStatementPeriod>
        {
            new(now.AddMonths(-3), 2_500_000_000m, 1_300_000_000m, 600_000_000m, 420_000_000m, 0.85m),
            new(now.AddMonths(-6), 2_450_000_000m, 1_250_000_000m, 580_000_000m, 400_000_000m, 0.80m)
        };
        var balance = new List<BalanceSheetPeriod>
        {
            new(now.AddMonths(-3), 15_000_000_000m, 6_000_000_000m, 9_000_000_000m, 2_000_000_000m, 3_000_000_000m),
            new(now.AddMonths(-6), 14_800_000_000m, 5_900_000_000m, 8_900_000_000m, 1_900_000_000m, 3_100_000_000m)
        };
        var cash = new List<CashFlowStatementPeriod>
        {
            new(now.AddMonths(-3), 550_000_000m, -120_000_000m, -50_000_000m, 380_000_000m),
            new(now.AddMonths(-6), 530_000_000m, -110_000_000m, -60_000_000m, 360_000_000m)
        };
        var fs = new FinancialStatements(symbol, now, income, balance, cash);
        return Task.FromResult<FinancialStatements?>(fs);
    }

    public Task<CompanyProfile?> GetCompanyProfileAsync(string symbol)
    {
        var profile = new CompanyProfile(symbol, "Sample Corp", "Technology", "Software", "TSX", "CA", "CAD", "Sample description placeholder.", DateTime.UtcNow);
        return Task.FromResult<CompanyProfile?>(profile);
    }

    public Task<IReadOnlyList<DividendRecord>> GetDividendHistoryAsync(string symbol, int years = 5)
    {
        var list = new List<DividendRecord>
        {
            new(DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow.AddMonths(-2), 0.25m, "CAD"),
            new(DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-5), 0.24m, "CAD")
        } as IReadOnlyList<DividendRecord>;
        return Task.FromResult(list);
    }
}
