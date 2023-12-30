using System.Collections.Concurrent;

namespace Metran;

/// <summary>
/// Thread-safe Metran container to create and manage memory contained transactions
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class MetranContainer<T> where T : notnull
{
  public MetranContainer() { }
  private ConcurrentDictionary<T, MetranTransaction<T>> _bag = new();

  /// <summary>
  ///  Returns null if there is a transaction with the same identity
  /// </summary>
  /// <param name="transactionIdentity"></param>
  /// <returns></returns>
  public MetranTransaction<T>? BeginTransaction(T transactionIdentity) {
    var metran = new MetranTransaction<T>(transactionIdentity, ref _bag);
    var added = _bag.TryAdd(transactionIdentity, metran);
    return !added
             ? null
             : metran;
  }

  public MetranTransaction<T>? BeginTransactionWithRetry(T transactionIdentity,
                                                         byte maxRetryCount,
                                                         int retryDelayMs) {
    var retryCount = 0;
    while (true) {
      var t = BeginTransaction(transactionIdentity);
      if (t != null) {
        return t;
      }

      if (retryCount >= maxRetryCount) {
        return null;
      }

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
    return _bag.TryRemove(transactionIdentity, out var _);
  }
}