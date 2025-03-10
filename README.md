# Metran

Thread-safe transaction manager working in memory

### Version

Almost all .NET versions are targeted

## What it does ?

- Metran provides a thread-safe transaction manager that basically wraps ConcurrentDictionary to provide ease of use
- Simple usage reasons is simply locking one or two users from calling same method concurrently. 
- This moves handling of this to memory. 
- Obviously this does not work with distributed systems.
- This projects goal is to provide a simple wrapper around ConcurrentDictionary

## How to use ?

You can check the example project

Define MetranContainer

```csharp
public static readonly MetranContainer<long> Container = new();
```

Use it in your method

```csharp

public void DoSomething(long userId)
{
  //accepts long
  using var metran = Container.ForceAddTransaction(userId); 

  //Only throws if transaction already exists
  //Only throws if any transaction with same id already exists
  //All ids provided must be free to use for transaction to be created
  //Must check for exceptions
    
  //Do your stuff here
}
```

Usage with HashSet

```csharp
public void DoSomething(List<long> userIds)
{
  //accepts long or HashSet<long>
  using var metran = Container.ForceAddTransactionList(userIds.ToHashSet()); 

  //Only throws if any transaction with same id already exists
  //All ids provided must be free to use for transaction to be created
  //Must check for exceptions
    
  //Do your stuff here
}
```

# Changelog

## v2.0
- Removed retry functions, this needs to be handled outside of the library
- Deleted BeginTransaction methods
- Added TryAddTransaction and TryAddTransactionList
- Adde more methods to MetranContainer