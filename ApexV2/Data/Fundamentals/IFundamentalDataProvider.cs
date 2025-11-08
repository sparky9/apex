using System.Threading.Tasks;
using System.Collections.Generic;

namespace ApexV2.Data.Fundamentals;

/// <summary>
/// Contract for providers that can supply fundamental data (ratios, statements, company profile).
/// Separate from realtime market data providers so we can plug in dedicated sources (e.g. FinancialModelingPrep).
/// </summary>
public interface IFundamentalDataProvider
{
    string ProviderName { get; }
    bool RequiresApiKey { get; }

    Task<FundamentalSnapshot?> GetSnapshotAsync(string symbol, FundamentalDataScope scope = FundamentalDataScope.Core);
    Task<FinancialStatements?> GetFinancialStatementsAsync(string symbol);
    Task<CompanyProfile?> GetCompanyProfileAsync(string symbol);
    Task<IReadOnlyList<DividendRecord>> GetDividendHistoryAsync(string symbol, int years = 5);
}

/// <summary>
/// Defines how much data to pull for a snapshot (allows performance tuning & UI driven requests)
/// </summary>
public enum FundamentalDataScope
{
    Core,          // Core ratios (P/E, EPS, D/E, P/B, ROE, DividendYield, MarketCap)
    Extended,      // + Margins, Revenue growth, EBITDA, FCF
    Full           // + Per share stats, advanced quality metrics
}

public record FundamentalSnapshot(
    string Symbol,
    DateTime RetrievedUtc,
    CoreRatios Core,
    ExtendedRatios? Extended = null,
    FullRatios? Full = null
);

public record CoreRatios(
    decimal PERatio,
    decimal EPS,
    decimal DebtToEquity,
    decimal PriceToBook,
    decimal ReturnOnEquity,
    decimal DividendYield,
    decimal MarketCap
);

public record ExtendedRatios(
    decimal GrossMargin,
    decimal OperatingMargin,
    decimal NetMargin,
    decimal RevenueGrowthYear,
    decimal Ebitda,
    decimal FreeCashFlow
);

public record FullRatios(
    decimal CurrentRatio,
    decimal QuickRatio,
    decimal InterestCoverage,
    decimal AssetTurnover,
    decimal InventoryTurnover,
    decimal PiotroskiFScore
);

public record FinancialStatements(
    string Symbol,
    DateTime RetrievedUtc,
    IReadOnlyList<IncomeStatementPeriod> IncomeStatements,
    IReadOnlyList<BalanceSheetPeriod> BalanceSheets,
    IReadOnlyList<CashFlowStatementPeriod> CashFlows
);

public record IncomeStatementPeriod(DateTime PeriodEnd, decimal Revenue, decimal GrossProfit, decimal OperatingIncome, decimal NetIncome, decimal DilutedEPS);
public record BalanceSheetPeriod(DateTime PeriodEnd, decimal TotalAssets, decimal TotalLiabilities, decimal ShareholderEquity, decimal CashAndEquivalents, decimal LongTermDebt);
public record CashFlowStatementPeriod(DateTime PeriodEnd, decimal OperatingCashFlow, decimal InvestingCashFlow, decimal FinancingCashFlow, decimal FreeCashFlow);

public record CompanyProfile(string Symbol, string CompanyName, string Sector, string Industry, string Exchange, string Country, string Currency, string Description, DateTime RetrievedUtc);
public record DividendRecord(DateTime ExDate, DateTime PaymentDate, decimal Amount, string Currency);
