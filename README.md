# Metran

Thread-safe transaction manager working in memory

### Version

Version kept as low as possible to support older .NET versions

.NET 4.6 or higher

C# 7.0 or higher

## What it does ?

Metran provides simple yet useful thread-safe locker

## What is the goal ?

Goal is removing load from database or any other source and using ConcurrentBag to determine if given transaction id is
processing

By using the "using" keyword you only have to BeginTransaction in metran container by providing an id

This way once you exit out of the method transaction will be automatically disposed and lock will be released

Metran does not do what "lock" keyword does. It does not lock current thread or any thread.

It's purpose is to lock certain users for instance calling same method/endpoint more than once.

Let's say you are going to do some financial database actions and you want user to be able to only call the method once.
And if method is in processing you don't want second instance of the method running.
Completely avoiding any overlap in db or any other business logic.

Remember this does NOT remove the need to use database transactions. It simply provides a locker with an id (in most
cases user id or something else)

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
  //accepts long or HashSet<long>
  using var metran = Container.BeginTransaction(userId); 

  //Only returns null if transaction already exists
  if (metran == null) {
    //Must check for null or throw with ArgumentNullException
    //Console.WriteLine($"[{userId}]Transaction already exists");
    return;
  }
    
  //Do your stuff here
}
```

Usage with HashSet
```csharp
public void DoSomething(List<long> userIds)
{
  //accepts long or HashSet<long>
  using var metran = Container.BeginTransaction(userIds.ToHashSet()); 

  //Only returns null if any transaction with same id already exists
  //All ids provided must be free to use for transaction to be created
  if (metran == null) {
    //Must check for null or throw with ArgumentNullException
    //Console.WriteLine($"[{userId}]Transaction already exists");
    return;
  }
    
  //Do your stuff here
}
```