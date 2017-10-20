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

namespace QuantConnect.VisualStudioPlugin
{
    /// <summary>
    /// Credentials to use QuantConnect API. Consists of a user id and an access token
    /// </summary>
    public struct Credentials
    {
        private string _userId;
        private string _accessToken;

        /// <summary>
        /// Create Credentials
        /// </summary>
        /// <param name="userId">User id</param>
        /// <param name="accessToken">Access token</param>
        public Credentials(string userId, string accessToken)
        {
            _userId = userId;
            _accessToken = accessToken;
        }

        /// <summary>
        /// User id for QuantConnect API
        /// </summary>
        public string UserId
        {
            get
            {
                return _userId;
            }
        }

        /// <summary>
        /// Access token for QuantConnect API
        /// </summary>
        public string AccessToken
        {
            get
            {
                return _accessToken;
            }
        }

    }
}
