using System.Collections.Concurrent;

namespace Metran;

public sealed class MetranTransactionList<T> : IDisposable where T : notnull
{
  private readonly List<MetranTransaction<T>> _tranList;
  private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;

  internal MetranTransactionList(List<MetranTransaction<T>> tranList,
                                 ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
    _tranList = tranList;
    _bag = bag;
  }

  private bool _disposed;

  public void Dispose() {
    if (_disposed) return;
    foreach (var id in _tranList) {
      id.Dispose();
    }
    _disposed = true;
  }

}