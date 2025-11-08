using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApexV2.Dashboard.Calendar
{
    public class AlphaVantageCalendarProvider : IEconomicCalendarProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AlphaVantageCalendarProvider> _logger;
        private readonly string? _apiKey;

        public string ProviderName => "Alpha Vantage";
        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        public AlphaVantageCalendarProvider(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AlphaVantageCalendarProvider> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["AlphaVantage:ApiKey"];
            
            _httpClient.BaseAddress = new Uri("https://www.alphavantage.co/");
        }

        public async Task<List<EconomicEvent>> GetEventsAsync(DateTime startDate, DateTime endDate)
        {
            var events = new List<EconomicEvent>();
            
            try
            {
                // Get earnings events
                var earnings = await GetEarningsAsync(startDate, endDate);
                events.AddRange(earnings);

                // Get economic indicators
                var indicators = await GetEconomicIndicatorsAsync(startDate, endDate);
                events.AddRange(indicators);

                _logger.LogInformation($"Retrieved {events.Count} total events from Alpha Vantage");
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting events from Alpha Vantage");
                throw;
            }
        }

        public async Task<List<EarningsEvent>> GetEarningsAsync(DateTime startDate, DateTime endDate, string? symbol = null)
        {
            try
            {
                if (!IsConfigured)
                {
                    _logger.LogWarning("Alpha Vantage API key not configured");
                    return new List<EarningsEvent>();
                }

                var url = $"query?function=EARNINGS_CALENDAR&apikey={_apiKey}";
                if (!string.IsNullOrEmpty(symbol))
                {
                    url += $"&symbol={symbol}";
                }

                _logger.LogInformation($"Fetching earnings from Alpha Vantage: {url}");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var earnings = ParseEarningsResponse(content);

                // Filter by date range
                var filteredEarnings = earnings.Where(e => 
                    e.EventDate >= startDate && e.EventDate <= endDate).ToList();

                _logger.LogInformation($"Retrieved {filteredEarnings.Count} earnings events");
                return filteredEarnings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting earnings from Alpha Vantage");
                return new List<EarningsEvent>();
            }
        }

        public async Task<List<EconomicIndicator>> GetEconomicIndicatorsAsync(DateTime startDate, DateTime endDate, string? country = null)
        {
            try
            {
                if (!IsConfigured)
                {
                    _logger.LogWarning("Alpha Vantage API key not configured");
                    return new List<EconomicIndicator>();
                }

                var indicators = new List<EconomicIndicator>();

                // Get various economic indicators
                var functions = new[]
                {
                    "REAL_GDP",
                    "INFLATION",
                    "UNEMPLOYMENT",
                    "FEDERAL_FUNDS_RATE",
                    "CPI",
                    "NONFARM_PAYROLL"
                };

                foreach (var function in functions)
                {
                    try
                    {
                        var url = $"query?function={function}&interval=monthly&apikey={_apiKey}";
                        
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var functionIndicators = ParseEconomicIndicatorResponse(content, function);
                            indicators.AddRange(functionIndicators);
                        }

                        // Add delay to respect API rate limits
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to get indicator: {function}");
                    }
                }

                // Filter by date range and country
                var filteredIndicators = indicators.Where(i => 
                    i.EventDate >= startDate && 
                    i.EventDate <= endDate &&
                    (string.IsNullOrEmpty(country) || i.Country?.Equals(country, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();

                _logger.LogInformation($"Retrieved {filteredIndicators.Count} economic indicators");
                return filteredIndicators;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting economic indicators from Alpha Vantage");
                return new List<EconomicIndicator>();
            }
        }

        public async Task<MarketHours> GetMarketHoursAsync(string market, DateTime date)
        {
            // Alpha Vantage doesn't provide market hours API, so we'll use default values
            var marketHours = new MarketHours
            {
                Market = market,
                Date = date,
                Timezone = "EST",
                IsOpen = IsMarketDay(date),
                IsHoliday = IsHoliday(date),
                UpdatedAt = DateTime.UtcNow
            };

            switch (market.ToUpper())
            {
                case "NYSE":
                case "NASDAQ":
                    marketHours.PreMarketOpen = new TimeSpan(4, 0, 0);
                    marketHours.MarketOpen = new TimeSpan(9, 30, 0);
                    marketHours.MarketClose = new TimeSpan(16, 0, 0);
                    marketHours.PostMarketClose = new TimeSpan(20, 0, 0);
                    break;
                
                case "TSX":
                    marketHours.PreMarketOpen = new TimeSpan(7, 0, 0);
                    marketHours.MarketOpen = new TimeSpan(9, 30, 0);
                    marketHours.MarketClose = new TimeSpan(16, 0, 0);
                    marketHours.PostMarketClose = new TimeSpan(17, 0, 0);
                    marketHours.Timezone = "EST";
                    break;
            }

            return await Task.FromResult(marketHours);
        }

        public async Task<List<EconomicEvent>> SearchEventsAsync(string searchTerm, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow.AddDays(30);

                var allEvents = await GetEventsAsync(startDate.Value, endDate.Value);
                
                var filteredEvents = allEvents.Where(e =>
                    e.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (e.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                    (e.Symbol?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                    (e.CompanyName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();

                return filteredEvents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching events with term: {searchTerm}");
                return new List<EconomicEvent>();
            }
        }

        private List<EarningsEvent> ParseEarningsResponse(string jsonContent)
        {
            var earnings = new List<EarningsEvent>();

            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                
                if (document.RootElement.TryGetProperty("data", out var dataElement))
                {
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        try
                        {
                            var earningsEvent = new EarningsEvent
                            {
                                Type = EventType.Earnings,
                                Status = EventStatus.Scheduled,
                                Importance = EventImportance.Medium,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            if (item.TryGetProperty("symbol", out var symbolElement))
                                earningsEvent.Symbol = symbolElement.GetString();

                            if (item.TryGetProperty("name", out var nameElement))
                                earningsEvent.CompanyName = nameElement.GetString();

                            if (item.TryGetProperty("reportDate", out var dateElement))
                            {
                                if (DateTime.TryParse(dateElement.GetString(), out var eventDate))
                                    earningsEvent.EventDate = eventDate;
                            }

                            if (item.TryGetProperty("fiscalDateEnding", out var fiscalElement))
                            {
                                if (DateTime.TryParse(fiscalElement.GetString(), out var fiscalDate))
                                {
                                    earningsEvent.Quarter = $"Q{(fiscalDate.Month - 1) / 3 + 1}";
                                    earningsEvent.FiscalYear = fiscalDate.Year;
                                }
                            }

                            if (item.TryGetProperty("estimate", out var estimateElement))
                            {
                                if (decimal.TryParse(estimateElement.GetString(), out var estimate))
                                    earningsEvent.EPSEstimate = estimate;
                            }

                            earningsEvent.Title = $"{earningsEvent.Symbol} Earnings Report";
                            earningsEvent.Description = $"Quarterly earnings report for {earningsEvent.CompanyName}";

                            earnings.Add(earningsEvent);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing individual earnings item");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing earnings response");
            }

            return earnings;
        }

        private List<EconomicIndicator> ParseEconomicIndicatorResponse(string jsonContent, string function)
        {
            var indicators = new List<EconomicIndicator>();

            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                
                if (document.RootElement.TryGetProperty("data", out var dataElement))
                {
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        try
                        {
                            var indicator = new EconomicIndicator
                            {
                                Type = EventType.EconomicIndicator,
                                Status = EventStatus.Completed,
                                Importance = GetIndicatorImportance(function),
                                IndicatorName = GetIndicatorDisplayName(function),
                                ReportingAgency = "U.S. Government",
                                Frequency = "Monthly",
                                Country = "United States",
                                Currency = "USD",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            if (item.TryGetProperty("date", out var dateElement))
                            {
                                if (DateTime.TryParse(dateElement.GetString(), out var eventDate))
                                    indicator.EventDate = eventDate;
                            }

                            if (item.TryGetProperty("value", out var valueElement))
                            {
                                if (decimal.TryParse(valueElement.GetString(), out var value))
                                    indicator.ActualValue = value;
                            }

                            indicator.Title = $"{indicator.IndicatorName} Report";
                            indicator.Description = $"Economic indicator: {indicator.IndicatorName}";
                            
                            // Only add future events for scheduling
                            if (indicator.EventDate > DateTime.UtcNow.AddDays(-30))
                            {
                                indicators.Add(indicator);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing individual indicator item");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing economic indicator response for {function}");
            }

            return indicators;
        }

        private EventImportance GetIndicatorImportance(string function)
        {
            return function switch
            {
                "REAL_GDP" => EventImportance.High,
                "INFLATION" => EventImportance.High,
                "UNEMPLOYMENT" => EventImportance.High,
                "FEDERAL_FUNDS_RATE" => EventImportance.High,
                "NONFARM_PAYROLL" => EventImportance.High,
                "CPI" => EventImportance.Medium,
                _ => EventImportance.Low
            };
        }

        private string GetIndicatorDisplayName(string function)
        {
            return function switch
            {
                "REAL_GDP" => "Real GDP",
                "INFLATION" => "Inflation Rate",
                "UNEMPLOYMENT" => "Unemployment Rate",
                "FEDERAL_FUNDS_RATE" => "Federal Funds Rate",
                "CPI" => "Consumer Price Index",
                "NONFARM_PAYROLL" => "Non-Farm Payroll",
                _ => function.Replace("_", " ")
            };
        }

        private bool IsMarketDay(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Saturday && 
                   date.DayOfWeek != DayOfWeek.Sunday && 
                   !IsHoliday(date);
        }

        private bool IsHoliday(DateTime date)
        {
            // Basic US market holidays - could be expanded
            var holidays = new[]
            {
                new DateTime(date.Year, 1, 1),   // New Year's Day
                new DateTime(date.Year, 7, 4),   // Independence Day
                new DateTime(date.Year, 12, 25)  // Christmas Day
            };

            return holidays.Any(h => h.Date == date.Date);
        }
    }
}
