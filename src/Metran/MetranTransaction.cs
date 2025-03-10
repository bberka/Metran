using System;
using System.Collections.Concurrent;

namespace Metran
{
  public sealed class MetranTransaction<T> : IDisposable
  {
    private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;
    private readonly T _id;

    private bool _disposed;

    internal MetranTransaction(T id,
                               ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
      _id = id;
      _bag = bag;
    }

    public void Dispose() {
      if (_disposed) return;
      _bag.TryRemove(_id, out _);
      _disposed = true;
    }
  }
}