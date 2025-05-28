# Metran

Thread-safe, in-memory transaction manager.

### Version

Almost all .NET versions are targeted.

## What it does?

- Metran provides a thread-safe transaction manager that wraps `ConcurrentDictionary` for easy in-memory transaction management.
- Its primary use case is to prevent concurrent execution of a specific operation by a single user or a group of users within the same application instance. For example, blocking a user from calling the same endpoint multiple times simultaneously.
- This project's goal is to offer a straightforward wrapper around `ConcurrentDictionary` to manage processing IDs in memory.
- **Important:** Metran is designed for single-instance applications and does not support distributed systems.

## How to use?

You can check the example project for detailed usage.

### 1. Define MetranContainer

Start by defining a `MetranContainer` instance, typically as a static field for application-wide access:

```csharp
public static readonly MetranContainer<long> Container = new();
```

### 2. Basic Transaction Usage (Single ID)

Use the `MetranContainer` within your methods to manage transactions. When the `using` block exits, the transaction is automatically disposed, releasing the ID.

```csharp
public void DoSomething(long userId)
{
  // ForceAddTransaction: Creates and adds a new transaction.
  // If an identical transactionIdentity already exists, it will overwrite the existing one.
  using var metran = Container.ForceAddTransaction(userId); 

  // Do your business logic here.
  // While 'metran' is in scope, 'userId' is considered "in transaction".
}
```

**Alternative Transaction Creation Methods:**

