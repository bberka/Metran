using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Metran
{
  /// <summary>
  ///   Thread-safe Metran container to create and manage memory contained transactions
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public sealed class MetranContainer<T>
  {
    private ConcurrentDictionary<T, MetranTransaction<T>> _bag = new ConcurrentDictionary<T, MetranTransaction<T>>();

    public MetranTransaction<T> this[T key] => _bag[key];

    public MetranTransaction<T> ForceAddTransaction(T transactionIdentity) {
      var tran = new MetranTransaction<T>(transactionIdentity, ref _bag);
      _bag[transactionIdentity] = tran;
      return tran;
    }

    public MetranTransaction<T> ThrowOrAddTransaction(T transactionIdentity) {
      if (_bag.ContainsKey(transactionIdentity)) {
        throw new Exception("Transaction already exists: " + transactionIdentity);
      }

      var tran = new MetranTransaction<T>(transactionIdentity, ref _bag);
      _bag[transactionIdentity] = tran;
      return tran;
    }

    public MetranTransaction<T> GetOrAddTransaction(T transactionIdentity) {
      if (_bag.TryGetValue(transactionIdentity, out var transaction)) {
        return transaction;
      }

      var tran = new MetranTransaction<T>(transactionIdentity, ref _bag);
      _bag[transactionIdentity] = tran;
      return tran;
    }


    public bool TryAddTransaction(T transactionIdentity, out MetranTransaction<T> transaction) {
      transaction = null;
      if (_bag.ContainsKey(transactionIdentity)) {
        return false;
      }

      transaction = new MetranTransaction<T>(transactionIdentity, ref _bag);
      _bag[transactionIdentity] = transaction;
      return true;
    }

    public bool TryAddTransactionList(HashSet<T> transactionIdentityList, out MetranTransactionList<T> transactionList) {
      transactionList = null;
      var addedList = new List<MetranTransaction<T>>();
      foreach (var id in transactionIdentityList) {
        var added = TryAddTransaction(id, out var transaction);
        if (added) {
          addedList.Add(transaction);
        }
        else {
          break;
        }
      }

      if (addedList.Count != transactionIdentityList.Count) {
        foreach (var tran in addedList) tran.Dispose();
        return false;
      }

      transactionList = new MetranTransactionList<T>(addedList, ref _bag);
      return true;
    }

    public MetranTransactionList<T> ForceAddTransactionList(HashSet<T> transactionIdentityList) {
      var addedList = new List<MetranTransaction<T>>();
      foreach (var id in transactionIdentityList) {
        var added = TryAddTransaction(id, out var transaction);
        if (added) {
          addedList.Add(transaction);
        }
        else {
          break;
        }
      }

      if (addedList.Count != transactionIdentityList.Count) {
        foreach (var tran in addedList) tran.Dispose();
        throw new Exception("Transaction already exists: " + string.Join(", ", transactionIdentityList));
      }

      return new MetranTransactionList<T>(addedList, ref _bag);
    }

    public bool HasTransaction(T transactionIdentity) {
      lock (_bag) {
        return _bag.ContainsKey(transactionIdentity);
      }
    }

    public bool RemoveTransaction(T transactionIdentity) {
      if (_bag.TryRemove(transactionIdentity, out var val)) {
        val.Dispose();
      }

      return false;
    }
  }
}