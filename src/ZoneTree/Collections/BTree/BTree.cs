﻿using Tenray.ZoneTree.Collections.BTree.Lock;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Collections.BTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public partial class BTree<TKey, TValue>
{
    readonly ILocker TopLevelLocker;

    readonly int NodeSize = 128;

    readonly int LeafSize = 128;

    volatile Node Root;

    volatile LeafNode FirstLeafNode;

    volatile LeafNode LastLeafNode;

    public readonly IRefComparer<TKey> Comparer;
    
    volatile int _length;

    public int Length => _length;
    
    public readonly BTreeLockMode LockMode;

    public IIncrementalIdProvider OpIndexProvider { get; }

    public bool IsReadOnly { get; set; }

    public BTree(
        IRefComparer<TKey> comparer,
        BTreeLockMode lockMode,
        IIncrementalIdProvider indexOpProvider = null,
        int nodeSize = 128,
        int leafSize = 128)
    {
        NodeSize = nodeSize;
        LeafSize = leafSize;
        Comparer = comparer;
        OpIndexProvider = indexOpProvider ?? new IncrementalIdProvider();
        TopLevelLocker = lockMode switch
        {
            BTreeLockMode.TopLevelMonitor => new MonitorLock(),
            BTreeLockMode.TopLevelReaderWriter => new ReadWriteLock(),
            BTreeLockMode.NoLock or BTreeLockMode.NodeLevelMonitor or BTreeLockMode.NodeLevelReaderWriter => new NoLock(),
            _ => throw new NotSupportedException(),
        };
        LockMode = lockMode;
        Root = new LeafNode(GetNodeLocker(), leafSize);
        FirstLeafNode = Root as LeafNode;
        LastLeafNode = FirstLeafNode;
    }

    ILocker GetNodeLocker() => LockMode switch
    {
        BTreeLockMode.TopLevelMonitor or
        BTreeLockMode.TopLevelReaderWriter or 
        BTreeLockMode.NoLock => new NoLock(),

        BTreeLockMode.NodeLevelMonitor => new MonitorLock(),
        BTreeLockMode.NodeLevelReaderWriter => new ReadWriteLock(),
        _ => throw new NotSupportedException(),
    };

    public void WriteLock()
    {
        TopLevelLocker.WriteLock();
    }

    public void WriteUnlock()
    {
        TopLevelLocker.WriteUnlock();
    }

    public void ReadLock()
    {
        TopLevelLocker.ReadLock();
    }

    public void ReadUnlock()
    {
        TopLevelLocker.ReadUnlock();
    }

    public NodeIterator GetIteratorWithLastKeySmallerOrEqual(in TKey key)
    {
        try
        {
            ReadLock();
            var iterator = GetLeafNode(key).GetIterator(this);
            return iterator.SeekLastKeySmallerOrEqual(Comparer, in key);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public NodeIterator GetIteratorWithFirstKeyGreaterOrEqual(in TKey key)
    {
        try
        {
            ReadLock();
            var iterator = GetLeafNode(key).GetIterator(this);
            return iterator.SeekFirstKeyGreaterOrEqual(Comparer, in key);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenIteratorWithLastKeySmallerOrEqual(in TKey key)
    {
        var iterator = GetFrozenLeafNode(key).GetFrozenIterator();
        return iterator.SeekLastKeySmallerOrEqual(Comparer, in key);
    }

    public FrozenNodeIterator GetFrozenIteratorWithFirstKeyGreaterOrEqual(in TKey key)
    {
        var iterator = GetFrozenLeafNode(key).GetFrozenIterator();
        return iterator.SeekFirstKeyGreaterOrEqual(Comparer, in key);
    }
        
    public NodeIterator GetFirstIterator()
    {
        try
        {
            ReadLock();
            return FirstLeafNode.GetIterator(this);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenFirstIterator()
    {
        return FirstLeafNode.GetFrozenIterator();
    }

    public NodeIterator GetLastIterator()
    {
        try
        {
            ReadLock();
            return LastLeafNode.GetIterator(this);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenLastIterator()
    {
        return LastLeafNode.GetFrozenIterator();
    }

    public void SetNextOpIndex(long nextId) 
        => OpIndexProvider.SetNextId(nextId);

    public long GetLastOpIndex()
        => OpIndexProvider.LastId;
}