- **`ThrowOrAddTransaction(T transactionIdentity)`:** Attempts to add a new transaction. Throws an `Exception` if a transaction with the identical `transactionIdentity` already exists. This method ensures uniqueness.
- **`GetOrAddTransaction(T transactionIdentity)`:** Returns the existing transaction if found. If no transaction with the given `transactionIdentity` exists, it adds and returns a new one. This method is idempotent.
- **`TryAddTransaction(T transactionIdentity, out MetranTransaction<T> transaction)`:** Attempts to add a transaction. Returns `true` if successful (i.e., the transaction was added because it didn't exist) and sets `transaction`, `false` otherwise (if a transaction with the same ID already exists). This method is non-blocking and safe for concurrent attempts to add.

### 3. Transaction Usage with a List of IDs

For operations involving multiple IDs that need to be locked atomically, use the `ForceAddTransactionList` or `TryAddTransactionList` methods:

```csharp
public void DoSomethingWithMultipleUsers(List<long> userIds)
{
  // ForceAddTransactionList: Attempts to add transactions for all provided IDs.
  // If ANY of the IDs in the HashSet already exist as active transactions,
  // it will throw an exception, and none of the transactions will be added.
  // This method ensures atomicity: either all transactions are added, or none are.
  using var metranList = Container.ForceAddTransactionList(userIds.ToHashSet()); 

  // Do your business logic here.
  // While 'metranList' is in scope, all 'userIds' are considered "in transaction".
}
```

**Alternative Transaction List Creation Method:**

- **`TryAddTransactionList(HashSet<T> transactionIdentityList, out MetranTransactionList<T> transactionList)`:** Attempts to add transactions for all provided IDs. Returns `true` if all are successfully added (meaning none of them existed previously) and sets `transactionList`. Returns `false` otherwise (if even one ID already exists). If `false` is returned, no transactions are ultimately held, and any partially added transactions are automatically disposed to maintain atomicity.

### 4. Waiting for Transactions to Complete

Metran provides asynchronous waiting capabilities for individual transactions and lists of transactions. This is useful when you need to wait for an ongoing operation (identified by a transaction) to complete before proceeding.

#### Waiting for a Single Transaction

```csharp
public async Task WaitForUserOperation(long userId)
{
    MetranTransaction<long> transaction;
    // Attempt to add a transaction if it doesn't exist.
    // If it already exists, 'TryAddTransaction' will return false,
    // and we can then choose to wait for the existing one.
    if (Container.TryAddTransaction(userId, out transaction))
    {
        // This transaction was just added, meaning no one else is currently processing for this user.
        // Perform your operation, then dispose the transaction.
        using (transaction)
        {
            Console.WriteLine($"Starting operation for user {userId}...");
            await Task.Delay(2000); // Simulate work
            Console.WriteLine($"Operation for user {userId} completed.");
        }
    }
    else
    {
        // A transaction for this userId already exists, meaning another operation is ongoing.
        // Get the existing transaction to wait for it.
        transaction = Container.GetOrAddTransaction(userId); // Get the existing one (it won't add here)
        try
        {
            Console.WriteLine($"User {userId} is busy. Waiting for existing operation to complete...");
            // Wait for the existing transaction to complete (be disposed).
            // This will throw a TimeoutException if it doesn't complete within 10 seconds.
            await transaction.WaitAsync(); 
            Console.WriteLine($"Existing operation for user {userId} completed. You can now proceed.");
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"Waiting for operation for user {userId} timed out: {ex.Message}");
        }
    }
}
```

**`WaitAsync` and `SafeWaitAsync` parameters:**

- `waitDelayMiliseconds` (default: 100): The delay between checks for the transaction's completion.
- `timeoutMiliseconds` (default: 10000): The maximum time to wait before throwing a `TimeoutException` (for `WaitAsync`) or returning `false` (for `SafeWaitAsync`).
- `cancellationToken` (default: `CancellationToken.None`): A cancellation token to abort the wait operation.

#### Waiting for a List of Transactions

When dealing with `MetranTransactionList`, the typical pattern is to acquire all locks atomically using `TryAddTransactionList` or `ForceAddTransactionList`. If `TryAddTransactionList` fails, it means one or more transactions are already active, and you generally wouldn't *wait* for an arbitrarily overlapping set of transactions. Instead, you'd likely handle the failure (e.g., return an error, retry after a delay).

The `WaitAllAsync` and `SafeWaitAllAsync` methods for `MetranTransactionList` are primarily intended to be used *after* you have successfully acquired all locks (e.g., within the `using` block of a `MetranTransactionList`) to wait for a *subset* of those transactions to complete, or if you passed a list of IDs for which you *know* transactions should be active and want to wait for them to finish.

However, a more common scenario for waiting for a list of transactions to *become available* might involve a retry loop using `TryAddTransactionList`.

```csharp
public async Task TryAcquireAndProcessMultipleUsers(List<long> userIds)
{
    var idsToProcess = userIds.ToHashSet();
    MetranTransactionList<long> transactionList;

    if (Container.TryAddTransactionList(idsToProcess, out transactionList))
    {
        // All transactions were successfully added. Process them.
        using (transactionList)
        {
            Console.WriteLine($"Acquired locks for users: {string.Join(", ", userIds)}. Processing...");
            await Task.Delay(3000); // Simulate work
            Console.WriteLine($"Finished processing for users: {string.Join(", ", userIds)}. Releasing locks.");
        }
    }
    else
    {
        Console.WriteLine($"Could not acquire all locks for users: {string.Join(", ", userIds)}. Some users are busy.");
        // At this point, you might inform the user, log, or implement a retry mechanism.
        // For example, if you wanted to wait for the *specific* IDs that were busy,
        // you would need to iterate through them and call individual `SafeWaitAsync` on each.
        // Or, if this operation is critical, you might retry `TryAddTransactionList` after a delay.
    }
}
```

**`WaitAllAsync` and `SafeWaitAllAsync` parameters:**

- `waitDelayMiliseconds` (default: 100): The delay between checks for all transactions' completion.
- `timeoutMiliseconds` (default: 10000): The maximum time to wait before throwing a `TimeoutException` (for `WaitAllAsync`) or returning `false` (for `SafeWaitAllAsync`).
- `cancellationToken` (default: `CancellationToken.None`): A cancellation token to abort the wait operation.

---

# Changelog

## v2.1

- **Added asynchronous waiting capabilities:**
  - `MetranTransaction<T>.WaitAsync()`: Waits for a single transaction to complete, throwing a `TimeoutException` on timeout.
  - `MetranTransaction<T>.SafeWaitAsync()`: Waits for a single transaction to complete, returning `false` on timeout instead of throwing an exception.
  - `MetranTransactionList<T>.WaitAllAsync()`: Waits for all transactions in the list to complete, throwing a `TimeoutException` on timeout.
  - `MetranTransactionList<T>.SafeWaitAllAsync()`: Waits for all transactions in the list to complete, returning `false` on timeout instead of throwing an exception.
- **Improved `MetranTransactionList` performance and consistency:** Now uses `HashSet<MetranTransaction<T>>` internally for storing the list of transactions, providing efficient lookups and set operations.

## v2.0

- Removed retry functions; retry logic should be handled by the consuming application.
- Deleted `BeginTransaction` methods.
- Added `TryAddTransaction` and `TryAddTransactionList` methods for non-blocking transaction creation.
- Added more methods to `MetranContainer` for greater flexibility in transaction management.