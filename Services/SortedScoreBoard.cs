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

    private Dictionary<long, decimal> _idValueMap; // ID -> Value  

    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    private List<CustomerScore> _cache = null;

    private int lastRanking = 0;

    private int _addOrUpdateCount = 0;

    private DateTime _lastAddOrUpdateTime = new DateTime();

    private Timer _updateCacheTimer;

    private TimeSpan _updateCacheTimeSpan = new TimeSpan(50);

    private SortedScoreBoard()
    {
        _idValueMap = new Dictionary<long, decimal>();
        _lock = new ReaderWriterLockSlim();
        InitializeUpdateCacheTimer();
    }

    /// <summary>
    /// Get the values ranked between start and end
    /// </summary>
    /// <param name="start">start index（>0）</param>
    /// <param name="end">end index(include)</param>
    /// <returns>List of values with indices between start and end"</returns>
    public List<CustomerScore> GeCustomerScoresBetweenIndices(int start, int end)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), "Start index cannot be less than 0.");
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end), "End index cannot be less than the start index.");

        _lock.EnterReadLock();
        try
        {
            if (start >= this.lastRanking) return new List<CustomerScore>();
            if (end >= this.lastRanking) end = this.lastRanking - 1; // end should not more than range.

            var subList = _cache.GetRange(start, end - start + 1);
            List<object> values = new List<object>();
            return subList;
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
    public List<CustomerScore> GeCustomerScoresAroundKey(long customerId, int high, int low, out int startIndex)
    {
        if (high < 0 || low < 0) throw new ArgumentOutOfRangeException("high and low must be non-negative integer");

        _lock.EnterReadLock();
        try
        {
            var index = GetIndexById(customerId);
            if (index == -1 || index > this.lastRanking)
            {
                startIndex = -1;
                return new List<CustomerScore>();
            }
            // Caculate the real start and end
            startIndex = Math.Max(0, index - high);
            int endIndex = Math.Min(this.lastRanking - 1, index + low);

            var subList = _cache.GetRange(startIndex, endIndex - startIndex + 1);
            return subList;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private int GetIndexById(long customerId)
    {
        if (_cache == null || _cache.Count == 0)
            return -1;

        // use id get score for binary search
        if (!_idValueMap.TryGetValue(customerId, out decimal score))
            return -1;

        int left = 0, right = _cache.Count - 1;
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            var midCustomer = _cache[mid];

            if (midCustomer.CustomerId == customerId)
                return mid;

            if (midCustomer.Score > score ||
            (midCustomer.Score == score && midCustomer.CustomerId < customerId))
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        // If not found by binary search, traverse to find it (fallback)
        for (int i = 0; i < _cache.Count; i++)
        {
            if (_cache[i].CustomerId == customerId)
            return i;
        }
        return -1;
    }


    /// <summary>
    /// Adds a key-value pair to the cache if the key does not exist,
    /// or updates the value if the key already exists. 
    /// Uses upgradeable read and write locks to ensure thread safety.
    /// </summary>
    /// <typeparam name="long">CustomerId.</typeparam>
    /// <typeparam name="decimal">score.</typeparam>
    /// <returns>Tuple: bool indicating if the value changed, and a detail message as out parameter.</returns>
    public bool AddOrUpdate(long customerId, decimal score, out string message)
    {
        bool changed = false;

        if (score < -1000m || score > 1000m)
        {
            message = "Score must be between -1000 and 1000.";
            return changed;
        }

        _lock.EnterUpgradeableReadLock();
        try
        {
            decimal newScore;
            if (_idValueMap.ContainsKey(customerId))
            {
                decimal oldScore = _idValueMap[customerId];
                _idValueMap[customerId] += score;

                // Clamp the updated score as well
                if (_idValueMap[customerId] < -10000m)
                {
                    _idValueMap[customerId] = -10000m;
                    newScore = _idValueMap[customerId];
                    message = $"Total score is less than -10000. Clamped to -10000.";
                }
                else if (_idValueMap[customerId] > 10000m)
                {
                    _idValueMap[customerId] = 10000m;
                    newScore = _idValueMap[customerId];
                    message = $"Total score is greater than 10000. Clamped to 10000.";
                }
                else
                {
                    newScore = _idValueMap[customerId];
                    message = $"User {customerId} has been updated to new score {newScore}.";
                }
                changed = oldScore != newScore;
            }
            else
            {
                _idValueMap[customerId] = score;
                newScore = _idValueMap[customerId];
                message = $"User {customerId} has been set to new score {newScore}.";
                changed = true;
            }
            return changed;
        }
        finally
        {
            _addOrUpdateCount++;
            _lock.ExitUpgradeableReadLock();
        }
    }
 

    private void InitializeUpdateCacheTimer()
    {
        this._updateCacheTimer = new Timer(_ =>
        {
            if (_addOrUpdateCount > 0 && (DateTime.UtcNow - _lastAddOrUpdateTime).TotalMilliseconds >= _updateCacheTimeSpan.TotalMilliseconds)
            {
                UpdateCache();
                _addOrUpdateCount = 0;
            }
        }, null, 50, 50);
    }
    
    /// <summary>
    /// Updates the cache by transferring _idValueMap to a sorted list of CustomerScore.
    /// </summary>
    private void UpdateCache()
    {
        _lock.EnterWriteLock();
        try
        {
            var customerScores = _idValueMap
                .Select(kvp => new CustomerScore(kvp.Key, kvp.Value))
                .ToList();
            customerScores.Sort(new CustomerScoreComparer());

            // Set lastRanking to the first index where Score <= 0
            this.lastRanking = customerScores.FindIndex(cs => cs.Score <= 0);
            if (this.lastRanking == -1)
            {
                this.lastRanking = customerScores.Count - 1;
            }

            _cache = customerScores;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

}