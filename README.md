# ![AsyncKeyedLock](https://raw.githubusercontent.com/MarkCiliaVincenti/AsyncKeyedLock/master/logo32.png) AsyncKeyedLock
[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/MarkCiliaVincenti/AsyncKeyedLock/dotnet.yml?branch=master&logo=github&style=for-the-badge)](https://actions-badge.atrox.dev/MarkCiliaVincenti/AsyncKeyedLock/goto?ref=master) [![Nuget](https://img.shields.io/nuget/v/AsyncKeyedLock?label=AsyncKeyedLock&logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/AsyncKeyedLock) [![Nuget](https://img.shields.io/nuget/dt/AsyncKeyedLock?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/AsyncKeyedLock)

An asynchronous .NET Standard 2.0 library that allows you to lock based on a key (keyed semaphores), limiting concurrent threads sharing the same key to a specified number.

For example, suppose you were processing financial transactions, but while working on one account you wouldn't want to concurrently process a transaction for the same account. Of course, you could just add a normal lock, but then you can only process one transaction at a time. If you're processing a transaction for account A, you may want to also be processing a separate transaction for account B. That's where AsyncKeyedLock comes in: it allows you to lock but only if the key matches.

## Installation
The recommended means is to use [NuGet](https://www.nuget.org/packages/AsyncKeyedLock), but you could also download the source code from [here](https://github.com/MarkCiliaVincenti/AsyncKeyedLock/releases).

## Usage
You need to start off with creating an instance of `AsyncKeyedLocker` or `AsyncKeyedLocker<T>`. The recommended way is to use the latter, which is faster and consumes less memory. The former uses `object` and can be used to mix different types of objects.

### Dependency injection
```csharp
services.AddSingleton<AsyncKeyedLocker>();
```

or (recommended):

```csharp
services.AddSingleton<AsyncKeyedLocker<string>>();
```

### Variable instantiation
```csharp
var asyncKeyedLocker = new AsyncKeyedLocker();
```

or (recommended):

```csharp
var asyncKeyedLocker = new AsyncKeyedLocker<string>();
```

or if you would like to set the maximum number of requests for the semaphore that can be granted concurrently (set to 1 by default):

```csharp
// using AsyncKeyedLockOptions
var asyncKeyedLocker1 = new AsyncKeyedLocker<string>(new AsyncKeyedLockOptions(maxCount: 2));

// using Action<AsyncKeyedLockOptions>
var asyncKeyedLocker2 = new AsyncKeyedLocker<string>(o => o.MaxCount = 2);
```

There are also AsyncKeyedLocker<TKey>() constructors which accept the parameters of ConcurrentDictionary, namely the concurrency level, the capacity and the IEqualityComparer<TKey> to use.

### Pooling
Whenever a lock needs to be acquired for a key that is not currently being processed, an `AsyncKeyedLockReleaser` object needs to exist for that key and added to a `ConcurrentDictionary`. In order to reduce allocations having to create objects only to dispose of them shortly after, `AsyncKeyedLock` allows for object pooling. Whenever a new key is needed, it is taken from the pool (rather than created from scratch). If the pool is empty, a new object is created. This means that the pool will not throttle or limit the number of keys being concurrently processed. Once a key is no longer in use, the `AsyncKeyedLockReleaser` object is returned back to the pool, unless the pool is already full up.

Usage of the pool can lead to big performance gains, but it can also very easily lead to inferior performance. If the pool is too small, the benefit from using the pool might be outweighed by the extra overhead from the pool itself. If, on the other hand, the pool is too big, then that's a number of objects in memory for nothing, consuming memory.

It is recommended to run benchmarks and tests if you intend on using pooling to make sure that you choose an optimal pool size.

Setting the pool size can be done via the `AsyncKeyedLockOptions` in one of the overloaded constructors, such as this:

```csharp
// using AsyncKeyedLockOptions
var asyncKeyedLocker1 = new AsyncKeyedLocker<string>(new AsyncKeyedLockOptions(poolSize: 100));

// using Action<AsyncKeyedLockOptions>
var asyncKeyedLocker2 = new AsyncKeyedLocker<string>(o => o.PoolSize = 100);
```

You can also set the initial pool fill (by default this is set to the pool size):

```csharp
// using AsyncKeyedLockOptions
var asyncKeyedLocker = new AsyncKeyedLocker<string>(new AsyncKeyedLockOptions(poolSize: 100, poolInitialFill: 50));

// using Action<AsyncKeyedLockOptions>
var asyncKeyedLocker = new AsyncKeyedLocker<string>(o =>
{
	o.PoolSize = 100;
	o.PoolInitialFill = 50;
});
```

### Locking
```csharp
// without cancellation token
using (await asyncKeyedLocker.LockAsync(myObject))
{
	...
}

// with cancellation token
using (await asyncKeyedLocker.LockAsync(myObject, cancellationToken))
{
	...
}
```

You can also use timeouts with overloaded methods to set the maximum time to wait, either in milliseconds or as a `TimeSpan`.

In the case you need to use timeouts to instead give up if unable to obtain a lock by a certain amount of time, you can also use `TryLockAsync` methods which will call a `Func<Task>` or `Action` if the timeout is not expired, whilst returning a boolean representing whether or not it waited successfully.

There are also synchronous `Lock` and `TryLock` methods available.

If you would like to see how many concurrent requests there are for a semaphore for a given key:
```csharp
int myRemainingCount = asyncKeyedLocker.GetRemainingCount(myObject);
```

If you would like to see the number of remaining threads that can enter the lock for a given key:
```csharp
int myCurrentCount = asyncKeyedLocker.GetCurrentCount(myObject);
```

If you would like to check whether any request is using a specific key:
```csharp
bool isInUse = asyncKeyedLocker.IsInUse(myObject);
```

## Credits
Check out our [list of contributors](https://github.com/MarkCiliaVincenti/AsyncKeyedLock/blob/master/CONTRIBUTORS.md)!