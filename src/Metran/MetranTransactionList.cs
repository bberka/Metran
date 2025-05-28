using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metran
{
	public sealed class MetranTransactionList<T> : IDisposable
	{
		private readonly ConcurrentDictionary<T, MetranTransaction<T>> _bag;
		private readonly HashSet<MetranTransaction<T>> _tranList;

		private bool _disposed;

		internal MetranTransactionList(HashSet<MetranTransaction<T>> tranList,
		                               ref ConcurrentDictionary<T, MetranTransaction<T>> bag) {
			_tranList = tranList;
			_bag = bag;
		}

		public void Dispose() {
			if (_disposed) return;
			foreach (var id in _tranList) id.Dispose();
			_disposed = true;
		}

		/// <summary>
		///  Waits for all transactions to complete, throwing a TimeoutException if the wait times out.
		/// </summary>
		/// <param name="waitDelayMiliseconds"></param>
		/// <param name="timeoutMiliseconds"></param>
		/// <param name="cancellationToken"></param>
		/// <exception cref="TimeoutException"></exception>
		public async Task WaitAllAsync(int waitDelayMiliseconds = 100,
		                               int timeoutMiliseconds = 10000,
		                               CancellationToken cancellationToken = default) {
			var safeWaitResult = await SafeWaitAllAsync(waitDelayMiliseconds, timeoutMiliseconds, cancellationToken);
			if (safeWaitResult) return;
			throw new TimeoutException($"Waiting for transactions to complete timed out after {timeoutMiliseconds} milliseconds.");
		}

		/// <summary>
		///  Safely waits for all transactions to complete without throwing an exception.
		/// </summary>
		/// <param name="waitDelayMiliseconds"></param>
		/// <param name="timeoutMiliseconds"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <exception cref="ObjectDisposedException"></exception>
		public async Task<bool> SafeWaitAllAsync(int waitDelayMiliseconds = 100,
		                                         int timeoutMiliseconds = 10000,
		                                         CancellationToken cancellationToken = default) {
			//returns false if timeout 
			if (_disposed) throw new ObjectDisposedException(nameof(MetranTransactionList<T>));
			if (_tranList.Count == 0) return true;
			var waitTime = 0;
			while (waitTime < timeoutMiliseconds) {
				var allDone = _tranList.All(tran => !_bag.ContainsKey(tran.Id));
				if (allDone) return true;
				await Task.Delay(waitDelayMiliseconds, cancellationToken);
				waitTime += waitDelayMiliseconds;
			}

			return false; // Timeout reached, return false
		}
	}
}