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
  // ForceAddTransaction: Attempts to add a transaction. If an identical
  // transactionIdentity already exists, it will throw an exception.
  using var metran = Container.ForceAddTransaction(userId); 

  // Do your business logic here.
  // While 'metran' is in scope, 'userId' is considered "in transaction".
}
```

**Alternative Transaction Creation Methods:**

- **`ThrowOrAddTransaction(T transactionIdentity)`:** Throws an `Exception` if the transaction already exists.
- **`GetOrAddTransaction(T transactionIdentity)`:** Returns the existing transaction if found, otherwise adds and returns a new one. This method is idempotent.
- **`TryAddTransaction(T transactionIdentity, out MetranTransaction<T> transaction)`:** Attempts to add a transaction. Returns `true` if successful and sets `transaction`, `false` otherwise. This method is non-blocking.

### 3. Transaction Usage with a List of IDs

For operations involving multiple IDs that need to be locked atomically, use the `ForceAddTransactionList` or `TryAddTransactionList` methods:

```csharp
public void DoSomethingWithMultipleUsers(List<long> userIds)
{
  // ForceAddTransactionList: Attempts to add transactions for all provided IDs.
  // If ANY of the IDs in the HashSet already exist as active transactions,
  // it will throw an exception, and none of the transactions will be added.
  using var metranList = Container.ForceAddTransactionList(userIds.ToHashSet()); 

  // Do your business logic here.
  // While 'metranList' is in scope, all 'userIds' are considered "in transaction".
}
```

**Alternative Transaction List Creation Method:**

- **`TryAddTransactionList(HashSet<T> transactionIdentityList, out MetranTransactionList<T> transactionList)`:** Attempts to add transactions for all provided IDs. Returns `true` if all are successfully added and sets `transactionList`, `false` otherwise (if even one ID already exists). If `false` is returned, no transactions are added, and any partially added transactions are automatically disposed.

### 4. Waiting for Transactions to Complete

Metran provides asynchronous waiting capabilities for individual transactions and lists of transactions. This is useful when you need to wait for an ongoing operation (identified by a transaction) to complete before proceeding.

#### Waiting for a Single Transaction

```csharp
public async Task WaitForUserOperation(long userId)
{
    MetranTransaction<long> transaction;
    // Attempt to get an existing transaction, or add a new one if it doesn't exist.
    // You might use TryAddTransaction or GetOrAddTransaction based on your logic.
    if (Container.TryAddTransaction(userId, out transaction))
    {
        // This transaction was just added, meaning no one else is currently processing for this user.
        // Perform your operation, then dispose the transaction.
        using (transaction)
        {
            // Do some work...
        }
    }
    else
    {
        // A transaction for this userId already exists, meaning another operation is ongoing.
        // Get the existing transaction to wait for it.
        transaction = Container.GetOrAddTransaction(userId); // Get the existing one
        try
        {
            // Wait for the existing transaction to complete (be disposed).
            // This will throw a TimeoutException if it doesn't complete within 10 seconds.
            await transaction.WaitAsync(); 
            Console.WriteLine($"Transaction for user {userId} completed.");
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"Waiting for transaction for user {userId} timed out: {ex.Message}");
        }
    }
}
```

**`WaitAsync` and `SafeWaitAsync` parameters:**

- `waitDelayMiliseconds` (default: 100): The delay between checks for the transaction's completion.
- `timeoutMiliseconds` (default: 10000): The maximum time to wait before throwing a `TimeoutException` (for `WaitAsync`) or returning `false` (for `SafeWaitAsync`).
- `cancellationToken` (default: `CancellationToken.None`): A cancellation token to abort the wait operation.

#### Waiting for a List of Transactions

```csharp
public async Task WaitForMultipleUserOperations(List<long> userIds)
{
    MetranTransactionList<long> transactionList;

    if (Container.TryAddTransactionList(userIds.ToHashSet(), out transactionList))
    {
        // All transactions were just added. Perform your operations.
        using (transactionList)
        {
            // Do some work...
        }
    }
    else
    {
        // One or more transactions already existed.
        // You would typically handle this by deciding if you want to wait for the existing ones,
        // or return an error indicating concurrent access.
        // For demonstration, let's assume you want to wait for them to finish.
        // Note: You would need to retrieve the individual transactions or handle the case
        // where you couldn't acquire all locks in the first place.
        // The most common pattern is to either succeed in acquiring all locks or fail immediately.
        // If you need to wait for existing locks, you'd likely use individual `SafeWaitAsync` calls
        // or re-attempt the `TryAddTransactionList` after a delay.

        Console.WriteLine("One or more transactions for the given user IDs already exist.");
        // Example: If you *must* wait for them even if you couldn't acquire them all initially,
        // you would need to implement a retry logic or individual waits.
        // The current design of ForceAdd/TryAdd focuses on acquiring all locks or none.
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