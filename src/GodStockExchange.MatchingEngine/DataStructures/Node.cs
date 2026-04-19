namespace GodStockExchange.MatchingEngine.DataStructures;

/// <summary>
/// Represents a node in a doubly linked list, which can be used to implement the order book.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct Node<T>
{
    /// <summary>
    /// The data payload stored in this node.
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// Index of the next node, or <see cref="NodePool{T}.NullIndex"/> if this is the last node in the list.
    /// </summary>
    public int Next { get; set; }

    /// <summary>
    /// Index of the previous node, 
    /// or the next free node index when this node is in the free list,
    /// or <see cref="NodePool{T}.NullIndex"/> if this is the first node in the list.
    /// </summary>
    public int Prev { get; set; }

    /// <summary>
    /// <c>true</c> if this node is currently allocated in the list, <c>false</c> if it is available for use.
    /// </summary>
    public bool IsAllocated { get; set; }
}