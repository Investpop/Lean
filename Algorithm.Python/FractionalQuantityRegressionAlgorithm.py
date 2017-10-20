﻿# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
# 
# Licensed under the Apache License, Version 2.0 (the "License"); 
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from clr import AddReference
AddReference("System")
AddReference("NodaTime")
AddReference("QuantConnect.Algorithm")
AddReference("QuantConnect.Indicators")
AddReference("QuantConnect.Common")

from System import *
from NodaTime import DateTimeZone
from QuantConnect import *
from QuantConnect.Algorithm import *
from QuantConnect.Data.Market import *
from QuantConnect.Data.Consolidators import *

import decimal as d
from datetime import timedelta
from math import floor

### <summary>
### Regression algorithm for fractional forex pair
### </summary>
### <meta name="tag" content="using data" />
### <meta name="tag" content="trading and orders" />
### <meta name="tag" content="regression test" />
class FractionalQuantityRegressionAlgorithm(QCAlgorithm):
    
    def Initialize(self):
        
        self.SetStartDate(2015, 11, 12)
        self.SetEndDate(2016, 04, 01)
        self.SetCash(100000)
        
        self.SetTimeZone(DateTimeZone.Utc)
        
        security = self.AddSecurity(SecurityType.Crypto, "BTCUSD", Resolution.Daily, Market.GDAX, False, 3.3, True)
        con = QuoteBarConsolidator(timedelta(1))
        self.SubscriptionManager.AddConsolidator("BTCUSD", con)
        con.DataConsolidated += self.DataConsolidated
        self.SetBenchmark(security.Symbol)

    def DataConsolidated(self, sender, bar):
        quantity = floor(self.Portfolio.Cash / abs(bar.Value + 1))
        btc_qnty = float(self.Portfolio["BTCUSD"].Quantity)

        if not self.Portfolio.Invested:
            self.Order("BTCUSD", quantity)
        elif btc_qnty == quantity:
            self.Order("BTCUSD", 0.1)
        elif btc_qnty == quantity + 0.1:
            self.Order("BTCUSD", 0.01)
        elif btc_qnty == quantity + 0.11:
            self.Order("BTCUSD", -0.02)
        elif btc_qnty == quantity + 0.09:
            # should fail
            self.Order("BTCUSD", 0.001)
            self.SetHoldings("BTCUSD", -2.0)
            self.SetHoldings("BTCUSD", 2.0)
            self.Quit()