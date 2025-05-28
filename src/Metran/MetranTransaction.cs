using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Metran
{
	public sealed class MetranTransaction<T> : IDisposable
	{
		private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;
		internal readonly T Id;

		private bool _disposed;

		internal MetranTransaction(T id,
		                           ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
			Id = id;
			_bag = bag;
		}

		public void Dispose() {
			if (_disposed) return;
			_bag.TryRemove(Id, out _);
			_disposed = true;
		}

		/// <summary>
		///  Waits for the transaction to complete without a timeout.
		/// </summary>
		/// <param name="waitDelayMiliseconds">
		///  The delay between checks for the transaction's completion.
		///  </param>
		/// <param name="timeoutMiliseconds">
		/// The maximum time to wait for the transaction to complete.
		/// </param>
		/// <param name="cancellationToken">
		/// A cancellation token to cancel the wait operation.
		/// </param>
		/// <exception cref="TimeoutException"></exception>
		public async Task WaitAsync(int waitDelayMiliseconds = 100,
		                            int timeoutMiliseconds = 10000,
		                            CancellationToken cancellationToken = default) {
			if (_disposed) throw new ObjectDisposedException(nameof(MetranTransactionList<T>));
			var safeWaitResult = await SafeWaitAsync(waitDelayMiliseconds, timeoutMiliseconds, cancellationToken).ConfigureAwait(false);
			if (!safeWaitResult) {
				throw new TimeoutException($"Timeout while waiting for transaction with ID: {Id}");
			}
		}

		/// <summary>
		///  Waits for the transaction to complete returns false if the transaction is still in progress after the timeout.
		/// </summary>
		/// <param name="waitDelayMiliseconds">
		///  The delay between checks for the transaction's completion.
		///  </param>
		/// <param name="timeoutMiliseconds">
		/// The maximum time to wait for the transaction to complete.
		/// </param>
		/// <param name="cancellationToken">
		/// A cancellation token to cancel the wait operation.
		/// </param>
		/// <exception cref="TimeoutException"></exception>
		public async Task<bool> SafeWaitAsync(int waitDelayMiliseconds = 100,
		                                      int timeoutMiliseconds = 10000,
		                                      CancellationToken cancellationToken = default) {
			if (_disposed) throw new ObjectDisposedException(nameof(MetranTransactionList<T>));
			var startTime = Environment.TickCount;
			while (_bag.TryGetValue(Id, out _)) {
				if (Environment.TickCount - startTime > timeoutMiliseconds) {
					return false;
				}

				await Task.Delay(waitDelayMiliseconds, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
	}
}