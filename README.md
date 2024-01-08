# Metran
Thread-safe transaction manager working in memory

## What it does ? 
Metran provides simple yet useful thread-safe locker 

## What is the goal ? 
Goal is removing load from database or any other source and using ConcurrentBag to determine if given transaction id is processing

By using the "using" keyword you only have to BeginTransaction in metran container by providing an id

This way once you exit out of the method transaction will be automatically disposed and lock will be released

Metran does not do what "lock" keyword does. It does not lock current thread or any thread.

It's purpose is to lock certain users for instance calling same method/endpoint more than once. 

Let's say you are going to do some financial database actions and you want user to be able to only call the method once.
And if method is in processing you don't want second instance of the method running.
Completely avoiding any overlap in db or any other business logic. 

Remember this does NOT remove the need to use database transactions. It simply provides a locker with an id (in most cases user id or something else9

## How to use ? 
You can check the example project

WIP...
