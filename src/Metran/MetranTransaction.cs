using System.Collections.Concurrent;

namespace Metran;


public sealed class MetranTransaction<T> : IDisposable
{
  private readonly T _id;
  private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;

  internal MetranTransaction(T id,
                             ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
    _id = id;
    _bag = bag;
  }

  private bool _disposed;

  public void Dispose() {
    if (_disposed) return;
    EndTransaction();
    _disposed = true;
  }

  public bool EndTransaction() {
    return _bag.TryRemove(_id, out var _);
  }
}