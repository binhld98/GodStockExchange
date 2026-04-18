namespace GodStockExchange.MatchingEngine.DataStructures;

/// <summary>
/// Enumerator for iterating over continuous segments of allocated nodes in a <see cref="NodePool{T}"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct NodePoolEnumerator<T>
{
    private readonly NodePool<T> _pool;

    private int _next;

    private int _current;

    public readonly T Current => _pool.GetValue(_current);

    public NodePoolEnumerator(NodePool<T> pool, int startIndex)
    {
        _pool = pool;
        _next = startIndex;
        _current = Index.NullIndex;
    }

    /// <summary>
    /// Advances to the next allocated node in the pool.
    /// </summary>
    /// <returns></returns>
    public bool MoveNext()
    {
        _current = _next;

        if (_current == Index.NullIndex)
            return false;

        _next = _pool.GetNext(_current);

        return true;
    }
}