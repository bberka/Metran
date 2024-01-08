using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Metran
{
  public sealed class MetranTransactionList<T> : IDisposable
  {
    private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;
    private readonly List<MetranTransaction<T>> _tranList;

    private bool _disposed;

    internal MetranTransactionList(List<MetranTransaction<T>> tranList,
                                   ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
      _tranList = tranList;
      _bag = bag;
    }

    public void Dispose() {
      if (_disposed) return;
      foreach (var id in _tranList) id.Dispose();
      _disposed = true;
    }
  }
}