# AsyncKeyedLock
An asynchronous .NET Standard 2.0 library that allows you to lock based on a key.

## Installation
The recommended means is to use [NuGet](https://www.nuget.org/packages/AsyncKeyedLock), but you could also download the source code from [here](https://github.com/MarkCiliaVincenti/AsyncKeyedLock/releases).

## Usage
```csharp
using (var lockObj = await AsyncKeyedLocker.LockAsync(myObject))
{
	...
}
```

You can also set the maximum number of requests for the semaphore that can be granted concurrently (set to 1 by default):
```csharp
AsyncKeyedLocker.MaxCount = 2;
```

If you would like to see how many concurrent requests there are for a semaphore for a given key:
```csharp
int myCount = AsyncKeyedLocker.GetCount(myObject);
```

And if for some reason you need to force release the requests in the semaphore for a key:
```csharp
AsyncKeyedLocker.ForceRelease(myObject);
```

## Credits
This library is based on [Stephen Cleary's solution](https://stackoverflow.com/questions/31138179/asynchronous-locking-based-on-a-key/31194647#31194647).