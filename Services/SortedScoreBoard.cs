using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ScoreBoard.Models;

namespace ScoreBoard.Services;

/// <summary>
/// Thread-safe cache for the entire leaderboard, 
/// using ReaderWriterLockSlim for concurrency control and SortedSet to maintain sorted data.
/// </summary>
/// <typeparam name="long">CustomerId.</typeparam>
/// <typeparam name="CustomerScore">Class CustomerScore.</typeparam>
public class SortedScoreBoard
{

    private static readonly Lazy<SortedScoreBoard> _instance =
        new Lazy<SortedScoreBoard>(() => new SortedScoreBoard());

    public static SortedScoreBoard Instance => _instance.Value;

    private readonly SortedSet<CustomerScore> _set = new SortedSet<CustomerScore>();

    private ConcurrentDictionary<long, int> _idToIndexMap; // ID -> Index  

    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    private List<CustomerScore> _cache;

    private bool isChacheNeedRefresh = false;

    private int lastRanking = 0;

    private SortedScoreBoard()
    {
        _set = new SortedSet<CustomerScore>(new CustomerScoreComparer());
        _idToIndexMap = new ConcurrentDictionary<long, int>();
        _lock = new ReaderWriterLockSlim();
    }

    /// <summary>
    /// Get the values ranked between start and end
    /// </summary>
    /// <param name="start">start index（>0）</param>
    /// <param name="end">end index(include)</param>
    /// <returns>List of values with indices between start and end"</returns>
    public List<object> GeCustomerScoresBetweenIndices(int start, int end)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), "Start index cannot be less than 0.");
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end), "End index cannot be less than the start index.");

        _lock.EnterReadLock();
        try
        {
            if (this.isChacheNeedRefresh)
            {
                _cache = _set.ToList();
                this.isChacheNeedRefresh = false;
            }
            int totalCount = Math.Min(_set.Count, this.lastRanking);
            if (start >= totalCount) throw new ArgumentOutOfRangeException(nameof(start), "Start index out of ranking range.");
            if (end >= totalCount) end = totalCount - 1; // end should not more than set range.

            var subList = _cache.GetRange(start, end - start + 1);
            List<object> values = new List<object>();
            foreach (var customer in subList)
            {
                if (customer.Score <= 0)
                {
                    break;
                }
                values.Add(customer);
            }
            return values;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the values within the range before and after the given key (high elements before to low elements after)
    /// </summary>
    /// <param name="customerId">CustomerId</param>
    /// <param name="high">The number of elements to retrieve before</param>
    /// <param name="low">The number of elements to retrieve after</param>
    /// <param name="startIndex">The start index of the returned range</param>
    /// <returns>A list containing values from m elements before to n elements after</returns>
    public List<object> GeCustomerScoresAroundKey(long customerId, int high, int low, out int startIndex)
    {
        if (high < 0 || low < 0) throw new ArgumentOutOfRangeException("high and low must be non-negative intege");

        _lock.EnterReadLock();
        try
        {
            if (this.isChacheNeedRefresh)
            {
                _cache = _set.ToList();
                this.isChacheNeedRefresh = false;
            }
            var index = GetIndexById(customerId);
            if (index == -1 || index >= this.lastRanking)
            {
                throw new KeyNotFoundException($"Customer {customerId} does not exist in the ranking board.");
            }


            // Caculate the real start and end
            startIndex = Math.Max(0, index - high);
            int endIndex = Math.Min(_set.Count - 1, index + low);
            
            var subList = _cache.GetRange(startIndex, endIndex - startIndex + 1);

            List<object> values = new List<object>();
            foreach (var customer in subList)
            {
                if (customer.Score <= 0)
                {
                    break;
                }
                values.Add(customer);
            }
            return values;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a key-value pair to the cache if the key does not exist,
    /// or updates the value if the key already exists. 
    /// Uses upgradeable read and write locks to ensure thread safety.
    /// </summary>
    /// <typeparam name="long">CustomerId.</typeparam>
    /// <typeparam name="decimal">score.</typeparam>
    /// <returns>The value that was added or updated.</returns>
    public decimal AddOrUpdate(long customerId, decimal score)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            this.isChacheNeedRefresh = true;
            var index = GetIndexById(customerId);
            if (index != -1)
            {
                _lock.EnterWriteLock();
                try
                {
                    var currenCustomerScore = _set.ElementAt(index);
                    _set.Remove(currenCustomerScore);
                    currenCustomerScore.Score += score;
                    _set.Add(currenCustomerScore);
                    UpdateAllIndexes();
                    return currenCustomerScore.Score;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            else
            {
                _lock.EnterWriteLock();
                try
                {
                    _set.Add(new CustomerScore(customerId, score));
                    UpdateAllIndexes();
                    return score;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public int GetIndexById(long customerId)
    {
        if (_idToIndexMap.TryGetValue(customerId, out int index))
        {
            return index;
        }
        return -1;
    }

    private void UpdateAllIndexes()
    {
        _idToIndexMap.Clear();
        int index = 0;
        foreach (var item in _set)
        {
            if (item.Score <= 0)
            {
                this.lastRanking = index;
            }
            _idToIndexMap[item.CustomerId] = index++;
        }
    }
}