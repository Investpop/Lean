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

namespace QuantConnect.VisualStudioPlugin
{
    /// <summary>
    /// Singleton that stores a reference to an authenticated Api instance
    /// </summary>
    public class AuthorizationManager
    {
        private static readonly Log _log = new Log(typeof(AuthorizationManager));

        private static AuthorizationManager _authorizationManager = new AuthorizationManager();
        private Api.Api _api;

        /// <summary>
        /// Get singleton authorization manager instance accessable from multiple commands
        /// </summary>
        /// <returns>singleton authorization instance</returns>
        public static AuthorizationManager GetInstance()
        {
            return _authorizationManager;
        }

        /// <summary>
        /// Get an authenticated API instance. 
        /// </summary>
        /// <returns>Authenticated API instance</returns>
        /// <exception cref="NotAuthenticatedException">It API is not authenticated</exception>
        public Api.Api GetApi()
        {
            if (_api == null)
            {
                throw new InvalidOperationException("Accessing API without logging in first");
            }
            return _api;
        }

        /// <summary>
        /// Check if the API is authenticated
        /// </summary>
        /// <returns>true if API is authenticated, false otherwise</returns>
        public bool IsLoggedIn()
        {
            return _api != null;
        }

        /// <summary>
        /// Authenticate API 
        /// </summary>
        /// <param name="userId">User id to authenticate the API</param>
        /// <param name="accessToken">Access token to authenticate the API</param>
        /// <returns>true if successfully authenticated API, false otherwise</returns>
        public bool LogIn(Credentials credentials, string dataFolderPath)
        {
            _log.Info($"Authenticating QuantConnect API with data folder {dataFolderPath}");
            try
            {
                var api = new Api.Api();
                api.Initialize(int.Parse(credentials.UserId), credentials.AccessToken, dataFolderPath);
                if (api.Connected)
                {
                    _api = api;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (FormatException)
            {
                // User id is not a number
                return false;
            }

        }

        /// <summary>
        /// Log out the API
        /// </summary>
        public void LogOut()
        {
            _api = null;
        }

    }
}
