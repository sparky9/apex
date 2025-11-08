namespace ApexV2.Data.DataValidation;

public enum QuoteValidationStatus { Valid, Corrected, Rejected }

public record QuoteValidationResult(
    QuoteValidationStatus Status,
    string Symbol,
    string Provider,
    string? Reason,
    IReadOnlyDictionary<string, object?> Corrections
);
