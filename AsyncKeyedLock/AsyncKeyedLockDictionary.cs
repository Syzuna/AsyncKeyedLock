﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AsyncKeyedLock
{
    internal sealed class AsyncKeyedLockDictionary<TKey> : ConcurrentDictionary<TKey, AsyncKeyedLockReleaser<TKey>>, IDisposable
    {
        public int MaxCount { get; private set; } = 1;
        private readonly AsyncKeyedLockPool<TKey> _pool;
        internal bool PoolingEnabled { get; private set; }

        public AsyncKeyedLockDictionary() : base()
        {
        }

        public AsyncKeyedLockDictionary(AsyncKeyedLockOptions options) : base()
        {
            if (options.MaxCount < 1) throw new ArgumentOutOfRangeException(nameof(options), options.MaxCount, $"{nameof(options.MaxCount)} should be greater than or equal to 1.");

            MaxCount = options.MaxCount;
            if (options.PoolSize > 0)
            {
                PoolingEnabled = true;
                _pool = new AsyncKeyedLockPool<TKey>((key) => new AsyncKeyedLockReleaser<TKey>(key, new SemaphoreSlim(MaxCount, MaxCount), this), options.PoolSize, options.PoolInitialFill);
            }
        }

        public AsyncKeyedLockDictionary(IEqualityComparer<TKey> comparer) : base(comparer)
        {
        }

        public AsyncKeyedLockDictionary(AsyncKeyedLockOptions options, IEqualityComparer<TKey> comparer) : base(comparer)
        {
            if (options.MaxCount < 1) throw new ArgumentOutOfRangeException(nameof(options), options.MaxCount, $"{nameof(options.MaxCount)} should be greater than or equal to 1.");

            MaxCount = options.MaxCount;
            if (options.PoolSize > 0)
            {
                PoolingEnabled = true;
                _pool = new AsyncKeyedLockPool<TKey>((key) => new AsyncKeyedLockReleaser<TKey>(key, new SemaphoreSlim(MaxCount, MaxCount), this), options.PoolSize, options.PoolInitialFill);
            }
        }

        public AsyncKeyedLockDictionary(int concurrencyLevel, int capacity) : base(concurrencyLevel, capacity)
        {
        }

        public AsyncKeyedLockDictionary(AsyncKeyedLockOptions options, int concurrencyLevel, int capacity) : base(concurrencyLevel, capacity)
        {
            if (options.MaxCount < 1) throw new ArgumentOutOfRangeException(nameof(options), options.MaxCount, $"{nameof(options.MaxCount)} should be greater than or equal to 1.");

            MaxCount = options.MaxCount;
            if (options.PoolSize > 0)
            {
                PoolingEnabled = true;
                _pool = new AsyncKeyedLockPool<TKey>((key) => new AsyncKeyedLockReleaser<TKey>(key, new SemaphoreSlim(MaxCount, MaxCount), this), options.PoolSize, options.PoolInitialFill);
            }
        }

        public AsyncKeyedLockDictionary(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer) : base(concurrencyLevel, capacity, comparer)
        {

        }

        public AsyncKeyedLockDictionary(AsyncKeyedLockOptions options, int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer) : base(concurrencyLevel, capacity, comparer)
        {
            if (options.MaxCount < 1) throw new ArgumentOutOfRangeException(nameof(options), options.MaxCount, $"{nameof(options.MaxCount)} should be greater than or equal to 1.");

            MaxCount = options.MaxCount;
            if (options.PoolSize > 0)
            {
                PoolingEnabled = true;
                _pool = new AsyncKeyedLockPool<TKey>((key) => new AsyncKeyedLockReleaser<TKey>(key, new SemaphoreSlim(MaxCount, MaxCount), this), options.PoolSize, options.PoolInitialFill);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AsyncKeyedLockReleaser<TKey> GetOrAdd(TKey key)
        {
            if (PoolingEnabled)
            {
                if (TryGetValue(key, out var releaser) && releaser.TryIncrement(key))
                {
                    return releaser;
                }

                var releaserToAdd = _pool.GetObject(key);
                if (TryAdd(key, releaserToAdd))
                {
                    return releaserToAdd;
                }

                while (true)
                {
                    releaser = GetOrAdd(key, releaserToAdd);
                    if (ReferenceEquals(releaser, releaserToAdd))
                    {
                        return releaser;
                    }
                    if (releaser.TryIncrement(key))
                    {
                        releaserToAdd.IsNotInUse = true;
                        _pool.PutObject(releaserToAdd);
                        return releaser;
                    }
                }
            }

            if (TryGetValue(key, out var releaserNoPooling) && releaserNoPooling.TryIncrementNoPooling())
            {
                return releaserNoPooling;
            }

            var releaserToAddNoPooling = new AsyncKeyedLockReleaser<TKey>(key, new SemaphoreSlim(MaxCount, MaxCount), this);
            if (TryAdd(key, releaserToAddNoPooling))
            {
                return releaserToAddNoPooling;
            }

            while (true)
            {
                releaserNoPooling = GetOrAdd(key, releaserToAddNoPooling);
                if (ReferenceEquals(releaserNoPooling, releaserToAddNoPooling) || releaserNoPooling.TryIncrementNoPooling())
                {
                    return releaserNoPooling;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release(AsyncKeyedLockReleaser<TKey> releaser)
        {
            Monitor.Enter(releaser);

            if (releaser.ReferenceCount == 1)
            {
                TryRemove(releaser.Key, out _);
                releaser.IsNotInUse = true;
                Monitor.Exit(releaser);
                if (PoolingEnabled)
                {
                    _pool.PutObject(releaser);
                }
                releaser.SemaphoreSlim.Release();
                return;
            }

            --releaser.ReferenceCount;
            Monitor.Exit(releaser);
            releaser.SemaphoreSlim.Release();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseWithoutSemaphoreRelease(AsyncKeyedLockReleaser<TKey> releaser)
        {
            Monitor.Enter(releaser);

            if (releaser.ReferenceCount == 1)
            {
                TryRemove(releaser.Key, out _);
                releaser.IsNotInUse = true;
                Monitor.Exit(releaser);
                if (PoolingEnabled)
                {
                    _pool.PutObject(releaser);
                }
                return;
            }
            --releaser.ReferenceCount;
            Monitor.Exit(releaser);
        }

        public void Dispose()
        {
            foreach (var semaphore in Values)
            {
                try
                {
                    semaphore.Dispose();
                }
                catch
                {
                    // do nothing
                }
            }
            Clear();
            if (PoolingEnabled)
            {
                _pool.Dispose();
            }
        }
    }
}
