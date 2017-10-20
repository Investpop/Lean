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
using System.Collections.Generic;
using Ionic.Zip;
using QuantConnect.Data;
using QuantConnect.Util;

namespace QuantConnect.ToolBox
{
    /// <summary>
    /// This class reads data directly from disk and returns the data without the data
    /// entering the Lean data enumeration stack
    /// </summary>
    public class LeanDataReader
    {
        private readonly DateTime _date;
        private readonly string _zipPath;
        private readonly string _zipentry;
        private readonly SubscriptionDataConfig _config;
        
        /// <summary>
        /// The LeanDataReader constructor
        /// </summary>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/></param>
        /// <param name="symbol">The <see cref="Symbol"/> that will be read</param>
        /// <param name="resolution">The <see cref="Resolution"/> that will be read</param>
        /// <param name="date">The <see cref="DateTime"/> that will be read</param>
        /// <param name="dataFolder">The root data folder</param>
        public LeanDataReader(SubscriptionDataConfig config, Symbol symbol, Resolution resolution, DateTime date, string dataFolder)
        {
            _date = date;
            _zipPath = LeanData.GenerateZipFilePath(dataFolder, symbol, date,  resolution, config.TickType);
            _zipentry = LeanData.GenerateZipEntryName(symbol, date, resolution, config.TickType);
            _config = config;
        }

        /// <summary>
        /// Enumerate over the tick zip file and return a list of BaseData.
        /// </summary>
        /// <returns>IEnumerable of ticks</returns>
        public IEnumerable<BaseData> Parse()
        {
            var factory = (BaseData) ObjectActivator.GetActivator(_config.Type).Invoke(new object[0]);
            ZipFile zipFile;
            using (var unzipped = Compression.Unzip(_zipPath,_zipentry, out zipFile))
            {
                if (unzipped == null)
                    yield break;
                string line;
                while ((line = unzipped.ReadLine()) != null)
                {
                    yield return factory.Reader(_config, line, _date, false);
                }
            }
            zipFile.Dispose();
        }
    }
}
