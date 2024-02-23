using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Metran
{
  /// <summary>
  ///   Thread-safe Metran container to create and manage memory contained transactions
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public sealed class MetranContainer<T>
  {
    private ConcurrentDictionary<T, MetranTransaction<T>> _bag = new ConcurrentDictionary<T, MetranTransaction<T>>();

    /// <summary>
    ///   Returns null if there is a transaction with the same identity
    /// </summary>
    /// <param name="transactionIdentity"></param>
    /// <returns></returns>
    public MetranTransaction<T> BeginTransaction(T transactionIdentity) {
      var metran = new MetranTransaction<T>(transactionIdentity, ref _bag);
      var added = _bag.TryAdd(transactionIdentity, metran);
      return !added
               ? null
               : metran;
    }

    /// <summary>
    ///   Returns null if there is a transaction with the same identity
    /// </summary>
    /// <param name="transactionIdentityList"></param>
    /// <returns></returns>
    public MetranTransactionList<T> BeginTransaction(HashSet<T> transactionIdentityList) {
      var addedList = new List<MetranTransaction<T>>();
      foreach (var id in transactionIdentityList) {
        var tran = BeginTransaction(id);
        if (tran == null) break;
        addedList.Add(tran);
      }

      if (addedList.Count == transactionIdentityList.Count)
        return new MetranTransactionList<T>(addedList, ref _bag);
      foreach (var tran in addedList) tran.Dispose();

      return null;
    }

    public MetranTransaction<T> BeginTransaction(T transactionIdentity,
                                                 byte maxRetryCount,
                                                 int retryDelayMs) {
      var retryCount = 0;
      while (true) {
        var t = BeginTransaction(transactionIdentity);
        if (t != null) return t;

        if (retryCount >= maxRetryCount) return null;

        retryCount++;
        Thread.Sleep(retryDelayMs);
      }
    }

    public MetranTransactionList<T> BeginTransaction(HashSet<T> transactionIdentityList,
                                                     byte maxRetryCount,
                                                     int retryDelayMs) {
      var retryCount = 0;
      while (true) {
        var t = BeginTransaction(transactionIdentityList);
        if (t != null) return t;

        if (retryCount >= maxRetryCount) return null;

        retryCount++;
        Thread.Sleep(retryDelayMs);
      }
    }


    public bool HasTransaction(T transactionIdentity) {
      lock (_bag) {
        return _bag.ContainsKey(transactionIdentity);
      }
    }

    public bool EndTransaction(T transactionIdentity) {
      return _bag.TryRemove(transactionIdentity, out _);
    }
  }
}