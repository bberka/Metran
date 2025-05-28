# Metran

Thread-safe, in-memory transaction manager.

### Latest Version

**v3.0 (Breaking Changes)**

### What it does?

- Metran provides a thread-safe transaction manager that wraps `ConcurrentDictionary` for easy in-memory transaction management.
- Its primary use case is to prevent concurrent execution of a specific operation by a single user or a group of users within the same application instance. For example, blocking a user from calling the same endpoint multiple times simultaneously.
- This project's goal is to offer a straightforward wrapper around `ConcurrentDictionary` to manage processing IDs in memory.
- **Important:** Metran is designed for single-instance applications and does not support distributed systems.

## Breaking Changes in v3.0

-   **Removed `ForceAddTransaction(T transactionIdentity)`:** This method, which previously allowed overwriting an existing transaction, has been removed to align with the core principle of managing unique, non-overlapping locks.
-   **Renamed `ThrowOrAddTransaction` to `AddTransaction`:** This method is now the standard way to explicitly add a single transaction, throwing an `InvalidOperationException` if a transaction with the same ID already exists.
-   **Renamed `ForceAddTransactionList` to `AddTransactionList`:** Similar to `AddTransaction`, this method is now the standard way to atomically add a batch of transactions, throwing an `InvalidOperationException` if any of the provided IDs are already in use.

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
  // AddTransaction: Attempts to add a new transaction for userId.
  // Throws InvalidOperationException if userId is already associated with an active transaction.
  using var metran = Container.AddTransaction(userId); 

  // Do your business logic here.
  // While 'metran' is in scope, 'userId' is considered "in transaction" and locked.
}
```

**Alternative Transaction Creation Methods:**

-   **`GetOrAddTransaction(T transactionIdentity)`:** Returns an existing transaction if found. If no transaction with the given `transactionIdentity` exists, it atomically adds and returns a new one. This method is idempotent and thread-safe, making it suitable for scenarios where you want to proceed with an existing transaction or create a new one if it's free.
-   **`TryAddTransaction(T transactionIdentity, out MetranTransaction<T> transaction)`:** Attempts to add a new transaction. Returns `true` if successful (i.e., the transaction was added because it didn't exist) and sets `transaction`, `false` otherwise (if a transaction with the same ID already exists). This method is non-blocking and safe for concurrent attempts to acquire a lock, allowing for custom handling of conflicts without exceptions.

### 3. Transaction Usage with a List of IDs

For operations involving multiple IDs that need to be locked atomically, use the `AddTransactionList` or `TryAddTransactionList` methods:

```csharp
public void DoSomethingWithMultipleUsers(List<long> userIds)
{
  // AddTransactionList: Attempts to add transactions for all provided IDs as a single atomic unit.
  // If ANY of the IDs in the HashSet already exist as active transactions,
  // it will throw an InvalidOperationException, and none of the transactions will be added.
  using var metranList = Container.AddTransactionList(userIds.ToHashSet()); 

  // Do your business logic here.
  // While 'metranList' is in scope, all 'userIds' are considered "in transaction" and locked.
}
```

**Alternative Transaction List Creation Method:**

-   **`TryAddTransactionList(HashSet<T> transactionIdentityList, out MetranTransactionList<T> transactionList)`:** Attempts to add transactions for all provided IDs atomically. Returns `true` if all are successfully added (meaning none of them existed previously) and sets `transactionList`. Returns `false` otherwise (if even one ID already exists or the list is empty). If `false` is returned, no transactions are ultimately held, and any partially added transactions are automatically disposed to maintain atomicity. This method is non-blocking and ideal for concurrent lock acquisition where you want to handle conflicts gracefully.

### 4. Checking for Existing Transactions

```csharp
public bool IsUserProcessing(long userId)
{
  // HasTransaction: Checks if a transaction with the given userId is currently active.
  return MetranContainer.Container.HasTransaction(userId);
}
```

### 5. Waiting for Transactions to Complete

Metran provides asynchronous waiting capabilities for individual transactions and lists of transactions. This is useful when you need to wait for an ongoing operation (identified by a transaction) to complete before proceeding.

#### Waiting for a Single Transaction

```csharp
public async Task WaitForUserOperation(long userId)
{
    MetranTransaction<long> transaction;
    // Attempt to add a transaction. If it already exists, 'TryAddTransaction' will return false,
    // and we can then choose to wait for the existing one.
    if (Container.TryAddTransaction(userId, out transaction))
    {
        // This transaction was just added, meaning no one else is currently processing for this user.
        // Perform your operation, then dispose the transaction using a 'using' statement.
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
        transaction = Container.GetOrAddTransaction(userId); 
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
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Waiting for user {userId} operation was cancelled.");
        }
    }
}
```

**`WaitAsync` and `SafeWaitAsync` parameters:**

-   `waitDelayMiliseconds` (default: 100): The delay in milliseconds between checks for the transaction's completion.
-   `timeoutMiliseconds` (default: 10000): The maximum time in milliseconds to wait before throwing a `TimeoutException` (for `WaitAsync`) or returning `false` (for `SafeWaitAsync`).
-   `cancellationToken` (default: `CancellationToken.None`): A `CancellationToken` to cancel the wait operation.

#### Waiting for a List of Transactions

When dealing with `MetranTransactionList`, the typical pattern is to acquire all locks atomically using `TryAddTransactionList` or `AddTransactionList`. If `TryAddTransactionList` fails, it means one or more transactions are already active, and you might choose to handle the failure (e.g., return an error, retry after a delay) rather than waiting for an arbitrarily overlapping set of transactions.

The `WaitAllAsync` and `SafeWaitAllAsync` methods for `MetranTransactionList` are primarily intended to be used *after* you have successfully acquired all locks (e.g., within the `using` block of a `MetranTransactionList`) to wait for a *subset* of those transactions to complete, or if you explicitly know transactions for a list of IDs should be active and want to wait for them to finish.

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
        // you would need to retrieve them individually using Container.GetOrAddTransaction()
        // and then call individual `SafeWaitAsync` on each, or implement a loop that retries
        // `TryAddTransactionList` after a delay.
    }
}
```

