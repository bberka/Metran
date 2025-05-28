using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metran
{
	/// <summary>
	/// Represents a collection of related in-memory transactions, managed as a single unit.
	/// This class is typically used within a `using` statement to ensure all contained
	/// transactions are automatically removed from the <see cref="MetranContainer{T}"/> upon completion.
	/// </summary>
	/// <typeparam name="T">The type of the transaction identity.</typeparam>
	public sealed class MetranTransactionList<T> : IDisposable
	{
		/// <summary>
		/// A reference to the parent <see cref="ConcurrentDictionary{T, MetranTransaction{T}}"/>
		/// in which these transactions are stored.
		/// </summary>
		private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;

		/// <summary>
		/// The collection of <see cref="MetranTransaction{T}"/> instances managed by this list.
		/// Uses a <see cref="HashSet{T}"/> for efficient lookups and uniqueness.
		/// </summary>
		private readonly HashSet<MetranTransaction<T>> _tranList;

		/// <summary>
		/// Indicates whether this transaction list has been disposed.
		/// </summary>
		private bool _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MetranTransactionList{T}"/> class.
		/// </summary>
		/// <param name="tranList">A <see cref="HashSet{MetranTransaction{T}}"/> containing the transactions to manage.</param>
		/// <param name="bag">A reference to the <see cref="ConcurrentDictionary{T, MetranTransaction{T}}"/>
		/// where these transactions are stored.</param>
		internal MetranTransactionList(HashSet<MetranTransaction<T>> tranList,
		                               ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
			_tranList = tranList ?? throw new ArgumentNullException(nameof(tranList));
			_bag = bag;
		}

		/// <summary>
		/// Disposes all transactions within this list, effectively removing them from the <see cref="MetranContainer{T}"/>.
		/// This method should ideally be called implicitly via a `using` statement.
		/// </summary>
		public void Dispose() {
			if (_disposed) return;

			foreach (var tran in _tranList) tran.Dispose();
			_disposed = true;
		}

		/// <summary>
		/// Waits asynchronously for all transactions in this list to complete (i.e., be disposed and removed from the container).
		/// If any transaction does not complete within the specified timeout, a <see cref="TimeoutException"/> is thrown.
		/// </summary>
		/// <param name="waitDelayMiliseconds">The delay in milliseconds between checks for the transactions' completion. Defaults to 100ms.</param>
		/// <param name="timeoutMiliseconds">The maximum time in milliseconds to wait for all transactions to complete. Defaults to 10000ms (10 seconds).</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the wait operation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous wait operation.</returns>
		/// <exception cref="ObjectDisposedException">Thrown if this <see cref="MetranTransactionList{T}"/> instance has already been disposed.</exception>
		/// <exception cref="TimeoutException">Thrown if any transaction in the list does not complete within the <paramref name="timeoutMiliseconds"/>.</exception>
		/// <exception cref="OperationCanceledException">Thrown if the <paramref name="cancellationToken"/> is cancelled during the wait.</exception>
		public async Task WaitAllAsync(int waitDelayMiliseconds = 100,
		                               int timeoutMiliseconds = 10000,
		                               CancellationToken cancellationToken = default) {
			var safeWaitResult = await SafeWaitAllAsync(waitDelayMiliseconds, timeoutMiliseconds, cancellationToken).ConfigureAwait(false);
			if (!safeWaitResult) {
				throw new TimeoutException($"Waiting for transactions to complete timed out after {timeoutMiliseconds} milliseconds.");
			}
		}

		/// <summary>
		/// Safely waits asynchronously for all transactions in this list to complete (i.e., be disposed and removed from the container).
		/// This method returns <see langword="false"/> if any transaction is still in progress after the timeout,
		/// rather than throwing an exception.
		/// </summary>
		/// <param name="waitDelayMiliseconds">The delay in milliseconds between checks for the transactions' completion. Defaults to 100ms.</param>
		/// <param name="timeoutMiliseconds">The maximum time in milliseconds to wait for all transactions to complete. Defaults to 10000ms (10 seconds).</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the wait operation.</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> that represents the asynchronous wait operation. The task result is
		/// <see langword="true"/> if all transactions completed within the timeout; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="ObjectDisposedException">Thrown if this <see cref="MetranTransactionList{T}"/> instance has already been disposed.</exception>
		/// <exception cref="OperationCanceledException">Thrown if the <paramref name="cancellationToken"/> is cancelled during the wait.</exception>
		public async Task<bool> SafeWaitAllAsync(int waitDelayMiliseconds = 100,
		                                         int timeoutMiliseconds = 10000,
		                                         CancellationToken cancellationToken = default) {
			if (_disposed) throw new ObjectDisposedException(nameof(MetranTransactionList<T>));

			if (!_tranList.Any()) return true;

			var waitTime = 0;
			var startTime = Environment.TickCount;

			while (waitTime < timeoutMiliseconds) {
				var allDone = _tranList.All(tran => !_bag.ContainsKey(tran.Id));
				if (allDone) return true;

				cancellationToken.ThrowIfCancellationRequested();

				if (Environment.TickCount - startTime >= timeoutMiliseconds) {
					return false;
				}

				await Task.Delay(waitDelayMiliseconds, cancellationToken).ConfigureAwait(false);
				waitTime += waitDelayMiliseconds;

				if (waitTime >= timeoutMiliseconds) {
					return false;
				}
			}

			return false;
		}
	}
}