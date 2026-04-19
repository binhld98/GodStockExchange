using System.Runtime.CompilerServices;
using GodStockExchange.Domain.Enums;
using GodStockExchange.Domain.Models;
using GodStockExchange.MatchingEngine.DataStructures;
using GodStockExchange.MatchingEngine.Models;
using static GodStockExchange.MatchingEngine.DataStructures.Index;

namespace GodStockExchange.MatchingEngine.Components;

public sealed class OneSideOrderBook
{
    #region Sentinel

    private const int DefaultPoolCapacity = 65_536;

    private const int DefaultMarketDepth = 10;

    #endregion

    #region Storage

    /// <summary>
    /// Shared node pool for all price levels.
    /// </summary>
    private readonly NodePool<Order> _pool;

    /// <summary>
    /// Flat array covering the entire valid price range.
    /// </summary>
    private readonly PriceLevel[] _levels;

    /// <summary>
    /// Maps OrderId to (LevelIndex, NodeIndex) for O(1) cancel/amend lookup.
    /// </summary>
    private readonly Dictionary<long, OrderLocator> _orderIndexes;

    /// <summary>
    /// For bids: +1, for asks: -1. Used to simplify comparison logic.
    /// </summary>
    private readonly int _direction;

    /// <summary>
    /// Lowest valid price ticks. Any order below this price ticks is rejected before reaching the book.
    /// </summary>
    private readonly long _floorPriceTicks;

    /// <summary>
    /// Number of discrete price points between floor and ceiling.
    /// </summary>
    private readonly long _priceTickRange;

    /// <summary>
    /// Index of the most aggressive non-empty price level.
    /// For bids, this is the highest index with orders; for asks, this is the lowest index with orders.
    /// </summary>
    private int _bestLevelIndex = NullIndex;

    #endregion

    #region Properties

    /// <summary>
    /// Current best price ticks, or <c>null</c> if the book is empty.
    /// </summary>
    public long? BestPriceTicks => PeekBestLevel()?.PriceTicks;