**`WaitAllAsync` and `SafeWaitAllAsync` parameters:**

-   `waitDelayMiliseconds` (default: 100): The delay in milliseconds between checks for the transactions' completion.
-   `timeoutMiliseconds` (default: 10000): The maximum time in milliseconds to wait before throwing a `TimeoutException` (for `WaitAllAsync`) or returning `false` (for `SafeWaitAllAsync`).
-   `cancellationToken` (default: `CancellationToken.None`): A `CancellationToken` to cancel the wait operation.

---

# Changelog

## v3.0 (Breaking Changes)

-   **Removed `ForceAddTransaction(T transactionIdentity)`:** This method was removed as its behavior of overwriting existing transactions did not align with the library's goal of managing unique concurrent locks.
-   **Renamed `ThrowOrAddTransaction` to `AddTransaction`:** This change makes `AddTransaction` the primary method for adding a single transaction, explicitly throwing an `InvalidOperationException` if a conflict occurs.
-   **Renamed `ForceAddTransactionList` to `AddTransactionList`:** This change makes `AddTransactionList` the primary method for atomically adding a batch of transactions, throwing an `InvalidOperationException` if any conflict occurs within the batch.
-   **Corrected `HasTransaction` implementation:** Removed unnecessary `lock` keyword, as `ConcurrentDictionary.ContainsKey` is already thread-safe.
-   **Enhanced XML Documentation:** Added comprehensive XML documentation to all public methods and classes for improved clarity and IntelliSense support.
-   **Refined internal `TryAddTransaction` and `AddTransaction` logic:** Utilized `ConcurrentDictionary.TryAdd` more directly for atomic operations where applicable.
-   **Minor `MetranTransactionList.TryAddTransactionList` refinement:** Added an early exit check for null or empty `transactionIdentityList`.
-   **Added asynchronous waiting capabilities:**
  -   `MetranTransaction<T>.WaitAsync()`: Waits for a single transaction to complete, throwing a `TimeoutException` on timeout.
  -   `MetranTransaction<T>.SafeWaitAsync()`: Waits for a single transaction to complete, returning `false` on timeout instead of throwing an exception.
  -   `MetranTransactionList<T>.WaitAllAsync()`: Waits for all transactions in the list to complete, throwing a `TimeoutException` on timeout.
  -   `MetranTransactionList<T>.SafeWaitAllAsync()`: Waits for all transactions in the list to complete, returning `false` on timeout instead of throwing an exception.
-   **Improved `MetranTransactionList` performance and consistency:** Now uses `HashSet<MetranTransaction<T>>` internally for storing the list of transactions, providing efficient lookups and set operations.

## v2.0

-   Removed retry functions; retry logic should be handled by the consuming application.
-   Deleted `BeginTransaction` methods.
-   Added `TryAddTransaction` and `TryAddTransactionList` methods for non-blocking transaction creation.
-   Added more methods to `MetranContainer` for greater flexibility in transaction management.