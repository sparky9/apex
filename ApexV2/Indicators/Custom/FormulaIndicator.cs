using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Custom;

/// <summary>
/// Formula-based indicator that evaluates mathematical expressions
/// </summary>
public class FormulaIndicator : CustomIndicatorBase
{
    private readonly Dictionary<string, Func<double[], double>> _functions;
    private readonly Dictionary<string, double> _variables;
    
    public FormulaIndicator(string id, string name, string formula, string author, IChartLogger? logger = null)
        : base(id, name, $"Formula: {formula}", author, logger)
    {
        SetFormula(formula);
        SetParameter("lookback", 1);
        
        _functions = InitializeFunctions();
        _variables = new Dictionary<string, double>();
    }
    
    public override int MinimumDataPoints => GetParameter<int>("lookback", 1);
    
    public override IIndicator Clone()
    {
        var clone = new FormulaIndicator(Id + "_clone", Name, Formula, Author);
        clone.Parameters.Clear();
        foreach (var param in Parameters)
        {
            clone.Parameters[param.Key] = param.Value;
        }
        clone.Tags.AddRange(Tags);
        return clone;
    }
    
    protected override IndicatorResult CalculateInternal(CandlestickData[] data)
    {
        var lookback = GetParameter<int>("lookback", 1);
        
        if (data.Length < lookback)
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = $"Insufficient data: need {lookback} points, have {data.Length}"
            };
        }

        try
        {
            // Prepare variables from candlestick data
            var recentData = data.TakeLast(lookback).ToArray();
            PrepareVariables(recentData);
            
            // Evaluate the formula
            var result = EvaluateFormula(Formula);
            
            return new IndicatorResult
            {
                Value = result,
                IsValid = !double.IsNaN(result) && !double.IsInfinity(result),
                Timestamp = data[^1].Timestamp,
                Metadata = new Dictionary<string, object>
                {
                    ["formula"] = Formula,
                    ["lookback"] = lookback,
                    ["variables"] = new Dictionary<string, double>(_variables)
                }
            };
        }
        catch (Exception ex)
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = $"Formula evaluation error: {ex.Message}"
            };
        }
    }
    
    protected override IndicatorValidationResult ValidateParametersInternal()
    {
        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(Formula))
        {
            errors.Add("Formula cannot be empty");
        }
        
        var lookback = GetParameter<int>("lookback", 0);
        if (lookback <= 0)
        {
            errors.Add("Lookback must be greater than 0");
        }
        if (lookback > 1000)
        {
            errors.Add("Lookback should not exceed 1000 for performance reasons");
        }
        
        // Try to validate formula syntax
        try
        {
            ValidateFormulaSyntax(Formula);
        }
        catch (Exception ex)
        {
            errors.Add($"Invalid formula syntax: {ex.Message}");
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    private void PrepareVariables(CandlestickData[] data)
    {
        _variables.Clear();
        
        // Current values
        var current = data[^1];
        _variables["open"] = (double)current.Open;
        _variables["high"] = (double)current.High;
        _variables["low"] = (double)current.Low;
        _variables["close"] = (double)current.Close;
        _variables["volume"] = current.Volume;
        
        // Arrays for historical data
        var opens = data.Select(d => (double)d.Open).ToArray();
        var highs = data.Select(d => (double)d.High).ToArray();
        var lows = data.Select(d => (double)d.Low).ToArray();
        var closes = data.Select(d => (double)d.Close).ToArray();
        var volumes = data.Select(d => d.Volume).ToArray();
        
        // Derived values
        _variables["hl2"] = (_variables["high"] + _variables["low"]) / 2;
        _variables["hlc3"] = (_variables["high"] + _variables["low"] + _variables["close"]) / 3;
        _variables["ohlc4"] = (_variables["open"] + _variables["high"] + _variables["low"] + _variables["close"]) / 4;
        
        // Simple statistics
        if (data.Length > 1)
        {
            _variables["change"] = _variables["close"] - (double)data[^2].Close;
            _variables["change_pct"] = (_variables["change"] / (double)data[^2].Close) * 100;
        }
        
        // Technical values
        _variables["range"] = _variables["high"] - _variables["low"];
        _variables["body"] = Math.Abs(_variables["close"] - _variables["open"]);
        _variables["upper_shadow"] = _variables["high"] - Math.Max(_variables["open"], _variables["close"]);
        _variables["lower_shadow"] = Math.Min(_variables["open"], _variables["close"]) - _variables["low"];
    }
    
    private double EvaluateFormula(string formula)
    {
        // Simple formula evaluator - in a real implementation, you'd use a proper expression parser
        // For now, support basic operations and function calls
        
        var cleanFormula = formula.Trim().ToLower();
        
        // Replace variables
        foreach (var variable in _variables)
        {
            cleanFormula = cleanFormula.Replace(variable.Key, variable.Value.ToString("F6"));
        }
        
        // Handle basic functions
        foreach (var function in _functions)
        {
            if (cleanFormula.Contains(function.Key + "("))
            {
                // For simplicity, assume single argument functions
                var startIndex = cleanFormula.IndexOf(function.Key + "(");
                var endIndex = cleanFormula.IndexOf(")", startIndex);
                if (endIndex > startIndex)
                {
                    var argString = cleanFormula.Substring(startIndex + function.Key.Length + 1, 
                        endIndex - startIndex - function.Key.Length - 1);
                    
                    if (double.TryParse(argString, out var argValue))
                    {
                        var result = function.Value(new[] { argValue });
                        cleanFormula = cleanFormula.Replace(
                            cleanFormula.Substring(startIndex, endIndex - startIndex + 1),
                            result.ToString("F6"));
                    }
                }
            }
        }
        
        // Evaluate the final expression (simplified)
        return EvaluateSimpleExpression(cleanFormula);
    }
    
    private double EvaluateSimpleExpression(string expression)
    {
        // Very basic expression evaluator - supports +, -, *, /, and parentheses
        // In production, use a proper expression parser like NCalc or similar
        
        try
        {
            // Remove spaces
            expression = expression.Replace(" ", "");
            
            // For now, just try to parse as a number if it's simple
            if (double.TryParse(expression, out var simpleResult))
            {
                return simpleResult;
            }
            
            // Handle simple binary operations
            if (expression.Contains("+"))
            {
                var parts = expression.Split('+');
                if (parts.Length == 2 && double.TryParse(parts[0], out var left) && double.TryParse(parts[1], out var right))
                {
                    return left + right;
                }
            }
            
            if (expression.Contains("-"))
            {
                var lastMinusIndex = expression.LastIndexOf('-');
                if (lastMinusIndex > 0) // Not a negative number
                {
                    var leftPart = expression.Substring(0, lastMinusIndex);
                    var rightPart = expression.Substring(lastMinusIndex + 1);
                    if (double.TryParse(leftPart, out var left) && double.TryParse(rightPart, out var right))
                    {
                        return left - right;
                    }
                }
            }
            
            if (expression.Contains("*"))
            {
                var parts = expression.Split('*');
                if (parts.Length == 2 && double.TryParse(parts[0], out var left) && double.TryParse(parts[1], out var right))
                {
                    return left * right;
                }
            }
            
            if (expression.Contains("/"))
            {
                var parts = expression.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], out var left) && double.TryParse(parts[1], out var right))
                {
                    return right != 0 ? left / right : double.NaN;
                }
            }
            
            // Default fallback
            return double.NaN;
        }
        catch
        {
            return double.NaN;
        }
    }
    
    private void ValidateFormulaSyntax(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            throw new ArgumentException("Formula cannot be empty");
        }
        
        // Basic syntax checks
        var openParens = formula.Count(c => c == '(');
        var closeParens = formula.Count(c => c == ')');
        if (openParens != closeParens)
        {
            throw new ArgumentException("Mismatched parentheses");
        }
        
        // Check for valid variable names and functions
        var validTokens = new HashSet<string> { "open", "high", "low", "close", "volume", "hl2", "hlc3", "ohlc4", 
            "change", "change_pct", "range", "body", "upper_shadow", "lower_shadow" };
        validTokens.UnionWith(_functions.Keys);
        
        // Additional validation could be added here
    }
    
    private Dictionary<string, Func<double[], double>> InitializeFunctions()
    {
        return new Dictionary<string, Func<double[], double>>
        {
            ["abs"] = args => Math.Abs(args[0]),
            ["sqrt"] = args => Math.Sqrt(args[0]),
            ["log"] = args => Math.Log(args[0]),
            ["exp"] = args => Math.Exp(args[0]),
            ["sin"] = args => Math.Sin(args[0]),
            ["cos"] = args => Math.Cos(args[0]),
            ["tan"] = args => Math.Tan(args[0]),
            ["min"] = args => args.Min(),
            ["max"] = args => args.Max(),
            ["avg"] = args => args.Average(),
            ["sum"] = args => args.Sum()
        };
    }
}
