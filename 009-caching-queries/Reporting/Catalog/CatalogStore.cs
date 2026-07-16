namespace Reporting.Catalog;

/// <summary>A tiny in-memory store of running catalog totals (stands in for an expensive read model).</summary>
public sealed class CatalogStore
{
    private readonly object _gate = new();
    private int _salesCount;
    private decimal _totalRevenue;

    public void RecordSale(decimal amount)
    {
        lock (_gate)
        {
            _salesCount++;
            _totalRevenue += amount;
        }
    }

    public (int SalesCount, decimal TotalRevenue) Snapshot()
    {
        lock (_gate)
        {
            return (_salesCount, _totalRevenue);
        }
    }
}

/// <summary>
/// Counts how many times the query handler actually executed. A cache HIT skips the handler, so this
/// counter is how the tests prove caching deterministically — without relying on timing.
/// </summary>
public sealed class QueryExecutionCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}
