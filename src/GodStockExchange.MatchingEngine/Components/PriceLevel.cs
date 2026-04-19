using System.Runtime.CompilerServices;
using GodStockExchange.Domain.Models;
using GodStockExchange.MatchingEngine.DataStructures;
using static GodStockExchange.MatchingEngine.DataStructures.Index;

namespace GodStockExchange.MatchingEngine.Components;

/// <summary>
/// Represents all resting orders at a single price point on one side of the order book.
/// Orders withing a level are stored in a FIFO queue implemented as an <see cref="NodePool{T}"/>.
/// The first order to arrive is the first to be matched (price-time priority).
/// </summary>
public sealed class PriceLevel
{
    #region Storage

    /// <summary>
    /// FIFO queue of orders at this price level, backed by the shared node pool.
    /// </summary>
    private readonly NodePool<Order> _pool;

    /// <summary>
    /// Index of the first node in the queue, or <see cref="NullIndex"/> if the queue is empty.
    /// </summary>
    private int _head = NullIndex;

    /// <summary>
    /// Index of the last node in the queue, or <see cref="NullIndex"/> if the queue is empty.
    /// </summary>
    private int _tail = NullIndex;

    #endregion

    #region Properties

    /// <summary>
    /// The price that all orders in this level are resting at, in minimum increment ticks.
    /// </summary>
    public long PriceTicks { get; }

    /// <summary>
    /// Sum of the remaining quantities of all orders in this price level.
    /// </summary>
    public long TotalQty { get; private set; }

    /// <summary>
    /// Number of individual orders resting at this price level.
    /// </summary>
    public int OrderCount { get; private set; }

    /// <summary>
    /// Indicates whether this price level has any resting orders or not.
    /// </summary>
    public bool IsEmpty => OrderCount == 0;

    #endregion
    
    #region Constructor

    /// <summary>
    /// Initializes a new price level, sharing the caller-provided node pool.
    /// </summary>
    /// <param name="priceTicks">The price that all orders in this level share.</param>
    /// <param name="pool">The node pool to use for storing orders.</param>
    public PriceLevel(long priceTicks, NodePool<Order> pool)
    {
        PriceTicks = priceTicks;
        _pool = pool;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Dequeues all orders and resests this price level to empty, returning all nodes to the shared pool.
    /// Useful for cleanup when a price is removed fromm the order book.
    /// </summary>
    public void Clear()
    {
        while (!IsEmpty)
            TryDequeue(out _);
    }

    /// <summary>
    /// Returns the order at the head of the FIFO (oldest = highetst priority) queue without removing it.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Order PeakHead()
        => _pool.GetValue(_head);

    /// <summary>
    /// Returns the order at the tail of the FIFO (newest = lowest priority) queue without removing it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Order PeakTail()
        => _pool.GetValue(_tail);

    /// <summary>
    /// Appends a new order to the end of the FIFO queue for this price level.
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public int Enqueue(Order order)
    {
        int index = _pool.AllocNode(order);
        _pool.SetPrev(index, _tail);

        if (_tail != NullIndex)
            _pool.SetNext(_tail, index);
        else
            _head = index;

        _tail = index;
        TotalQty += order.OrigQty;
        OrderCount++;

        return index;
    }

    /// <summary>
    /// Removes and returns the order at the head of the FIFO queue (oldest = highest priority).
    /// Throws if the queue is empty.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Order Dequeue()
    {
        if (!TryDequeue(out var order))
            throw new InvalidOperationException("Cannot dequeue from an empty price level.");
        
        return order;
    }

    /// <summary>
    /// Attempts to remove and return the order at the head of the FIFO queue without throwing if the queue is empty.
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public bool TryDequeue(out Order order)
    {
        if (_head == NullIndex)
        {
            order = default;
            return false;
        }

        order = _pool.GetValue(_head);
        TotalQty -= order.OrigQty;
        OrderCount--;
        UnlinkNode(_head);

        return true;
    }

    /// <summary>
    /// Applies a partial fill to the order at the head of the FIFO queue without freeing and re-inserting the node.
    /// </summary>
    /// <param name="filledQty"></param>
    public void ApplyPartialFillToHead(long filledQty)
    {
        var headOrder = PeakHead();
        _pool.SetValue(_head, headOrder.WithFill(filledQty));
        TotalQty -= filledQty;
    }

    /// <summary>
    /// Removes the order at <paramref name="index"/> from anywhere in the shared pool in O(1).
    /// The caller must ensure that the order at <paramref name="index"/> belongs to this price level, otherwise the integrity of this price level will be compromised.
    /// </summary>
    /// <param name="index"></param>
    public void RemoveAt(int index)
    {
        var order = _pool.GetValue(index);
        TotalQty -= order.OrigQty;
        OrderCount--;
        UnlinkNode(index);
    }

    /// <summary>
    /// Returns an enumerator to iterate through all orders resting at this price level from head to tail (time priority).
    /// </summary>
    /// <returns></returns>
    public NodePoolEnumerator<Order> GetEnumerator()
        => new(_pool, _head);

    #endregion

    #region Private Helpers

    /// <summary>
    /// Unlinks the node at <paramref name="index"/> from the FIFO queue and also frees and returns it to the shared pool.
    /// Note that only 4 pointers are touched: <c>_head</c>, <c>_tail</c>, and the neighbors' <c>Next</c> and <c>Prev</c>.
    /// </summary>
    /// <param name="index"></param>
    private void UnlinkNode(int index)
    {
        int next = _pool.GetNext(index);
        int prev = _pool.GetPrev(index);

        if (prev != NullIndex)
            _pool.SetNext(prev, next);
        else
            _head = next;

        if (next != NullIndex)
            _pool.SetPrev(next, prev);
        else
            _tail = prev;

        _pool.FreeNode(index);
    }

    #endregion
}