namespace Oms.Domain.ValueObjects;

using Oms.Domain.Enums;

/// <summary>
/// Immutable value object capturing risk assessment results from external risk engine.
/// Once created, risk data cannot be modified; prevents business logic drift.
/// </summary>
public class RiskData
{
    public Guid RiskId { get; private set; }
    public RiskLevel Level { get; private set; }
    public decimal RiskScore { get; private set; } // 0-100
    public List<RiskIndicator> Indicators { get; private set; } = new();
    public DateTime EvaluatedAt { get; private set; }
    public string RiskEngineVersion { get; private set; } = string.Empty;
    public bool RequiresManualReview { get; private set; }

    // EF Core constructor
    private RiskData() { }

    public static RiskData Create(
        RiskLevel level,
        decimal riskScore,
        List<RiskIndicator> indicators,
        string engineVersion,
        bool requiresReview)
    {
        if (riskScore < 0 || riskScore > 100)
            throw new ArgumentException("Risk score must be between 0 and 100");
        
        return new RiskData
        {
            RiskId = Guid.NewGuid(),
            Level = level,
            RiskScore = riskScore,
            Indicators = indicators,
            EvaluatedAt = DateTime.UtcNow,
            RiskEngineVersion = engineVersion,
            RequiresManualReview = requiresReview
        };
    }

    public override bool Equals(object? obj) =>
        obj is RiskData other && RiskId == other.RiskId;
    
    public override int GetHashCode() => RiskId.GetHashCode();
}

/// <summary>Individual risk indicator flagged by risk engine</summary>
public class RiskIndicator
{
    public Guid IndicatorId { get; set; } = Guid.NewGuid();
    public string IndicatorType { get; set; } = string.Empty;
    public string RiskFactor { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool IsFlagged { get; set; }
}

/// <summary>
/// Immutable value object representing order data enriched from PIM.
/// Contains detailed product information and pricing after enrichment.
/// </summary>
public class EnrichedOrder
{
    public Guid OrderId { get; private set; }
    public List<EnrichedOrderItem> EnrichedItems { get; private set; } = new();
    public DateTime EnrichedAt { get; private set; }
    public string PimVersion { get; private set; } = string.Empty;
    public decimal EnrichedTotalPrice { get; private set; }

    // EF Core constructor
    private EnrichedOrder() { }

    public static EnrichedOrder Create(
        Guid orderId,
        List<EnrichedOrderItem> items,
        string pimVersion)
    {
        if (!items.Any())
            throw new ArgumentException("Enriched order must have at least one item");
        
        return new EnrichedOrder
        {
            OrderId = orderId,
            EnrichedItems = items,
            EnrichedAt = DateTime.UtcNow,
            PimVersion = pimVersion,
            EnrichedTotalPrice = items.Sum(i => i.EnrichedPrice)
        };
    }

    public override bool Equals(object? obj) =>
        obj is EnrichedOrder other && OrderId == other.OrderId;
    
    public override int GetHashCode() => OrderId.GetHashCode();
}

/// <summary>Enriched detail for a single order item from PIM</summary>
public class EnrichedOrderItem
{
    public Guid ItemId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public decimal EnrichedPrice { get; set; }
}
