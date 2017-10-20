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
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Interfaces;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Slippage;
using QuantConnect.Configuration;
using System.Linq;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages
{

    /// <summary>
    /// Provides GDAX specific properties
    /// </summary>
    public class GDAXBrokerageModel : DefaultBrokerageModel
    {

        private static BrokerageMessageEvent _message = new BrokerageMessageEvent(BrokerageMessageType.Warning, 0, "Brokerage does not support update. You must cancel and re-create instead.");

        /// <summary>
        /// Initializes a new instance of the <see cref="GDAXBrokerageModel"/> class
        /// </summary>
        /// <param name="accountType">The type of account to be modelled, defaults to 
        /// <see cref="QuantConnect.AccountType.Margin"/></param>
        public GDAXBrokerageModel(AccountType accountType = AccountType.Margin)
            : base(accountType)
        {
            if (accountType == AccountType.Margin)
            {
                new BrokerageMessageEvent(BrokerageMessageType.Warning, 0, 
                    "It is recommend to use a cash account. Margin trading is currently in pre-Alpha. Use at your own risk and please report any issues encountered.");
            }
        }

        /// <summary>
        /// GDAX global leverage rule
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override decimal GetLeverage(Security security)
        {
            return 3m;
        }

        /// <summary>
        /// Provides GDAX fee model
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override IFeeModel GetFeeModel(Security security)
        {
            return new GDAXFeeModel();
        }

        /// <summary>
        /// Gdax does no support update of orders
        /// </summary>
        /// <param name="security"></param>
        /// <param name="order"></param>
        /// <param name="request"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public override bool CanUpdateOrder(Security security, Order order, UpdateOrderRequest request, out BrokerageMessageEvent message)
        {
            message = _message;
            return false;
        }

        /// <summary>
        /// Evaluates whether exchange will accept order. Will reject order update
        /// </summary>
        /// <param name="security"></param>
        /// <param name="order"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public override bool CanSubmitOrder(Security security, Order order, out BrokerageMessageEvent message)
        {           
            if (order.BrokerId != null && order.BrokerId.Any())
            {
                message = _message;
                return false;
            }

            return base.CanSubmitOrder(security, order, out message);
        }

    }
}