﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.IO;
using System.Collections.Concurrent;
using QuantConnect.Logging;
using System.Linq;
using Ionic.Zip;
using Ionic.Zlib;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// File provider implements optimized zip archives caching facility. Cache is thread safe.
    /// </summary>
    public class ZipDataCacheProvider : IDataCacheProvider
    {
        private const int CacheSeconds = 10;

        // ZipArchive cache used by the class
        private readonly ConcurrentDictionary<string, CachedZipFile> _zipFileCache = new ConcurrentDictionary<string, CachedZipFile>();
        private DateTime _lastCacheScan = DateTime.MinValue;
        private readonly IDataProvider _dataProvider;

        // Ionic.Zip.ZipFile instances are not thread-safe
        private readonly object _zipFileSynchronizer = new object();

        /// <summary>
        /// Constructor that sets the <see cref="IDataProvider"/> used to retrieve data
        /// </summary>
        public ZipDataCacheProvider(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        /// <summary>
        /// Does not attempt to retrieve any data
        /// </summary>
        public Stream Fetch(string key)
        {
            string entryName = null; // default to all entries
            var filename = key;
            var hashIndex = key.LastIndexOf("#", StringComparison.Ordinal);
            if (hashIndex != -1)
            {
                entryName = key.Substring(hashIndex + 1);
                filename = key.Substring(0, hashIndex);
            }

            // handles zip files
            if (filename.GetExtension() == ".zip")
            {
                Stream stream = null;

                // scan the cache once every 3 seconds
                if (_lastCacheScan == DateTime.MinValue || _lastCacheScan < DateTime.Now.AddSeconds(-3))
                {
                    CleanCache();
                }

                try
                {
                    CachedZipFile existingEntry;
                    if (!_zipFileCache.TryGetValue(filename, out existingEntry))
                    {
                        var dataStream = _dataProvider.Fetch(filename);

                        if (dataStream != null)
                        {
                            try
                            {
                                var newItem = new CachedZipFile(ZipFile.Read(dataStream), filename);

                                lock (_zipFileSynchronizer)
                                {
                                    stream = CreateStream(newItem.ZipFile, entryName);
                                }

                                _zipFileCache.TryAdd(filename, newItem);
                            }
                            catch (Exception exception)
                            {
                                if (exception is ZipException || exception is ZlibException)
                                {
                                    Log.Error("ZipDataCacheProvider.Fetch(): Corrupt zip file/entry: " + filename + "#" + entryName + " Error: " + exception);
                                }
                                else throw;
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            lock (_zipFileSynchronizer)
                            {
                                stream = CreateStream(existingEntry.ZipFile, entryName);
                            }
                        }
                        catch (Exception exception)
                        {
                            if (exception is ZipException || exception is ZlibException)
                            {
                                Log.Error("ZipDataCacheProvider.Fetch(): Corrupt zip file/entry: " + filename + "#" + entryName + " Error: " + exception);
                            }
                            else throw;
                        }
                    }

                    return stream;
                }
                catch (Exception err)
                {
                    Log.Error(err, "Inner try/catch");
                    if (stream != null) stream.Dispose();
                    return null;
                }
            }
            else
            {
                // handles text files
                return _dataProvider.Fetch(filename);
            }
        }

        /// <summary>
        /// Store the data in the cache. Not implemented in this instance of the IDataCacheProvider
        /// </summary>
        /// <param name="key">The source of the data, used as a key to retrieve data in the cache</param>
        /// <param name="data">The data as a byte array</param>
        public void Store(string key, byte[] data)
        {
            //
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            lock (_zipFileSynchronizer)
            {
                foreach (var zip in _zipFileCache)
                {
                    zip.Value.ZipFile.Dispose();
                }
            }

            _zipFileCache.Clear();
        }

        /// <summary>
        /// Remove items in the cache that are older than the cutoff date
        /// </summary>
        private void CleanCache()
        {
            var clearCacheIfOlderThan = DateTime.Now.AddSeconds(-CacheSeconds);

            // clean all items that that are older than CacheSeconds than the current date
            foreach (var zip in _zipFileCache)
            {
                if (zip.Value.Uncache(clearCacheIfOlderThan))
                {
                    // removing it from the cache
                    CachedZipFile removed;
                    if (_zipFileCache.TryRemove(zip.Key, out removed))
                    {
                        // disposing zip archive
                        removed.Dispose();
                    }
                }
            }

            _lastCacheScan = DateTime.Now;
        }

        /// <summary>
        /// Create a stream of a specific ZipEntry
        /// </summary>
        /// <param name="zipFile">The zipFile containing the zipEntry</param>
        /// <param name="entryName">The name of the entry</param>
        /// <returns>A <see cref="Stream"/> of the appropriate zip entry</returns>
        private Stream CreateStream(ZipFile zipFile, string entryName)
        {
            var entry = zipFile.Entries.FirstOrDefault(x => entryName == null || string.Compare(x.FileName, entryName, StringComparison.OrdinalIgnoreCase) == 0);
            if (entry != null)
            {
                var stream = new MemoryStream();
                entry.OpenReader().CopyTo(stream);
                stream.Position = 0;
                return stream;
            }

            return null;
        }
    }


    /// <summary>
    /// Type for storing zipfile in cache
    /// </summary>
    public class CachedZipFile : IDisposable
    {
        private string _key;
        private DateTime _dateCached;
        private ZipFile _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedZipFile"/> 
        /// </summary>
        /// <param name="data">ZipFile to be store</param>
        /// <param name="key">Key that represents the path to the data</param>
        public CachedZipFile(ZipFile data, string key)
        {
            _data = data;
            _key = key;
            _dateCached = DateTime.Now;
        }

        /// <summary>
        /// Method used to check if this object was created before a certain time
        /// </summary>
        /// <param name="date">DateTime which is compared to the DateTime this object was created</param>
        /// <returns>Bool indicating whether this object is older than the specified time</returns>
        public bool Uncache(DateTime date)
        {
            return _dateCached < date;
        }

        /// <summary>
        /// The ZipFile this object represents
        /// </summary>
        public ZipFile ZipFile
        {
            get { return _data; }
        }

        /// <summary>
        /// Path to the ZipFile
        /// </summary>
        public string Key
        {
            get { return _key; }
        }

        /// <summary>
        /// Dispose of the ZipFile
        /// </summary>
        public void Dispose()
        {
            if (_data != null)
            {
                _data.Dispose();
            }

            _key = null;
        }
    }
}