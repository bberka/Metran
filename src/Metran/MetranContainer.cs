using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq; // Added for .All() in MetranTransactionList, but not directly in container.

namespace Metran
{
	/// <summary>
	/// Thread-safe Metran container to create and manage in-memory transactions.
	/// Transactions in Metran represent active operations identified by a unique key,
	/// primarily used to prevent concurrent execution of code segments based on these keys.
	/// </summary>
	/// <typeparam name="T">The type of the transaction identity (e.g., long for user ID, string for resource name).</typeparam>
	public sealed class MetranContainer<T>
	{
		/// <summary>
		/// The underlying thread-safe dictionary that stores active transactions.
		/// </summary>
		private ConcurrentDictionary<T, MetranTransaction<T>> _bag = new ConcurrentDictionary<T, MetranTransaction<T>>();

		/// <summary>
		/// Gets the <see cref="MetranTransaction{T}"/> associated with the specified key.
		/// </summary>
		/// <param name="key">The key of the transaction to get.</param>
		/// <returns>The <see cref="MetranTransaction{T}"/> associated with the specified key.</returns>
		/// <exception cref="KeyNotFoundException">The property is retrieved, and key does not exist.</exception>
		public MetranTransaction<T> this[T key] => _bag[key];

		/// <summary>
		/// Adds a new transaction with the specified identity to the container.
		/// This method enforces uniqueness; if a transaction with the same identity already exists,
		/// an exception is thrown.
		/// </summary>
		/// <param name="transactionIdentity">The unique identity of the transaction to add.</param>
		/// <returns>The newly created <see cref="MetranTransaction{T}"/>.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown if a transaction with the given <paramref name="transactionIdentity"/> already exists in the container.
		/// </exception>
		/// <remarks>
		/// This method ensures that only one active transaction exists for a given identity at any time.
		/// </remarks>
		public MetranTransaction<T> AddTransaction(T transactionIdentity) {
			var tran = new MetranTransaction<T>(transactionIdentity, ref _bag);
			if (_bag.TryAdd(transactionIdentity, tran)) return tran;
			tran.Dispose();
			throw new InvalidOperationException($"Transaction already exists: {transactionIdentity}");
		}

		/// <summary>
		/// Gets an existing transaction with the specified identity, or adds a new one if it does not exist.
		/// This method is idempotent and thread-safe.
		/// </summary>
		/// <param name="transactionIdentity">The unique identity of the transaction to get or add.</param>
		/// <returns>
		/// The existing <see cref="MetranTransaction{T}"/> if found, or the newly added one.
		/// </returns>
		public MetranTransaction<T> GetOrAddTransaction(T transactionIdentity) {
			var tran = new MetranTransaction<T>(transactionIdentity, ref _bag);
			return _bag.TryAdd(transactionIdentity, tran)
				       ? tran
				       : _bag[transactionIdentity];
		}

			/// <summary>
			/// Attempts to add a new transaction with the specified identity.
			/// This method does not throw an exception if the transaction already exists; instead, it returns false.
			/// </summary>
			/// <param name="transactionIdentity">The unique identity of the transaction to attempt to add.</param>
			/// <param name="transaction">
			/// When this method returns, contains the newly added <see cref="MetranTransaction{T}"/> if the addition was successful,
			/// or <see langword="null"/> if a transaction with the same identity already exists.
			/// This parameter is passed uninitialized.
			/// </param>
			/// <returns>
			/// <see langword="true"/> if the transaction was successfully added (i.e., it did not exist previously);
			/// <see langword="false"/> if a transaction with the same identity already exists.
			/// </returns>
			public bool TryAddTransaction(T transactionIdentity, out MetranTransaction<T> transaction) {
				var newTran = new MetranTransaction<T>(transactionIdentity, ref _bag);
				if (_bag.TryAdd(transactionIdentity, newTran)) {
					transaction = newTran;
					return true;
				}

				transaction = null;
				newTran.Dispose(); 
				return false;
			}

			/// <summary>
			/// Attempts to add a list of transactions atomically.
			/// All transactions in the <paramref name="transactionIdentityList"/> must be unique and not currently exist in the container.
			/// If even one identity already exists, no transactions are added, and the method returns false.
			/// </summary>
			/// <param name="transactionIdentityList">A <see cref="HashSet{T}"/> containing the unique identities of the transactions to add.</param>
			/// <param name="transactionList">
			/// When this method returns, contains the <see cref="MetranTransactionList{T}"/> representing the added transactions
			/// if the operation was successful; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
			/// </param>
			/// <returns>
			/// <see langword="true"/> if all transactions were successfully added;
			/// <see langword="false"/> if any transaction in the list already existed or if the list was empty.
			/// </returns>
			/// <exception cref="ArgumentException">
			///  Thrown if the <paramref name="transactionIdentityList"/> is null or empty.
			///  </exception>
			/// <remarks>
			/// This operation attempts to acquire all locks as a single unit. If it fails for any reason
			/// (e.g., an ID is already taken), any partially acquired locks are released to maintain atomicity.
			/// </remarks>
			public bool TryAddTransactionList(HashSet<T> transactionIdentityList, out MetranTransactionList<T> transactionList) {
				if (transactionIdentityList.Count == 0) {
					throw new ArgumentException("Transaction identity list cannot be empty.", nameof(transactionIdentityList));
				}
				transactionList = null;
				var addedList = new HashSet<MetranTransaction<T>>();

				foreach (var id in transactionIdentityList) {
					if (!_bag.ContainsKey(id)) continue;
					foreach (var tran in addedList) tran.Dispose();
					return false;
				}

				foreach (var id in transactionIdentityList) {
					var newTran = new MetranTransaction<T>(id, ref _bag);
					if (_bag.TryAdd(id, newTran)) {
						addedList.Add(newTran);
					}
					else {
						foreach (var tran in addedList) tran.Dispose();
						newTran.Dispose(); 
						return false;
					}
				}

				transactionList = new MetranTransactionList<T>(addedList, ref _bag);
				return true;
			}

			/// <summary>
			/// Adds a list of transactions atomically.
			/// All transactions in the <paramref name="transactionIdentityList"/> must be unique and not currently exist in the container.
			/// If even one identity already exists, an <see cref="InvalidOperationException"/> is thrown, and no transactions are added.
			/// </summary>
			/// <param name="transactionIdentityList">A <see cref="HashSet{T}"/> containing the unique identities of the transactions to add.</param>
			/// <returns>The newly created <see cref="MetranTransactionList{T}"/> representing the added transactions.</returns>
			/// <exception cref="InvalidOperationException">
			/// Thrown if any transaction in the <paramref name="transactionIdentityList"/> already exists in the container.
			/// </exception>
			/// <exception cref="ArgumentException">
			///  Thrown if the <paramref name="transactionIdentityList"/> is null or empty.
			///  </exception>
			/// <remarks>
			/// This operation ensures that either all transactions are successfully added, or none are.
			/// It's a blocking operation in terms of throwing an exception on conflict.
			/// </remarks>
			public MetranTransactionList<T> AddTransactionList(HashSet<T> transactionIdentityList) {
				if (transactionIdentityList.Count == 0) {
					throw new ArgumentException("Transaction identity list cannot be empty.", nameof(transactionIdentityList));
				}
				var addedList = new HashSet<MetranTransaction<T>>();

				if (transactionIdentityList == null || !transactionIdentityList.Any()) {
					throw new ArgumentException("Transaction identity list cannot be null or empty.", nameof(transactionIdentityList));
				}

				foreach (var id in transactionIdentityList) {
					if (!_bag.ContainsKey(id)) continue;
					foreach (var tran in addedList) tran.Dispose();
					throw new InvalidOperationException("Transaction already exists: " + string.Join(", ", transactionIdentityList));
				}

				foreach (var id in transactionIdentityList) {
					var newTran = new MetranTransaction<T>(id, ref _bag);
					if (_bag.TryAdd(id, newTran)) {
						addedList.Add(newTran);
					}
					else {
						foreach (var tran in addedList) tran.Dispose();
						newTran.Dispose(); // Also dispose the one that failed to add
						throw new InvalidOperationException("Transaction already exists due to concurrent operation: " + id);
					}
				}

				return new MetranTransactionList<T>(addedList, ref _bag);
			}

			/// <summary>
			/// Determines whether the container contains an active transaction with the specified identity.
			/// </summary>
			/// <param name="transactionIdentity">The identity of the transaction to locate.</param>
			/// <returns>
			/// <see langword="true"/> if the container contains an element with the specified identity;
			/// otherwise, <see langword="false"/>.
			/// </returns>
			public bool HasTransaction(T transactionIdentity) {
				return _bag.ContainsKey(transactionIdentity);
			}

			/// <summary>
			/// Attempts to remove and dispose the transaction with the specified identity from the container.
			/// </summary>
			/// <param name="transactionIdentity">The identity of the transaction to remove.</param>
			/// <returns>
			/// <see langword="true"/> if the transaction was successfully removed and disposed;
			/// <see langword="false"/> if the transaction was not found in the container.
			/// </returns>
			/// <remarks>
			/// This method is less commonly used than the `using` statement with `IDisposable`
			/// for `MetranTransaction` and `MetranTransactionList`, as `Dispose()` is the
			/// intended mechanism for releasing transactions. This method provides an explicit
			/// way to force removal.
			/// </remarks>
			public bool RemoveTransaction(T transactionIdentity) {
				if (!_bag.TryRemove(transactionIdentity, out var val)) return false;
				val.Dispose();
				return true;
			}
		}
	}