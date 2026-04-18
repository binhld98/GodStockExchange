using System.Runtime.CompilerServices;

namespace GodStockExchange.MatchingEngine.DataStructures;

/// <summary>
/// Represents a high-performance, memory-efficient pool of reusable nodes for a doubly-lined list.
/// The pool handles allocation and deallocation of nodes, while the caller is responsible for linking nodes via <see cref="SetNext(int, int)"/> and <see cref="SetPrev(int, int)"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class NodePool<T>
{
    #region Storage

    private readonly Node<T>[] _nodes;

    private readonly int _capacity;

    #endregion

    #region Live List State

    /// <summary>
    /// Number of allocated nodes currently in use in the list. Always less than or equal to <see cref="_capacity"/>.
    /// </summary>
    public int Count { get; private set; } = 0;

    #endregion

    #region Free List State

    /// <summary>
    /// Index of the first free node in the free list, or <see cref="Index.Null"/> if there are no free nodes available.
    /// Nodes in the free list are linked together using their <see cref="Node{T}.Prev"/> field, which stores the index of the next free node.
    /// When <c>_freeListHead</c> is <see cref="Index.Null"/>, it indicates that the pool is exhausted and no more nodes can be allocated until some are freed back to the pool.
    /// </summary>
    private int _freeListHead = Index.Null;

    /// <summary>
    /// <c>true</c> if there are no allocated nodes in the list, <c>false</c> otherwise.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Number of free nodes remaining in the free list.
    /// </summary>
    public int FreeCapacity => _capacity - Count;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="NodePool{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of simultaneous allocated nodes. Choose a value that covers the peak demand.</param>
    public NodePool(int capacity)
    {
        _capacity = capacity;
        _nodes = new Node<T>[capacity];
        Clear();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Resets the pool to its initial state, clearing all allocated nodes and returning them to the free list.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _capacity - 1; i++)
        {
            _nodes[i].Value = default!;
            _nodes[i].Next = Index.Null;
            _nodes[i].Prev = i + 1;
            _nodes[i].IsAllocated = false;
        }

        _nodes[_capacity - 1].Value = default!;
        _nodes[_capacity - 1].Next = Index.Null;
        _nodes[_capacity - 1].Prev = Index.Null;

        _freeListHead = 0;
        Count = 0;
    }

    /// <summary>
    /// Retrieves the value stored in the node at <paramref name="index"/>.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValue(int index)
        => _nodes[index].Value;

    /// <summary>
    /// Sets the value stored in the node at <paramref name="index"/> to <paramref name="value"/>.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(int index, T value)
        => _nodes[index].Value = value;

    /// <summary>
    /// Retrieves the index of the node that follows the node at <paramref name="index"/> in the list.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetNext(int index)
        => _nodes[index].Next;

    /// <summary>
    /// Sets the node at <paramref name="next"/> to be successor of the node at <paramref name="index"/> in the list.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="next"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetNext(int index, int next)
        => _nodes[index].Next = next;

    /// <summary>
    /// Retrieves the index of the node that precedes <paramref name="index"/> in the list.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetPrev(int index)
        => _nodes[index].Prev;

    /// <summary>
    /// Sets the node at <paramref name="prev"/> to be the predecessor of the node at <paramref name="index"/> in the list.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="prev"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPrev(int index, int prev)
        => _nodes[index].Prev = prev;

    /// <summary>
    /// Allocates a new node from the free list, initializes it with the specified <paramref name="value"/>, and returns its index.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public int AllocateNode(T value)
    {
        if (_freeListHead == Index.Null)
            throw new InvalidOperationException($"Cannot allocate a new node, node pool capacity={_capacity} exhausted.");

        Count++;
        int allocatedIndex = _freeListHead;
        _freeListHead = _nodes[allocatedIndex].Prev;

        _nodes[allocatedIndex].Value = value;
        _nodes[allocatedIndex].Next = Index.Null;
        _nodes[allocatedIndex].Prev = Index.Null;
        _nodes[allocatedIndex].IsAllocated = true;

        return allocatedIndex;
    }

    /// <summary>
    /// Frees the node at <paramref name="index"/>, returning it to the free list and clears its value.
    /// </summary>
    /// <param name="index"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void FreeNode(int index)
    {
        if (!_nodes[index].IsAllocated)
            throw new InvalidOperationException($"Cannot free node at index={index} because it is already free.");

        Count--;
        _nodes[index].Value = default!;
        _nodes[index].Next = Index.Null;
        _nodes[index].Prev = _freeListHead;
        _nodes[index].IsAllocated = false;
        _freeListHead = index;
    }

    #endregion
}