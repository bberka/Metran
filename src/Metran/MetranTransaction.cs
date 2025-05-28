
// MetranTransaction.cs (No functional changes, just updated comments and reference to MetranContainer)
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Metran
{
	/// <summary>
	/// Represents a single, unique in-memory transaction managed by <see cref="MetranContainer{T}"/>.
	/// This class is primarily used within a `using` statement to ensure the transaction is automatically
	/// removed from the container upon completion.
	/// </summary>
	/// <typeparam name="T">The type of the transaction identity.</typeparam>
	public sealed class MetranTransaction<T> : IDisposable
	{
		/// <summary>
		/// A reference to the parent <see cref="ConcurrentDictionary{T, MetranTransaction{T}}"/>
		/// in which this transaction is stored.
		/// </summary>
		private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;
		
		/// <summary>
		/// The unique identity of this transaction.
		/// </summary>
		internal readonly T Id;

		/// <summary>
		/// Indicates whether the transaction has been disposed.
		/// </summary>
		private bool _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MetranTransaction{T}"/> class.
		/// </summary>
		/// <param name="id">The unique identity for this transaction.</param>
		/// <param name="bag">A reference to the <see cref="ConcurrentDictionary{T, MetranTransaction{T}}"/>
		/// where this transaction will be stored.</param>
		internal MetranTransaction(T id,
		                           ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
			Id = id;
			_bag = bag;
		}

		/// <summary>
		/// Disposes the transaction, removing it from the <see cref="MetranContainer{T}"/>.
		/// This method should ideally be called implicitly via a `using` statement.
		/// </summary>
		public void Dispose() {
			if (_disposed) return; // Prevent multiple disposals
			
			// Attempt to remove the transaction from the concurrent dictionary.
			// This releases the "lock" for the given ID.
			_bag.TryRemove(Id, out _);
			_disposed = true;
		}

		/// <summary>
		/// Waits asynchronously for this transaction to complete (i.e., be disposed and removed from the container).
		/// If the transaction does not complete within the specified timeout, a <see cref="TimeoutException"/> is thrown.
		/// </summary>
		/// <param name="waitDelayMiliseconds">The delay in milliseconds between checks for the transaction's completion. Defaults to 100ms.</param>
		/// <param name="timeoutMiliseconds">The maximum time in milliseconds to wait for the transaction to complete. Defaults to 10000ms (10 seconds).</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the wait operation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous wait operation.</returns>
		/// <exception cref="ObjectDisposedException">Thrown if this <see cref="MetranTransaction{T}"/> instance has already been disposed.</exception>
		/// <exception cref="TimeoutException">Thrown if the transaction does not complete within the <paramref name="timeoutMiliseconds"/>.</exception>
		/// <exception cref="OperationCanceledException">Thrown if the <paramref name="cancellationToken"/> is cancelled during the wait.</exception>
		public async Task WaitAsync(int waitDelayMiliseconds = 100,
		                            int timeoutMiliseconds = 10000,
		                            CancellationToken cancellationToken = default) {
			if (_disposed) throw new ObjectDisposedException(nameof(MetranTransaction<T>));
			var safeWaitResult = await SafeWaitAsync(waitDelayMiliseconds, timeoutMiliseconds, cancellationToken).ConfigureAwait(false);
			if (!safeWaitResult) {
				throw new TimeoutException($"Timeout while waiting for transaction with ID: {Id} after {timeoutMiliseconds} milliseconds.");
			}
		}

		/// <summary>
		/// Safely waits asynchronously for this transaction to complete (i.e., be disposed and removed from the container).
		/// This method returns <see langword="false"/> if the transaction is still in progress after the timeout,
		/// rather than throwing an exception.
		/// </summary>
		/// <param name="waitDelayMiliseconds">The delay in milliseconds between checks for the transaction's completion. Defaults to 100ms.</param>
		/// <param name="timeoutMiliseconds">The maximum time in milliseconds to wait for the transaction to complete. Defaults to 10000ms (10 seconds).</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the wait operation.</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> that represents the asynchronous wait operation. The task result is
		/// <see langword="true"/> if the transaction completed within the timeout; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="ObjectDisposedException">Thrown if this <see cref="MetranTransaction{T}"/> instance has already been disposed.</exception>
		/// <exception cref="OperationCanceledException">Thrown if the <paramref name="cancellationToken"/> is cancelled during the wait.</exception>
		public async Task<bool> SafeWaitAsync(int waitDelayMiliseconds = 100,
		                                      int timeoutMiliseconds = 10000,
		                                      CancellationToken cancellationToken = default) {
			if (_disposed) throw new ObjectDisposedException(nameof(MetranTransaction<T>));
			var startTime = Environment.TickCount;
			while (_bag.ContainsKey(Id)) { 
				if (Environment.TickCount - startTime > timeoutMiliseconds) {
					return false;
				}
				await Task.Delay(waitDelayMiliseconds, cancellationToken).ConfigureAwait(false);
			}

			return true; 
		}
	}
}