    /// <summary>
    /// Total number of resting orders on this book.
    /// </summary>
    public int OrderCount => _orderIndexes.Count;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new one-side order book with the specified parameters.
    /// </summary>
    /// <param name="side"></param>
    /// <param name="floorPriceTicks"></param>
    /// <param name="priceTickRange"></param>
    /// <param name="poolCapacity"></param>
    public OneSideOrderBook(OrderSide side, long floorPriceTicks, long priceTickRange, int poolCapacity = DefaultPoolCapacity)
    {
        _direction = side == OrderSide.Buy ? 1 : -1;
        _floorPriceTicks = floorPriceTicks;
        _priceTickRange = priceTickRange;

        _pool = new NodePool<Order>(poolCapacity);
        _levels = new PriceLevel[priceTickRange];
        _orderIndexes = new Dictionary<long, OrderLocator>(poolCapacity);

        for (int i = 0; i < priceTickRange; i++)
            _levels[i] = new PriceLevel(ToLevelPrice(i), _pool);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Returns the total quantity of all orders resting on this book.
    /// </summary>
    /// <returns></returns>
    public long GetTotalQty()
        => _levels.Sum(level => level.TotalQty);

    /// <summary>
    /// Returns the best price level without removing it, or <c>null</c> if the book is empty.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PriceLevel? PeekBestLevel()
        => _bestLevelIndex == NullIndex ? null : _levels[_bestLevelIndex];

    /// <summary>
    /// Determines if the given aggressor price ticks can match the best price ticks on this book.
    /// </summary>
    /// <param name="aggressorPriceTicks"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanMatch(long aggressorPriceTicks)
    {
        if (BestPriceTicks is null)
            return false;

        return  BestPriceTicks * _direction >= aggressorPriceTicks * _direction;
    }

    /// <summary>
    /// Returns a snapshot of the current market depth up to <paramref name="depth"/> levels, starting from the best price level.
    /// </summary>
    /// <param name="depth"></param>
    /// <returns></returns>
    public IReadOnlyList<SimplePriceLevelSummary> GetMarketDepthSnapshot(int depth = DefaultMarketDepth)
    {
        if (_bestLevelIndex == NullIndex)
            return [];

        var result = new List<SimplePriceLevelSummary>();

        for (int i = _bestLevelIndex; i >= 0 && i < _priceTickRange; i -= _direction)
        {
            if (result.Count >= depth)
                break;

            var level = _levels[i];
            if (!level.IsEmpty)
                result.Add(new SimplePriceLevelSummary(level.PriceTicks, level.TotalQty));
        }

        return result;
    }

    /// <summary>
    /// Adds a new order into the level at its price ticks.
    /// Internally updates the best price cursor if the new price is more aggressive.
    /// </summary>
    /// <param name="order"></param>
    /// <returns>
    /// The node index within the shared pool.
    /// </returns>
    public int Add(Order order)
    {
        int levelIndex = ToLevelIndex(order.PriceTicks);
        int nodeIndex = _levels[levelIndex].Enqueue(order);
        _orderIndexes.Add(order.OrderId, new OrderLocator(levelIndex, nodeIndex));

        // Update the cursor: for bids, a higher index is better; for asks, a lower index is better.
        if (_bestLevelIndex == NullIndex || levelIndex * _direction > _bestLevelIndex * _direction)
            _bestLevelIndex = levelIndex;

        return nodeIndex;
    }

    /// <summary>
    /// Cancel the order identified by <paramref name="orderId"/> and returns the cancelled order.
    /// Throws if the order is not found in the book.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Order Cancel(long orderId)
    {
        if (!TryCancel(orderId, out var cancelledOrder))
            throw new InvalidOperationException($"Order OrderId={orderId} not found for cancellation.");
        
        return cancelledOrder;
    }

    /// <summary>
    /// Attempts to cancel the order identified by <paramref name="orderId"/> and returns the cancelled order.
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="cancelledOrder"></param>
    /// <returns></returns>
    public bool TryCancel(long orderId, out Order cancelledOrder)
    {
        if (!_orderIndexes.TryGetValue(orderId, out var locator))
        {
            cancelledOrder = default;
            return false;
        }

        var level = _levels[locator.LevelIndex];
        if (level.IsEmpty)
        {
            cancelledOrder = default;
            return false;
        }

        cancelledOrder = _pool.GetValue(locator.NodeIndex).AsCancelled();
        level.RemoveAt(locator.NodeIndex);
        _orderIndexes.Remove(orderId);
        AttemptAdvanceBestIndex();

        return true;
    }

    /// <summary>
    /// Attempts to advance the best price cursor after a level drained  in O(k) - where k is the tick gap to the next occupied level.
    /// In practice, k is often 1 because price moves incrementally under normal market conditions.
    /// </summary>
    public void AttemptAdvanceBestIndex()
    {
        int levelIndex = _bestLevelIndex;
        _bestLevelIndex = NullIndex;

        for (int i = levelIndex; i >= 0 && i < _priceTickRange; i -= _direction)
        {
            if (!_levels[i].IsEmpty)
            {
                _bestLevelIndex = i;
                break;
            }
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Converts a price ticks to a zero-based price level index.
    /// </summary>
    /// <param name="priceTicks"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ToLevelIndex(long priceTicks)
        => (int)(priceTicks - _floorPriceTicks);

    /// <summary>
    /// Converts a zero-based price level index to actual price ticks.
    /// </summary>
    /// <param name="priceLevelIndex"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long ToLevelPrice(int priceLevelIndex)
        => priceLevelIndex + _floorPriceTicks;

    #endregion

    #region Private Types

    /// <summary>
    /// Compact value-type stored in the order indexes.
    /// Contains everything needed to locate an order in O(1) for cancel/ammend.
    /// </summary>
    /// <param name="LevelIndex"></param>
    /// <param name="NodeIndex"></param>
    private record struct OrderLocator(int LevelIndex, int NodeIndex);

    #endregion
}