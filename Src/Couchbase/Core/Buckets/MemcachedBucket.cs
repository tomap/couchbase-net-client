﻿ using System;
 using System.Threading;
 using Common.Logging;
 using Couchbase.Configuration;
 using Couchbase.Configuration.Server.Providers;
 using Couchbase.IO;
 using Couchbase.IO.Operations;
 using Couchbase.Views;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Represents an in-memory bucket for storing Key/Value pairs. Most often used as a distributed cache.
    /// </summary>
    public class MemcachedBucket : IBucket, IConfigObserver
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IClusterManager _clusterManager;
        private IConfigInfo _configInfo;
        private volatile bool _disposed;
        private static readonly object SyncObj = new object();
        private readonly IByteConverter _converter;

        internal MemcachedBucket(IClusterManager clusterManager, string bucketName, IByteConverter converter)
        {
            _clusterManager = clusterManager;
            _converter = converter;
            Name = bucketName;
        }

        /// <summary>
        /// The Bucket's name. You can view this from the Couchbase Management Console.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Called when a configuration update has occurred from the server.
        /// </summary>
        /// <param name="configInfo">The new configuration</param>
        void IConfigObserver.NotifyConfigChanged(IConfigInfo configInfo)
        {
            Log.Info(m => m("Updating MemcachedBucket - old config rev#{0} new config rev#{1} on thread {2}",
               _configInfo == null ? 0 : _configInfo.BucketConfig.Rev,
                configInfo.BucketConfig.Rev,
                Thread.CurrentThread.ManagedThreadId));

            lock (SyncObj)
            {
                var old = Interlocked.Exchange(ref _configInfo, configInfo);

                Log.Info(m => m("Updated MemcachedBucket - old config rev#{0} new config rev#{1}",
                    old == null ? 0 : old.BucketConfig.Rev,
                    _configInfo.BucketConfig.Rev));
            }
        }

        /// <summary>
        /// Inserts or replaces an existing document into a Memcached Bucket on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var bucket = keyMapper.MapKey(key);
            var server = bucket.LocatePrimary();

            var operation = new SetOperation<T>(key, value, null, _converter);
            var operationResult = server.Send(operation);
            return operationResult;
        }

        /// <summary>
        /// Replaces a value for a key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var bucket = keyMapper.MapKey(key);
            var server = bucket.LocatePrimary();

            var operation = new ReplaceOperation<T>(key, value, null, _converter);
            var operationResult = server.Send(operation);
            return operationResult;
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var bucket = keyMapper.MapKey(key);
            var server = bucket.LocatePrimary();

            var operation = new AddOperation<T>(key, value, null, _converter);
            var operationResult = server.Send(operation);
            return operationResult;
        }

        /// <summary>
        /// For a given key, removes a document from the database.
        /// </summary>
        /// <param name="key">The unique key for indexing.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<object> Remove(string key)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var bucket = keyMapper.MapKey(key);
            var server = bucket.LocatePrimary();

            var operation = new DeleteOperation(key, null, _converter);
            var operationResult = server.Send(operation);
            return operationResult;
        }

        /// <summary>
        /// Gets a value for a given key from a Memcached Bucket on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Get<T>(string key)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var bucket = keyMapper.MapKey(key);
            var server = bucket.LocatePrimary();

            var operation = new GetOperation<T>(key, null, _converter);
            var operationResult = server.Send(operation);
            return operationResult;
        }

        /// <summary>
        /// Increments the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<long> Increment(string key)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

            return Increment(key, delta, initial, expiration);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<long> Increment(string key, ulong delta)
        {
            const ulong initial = 1;
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

            return Increment(key, delta, initial, expiration);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<long> Increment(string key, ulong delta, ulong initial)
        {
            //infinite - there is also a 'special' value -1: 'don't create if missing'
            const uint expiration = 0;

            return Increment(key, delta, initial, expiration);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<long> Increment(string key, ulong delta, ulong initial, uint expiration)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var bucket = keyMapper.MapKey(key);
            var server = bucket.LocatePrimary();

            var operation = new IncrementOperation(key, initial, delta, expiration, null, _converter);
            var operationResult = server.Send(operation);

            return operationResult;
        }

        /// <summary>
        /// Decrements the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<long> Decrement(string key)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

            return Decrement(key, delta, initial, expiration);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<long> Decrement(string key, ulong delta)
        {
            const ulong initial = 1;
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

            return Decrement(key, delta, initial, expiration);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<long> Decrement(string key, ulong delta, ulong initial)
        {
            //infinite - there is also a 'special' value -1: 'don't create if missing'
            const uint expiration = 0;

            return Decrement(key, delta, initial, expiration);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<long> Decrement(string key, ulong delta, ulong initial, uint expiration)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var bucket = keyMapper.MapKey(key);
            var server = bucket.LocatePrimary();

            var operation = new DecrementOperation(key, initial, delta, expiration, null, _converter);
            var operationResult = server.Send(operation);

            return operationResult;
        }

        public System.Threading.Tasks.Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            throw new NotImplementedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public System.Threading.Tasks.Task<IOperationResult<T>> InsertAsync<T>(string key, T value)
        {
            throw new NotImplementedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewResult<T> Query<T>(IViewQuery query)
        {
            throw new NotImplementedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public N1QL.IQueryResult<T> Query<T>(string query)
        {
            throw new NotImplementedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewQuery CreateQuery(bool development)
        {
            throw new NotImplementedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewQuery CreateQuery(bool development, string designdoc)
        {
            throw new NotImplementedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewQuery CreateQuery(bool development, string designdoc, string view)
        {
            throw new NotImplementedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Closes this <see cref="MemcachedBucket"/> instance, shutting down and releasing all resources, 
        /// removing it from it's <see cref="ClusterManager"/> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Closes this <see cref="MemcachedBucket"/> instance, shutting down and releasing all resources, 
        /// removing it from it's <see cref="ClusterManager"/> instance.
        /// </summary>
        /// <param name="disposing">If true suppresses finalization.</param>
        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _clusterManager.DestroyBucket(this);
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer for this <see cref="MemcachedBucket"/> instance if not shutdown and disposed gracefully. 
        /// </summary>
        ~MemcachedBucket()
        {
            Dispose(false);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion