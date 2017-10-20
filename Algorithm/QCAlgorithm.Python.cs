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

using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System;
using QuantConnect.Securities;
using NodaTime;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using QuantConnect.Python;
using Python.Runtime;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Data.Fundamental;
using System.Linq;
using QuantConnect.Util;

namespace QuantConnect.Algorithm
{
    public partial class QCAlgorithm
    {
        private PandasConverter _converter;

        /// <summary>
        /// Sets pandas converter
        /// </summary>
        public void SetPandas()
        {
            _converter = new PandasConverter();
        }

        /// <summary>
        /// AddData a new user defined data source, requiring only the minimum config options.
        /// The data is added with a default time zone of NewYork (Eastern Daylight Savings Time)
        /// </summary>
        /// <param name="type">Data source type</param>
        /// <param name="symbol">Key/Symbol for data</param>
        /// <param name="resolution">Resolution of the data</param>
        /// <remarks>Generic type T must implement base data</remarks>
        public void AddData(PyObject type, string symbol, Resolution resolution = Resolution.Minute)
        {
            AddData(type, symbol, resolution, TimeZones.NewYork, false, 1m);
        }


        /// <summary>
        /// AddData a new user defined data source, requiring only the minimum config options.
        /// </summary>
        /// <param name="type">Data source type</param>
        /// <param name="symbol">Key/Symbol for data</param>
        /// <param name="resolution">Resolution of the Data Required</param>
        /// <param name="timeZone">Specifies the time zone of the raw data</param>
        /// <param name="fillDataForward">When no data available on a tradebar, return the last data that was generated</param>
        /// <param name="leverage">Custom leverage per security</param>
        public void AddData(PyObject type, string symbol, Resolution resolution, DateTimeZone timeZone, bool fillDataForward = false, decimal leverage = 1.0m)
        {
            AddData(CreateType(type), symbol, resolution, timeZone, fillDataForward, leverage);
        }

        /// <summary>
        /// AddData a new user defined data source, requiring only the minimum config options.
        /// </summary>
        /// <param name="dataType">Data source type</param>
        /// <param name="symbol">Key/Symbol for data</param>
        /// <param name="resolution">Resolution of the Data Required</param>
        /// <param name="timeZone">Specifies the time zone of the raw data</param>
        /// <param name="fillDataForward">When no data available on a tradebar, return the last data that was generated</param>
        /// <param name="leverage">Custom leverage per security</param>
        public void AddData(Type dataType, string symbol, Resolution resolution, DateTimeZone timeZone, bool fillDataForward = false, decimal leverage = 1.0m)
        {
            var marketHoursDbEntry = _marketHoursDatabase.GetEntry(Market.USA, symbol, SecurityType.Base, timeZone);

            //Add this to the data-feed subscriptions
            var symbolObject = new Symbol(SecurityIdentifier.GenerateBase(symbol, Market.USA), symbol);
            var symbolProperties = _symbolPropertiesDatabase.GetSymbolProperties(Market.USA, symbol, SecurityType.Base, CashBook.AccountCurrency);

            //Add this new generic data as a tradeable security: 
            var security = SecurityManager.CreateSecurity(dataType, Portfolio, SubscriptionManager, marketHoursDbEntry.ExchangeHours, marketHoursDbEntry.DataTimeZone,
                symbolProperties, SecurityInitializer, symbolObject, resolution, fillDataForward, leverage, true, false, true, LiveMode);

            AddToUserDefinedUniverse(security);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This is for coarse fundamental US Equity data and
        /// will be executed on day changes in the NewYork time zone (<see cref="TimeZones.NewYork"/>
        /// </summary>
        /// <param name="pycoarse">Defines an initial coarse selection</param>
        public void AddUniverse(PyObject pycoarse)
        {
            var coarse = PythonUtil.ToFunc<IEnumerable<CoarseFundamental>, object[]>(pycoarse);
            AddUniverse(c => coarse(c).Select(x => (Symbol)x));
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This is for coarse and fine fundamental US Equity data and
        /// will be executed on day changes in the NewYork time zone (<see cref="TimeZones.NewYork"/>
        /// </summary>
        /// <param name="pycoarse">Defines an initial coarse selection</param>
        /// <param name="pyfine">Defines a more detailed selection with access to more data</param>
        public void AddUniverse(PyObject pycoarse, PyObject pyfine)
        {
            var coarse = PythonUtil.ToFunc<IEnumerable<CoarseFundamental>, object[]>(pycoarse);
            var fine = PythonUtil.ToFunc<IEnumerable<FineFundamental>, object[]>(pyfine);
            AddUniverse(c => coarse(c).Select(x => (Symbol)x), f => fine(f).Select(x => (Symbol)x));
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This can be used to return a list of string
        /// symbols retrieved from anywhere and will loads those symbols under the US Equity market.
        /// </summary>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The resolution this universe should be triggered on</param>
        /// <param name="pySelector">Function delegate that accepts a DateTime and returns a collection of string symbols</param>
        public void AddUniverse(string name, Resolution resolution, PyObject pySelector)
        {
            var selector = PythonUtil.ToFunc<DateTime, object[]>(pySelector);
            AddUniverse(name, resolution, d => selector(d).Select(x => (string)x));
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This can be used to return a list of string
        /// symbols retrieved from anywhere and will loads those symbols under the US Equity market.
        /// </summary>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="pySelector">Function delegate that accepts a DateTime and returns a collection of string symbols</param>
        public void AddUniverse(string name, PyObject pySelector)
        {
            var selector = PythonUtil.ToFunc<DateTime, object[]>(pySelector);
            AddUniverse(name, d => selector(d).Select(x => (string)x));
        }

        /// <summary>
        /// Creates a new user defined universe that will fire on the requested resolution during market hours.
        /// </summary>
        /// <param name="securityType">The security type of the universe</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The resolution this universe should be triggered on</param>
        /// <param name="market">The market of the universe</param>
        /// <param name="universeSettings">The subscription settings used for securities added from this universe</param>
        /// <param name="pySelector">Function delegate that accepts a DateTime and returns a collection of string symbols</param>
        public void AddUniverse(SecurityType securityType, string name, Resolution resolution, string market, UniverseSettings universeSettings, PyObject pySelector)
        {
            var selector = PythonUtil.ToFunc<DateTime, object[]>(pySelector);
            AddUniverse(securityType, name, resolution, market, universeSettings, d => selector(d).Select(x => (string)x));
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Resolution.Daily, Market.USA, and UniverseSettings
        /// </summary>
        /// <param name="T">The data type</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse(PyObject T, string name, PyObject selector)
        {
            AddUniverse(CreateType(T), SecurityType.Equity, name, Resolution.Daily, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Market.USA and UniverseSettings
        /// </summary>
        /// <param name="T">The data type</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse(PyObject T, string name, Resolution resolution, PyObject selector)
        {
            AddUniverse(CreateType(T), SecurityType.Equity, name, resolution, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, and Market.USA
        /// </summary>
        /// <param name="T">The data type</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="universeSettings">The settings used for securities added by this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse(PyObject T, string name, Resolution resolution, UniverseSettings universeSettings, PyObject selector)
        {
            AddUniverse(CreateType(T), SecurityType.Equity, name, resolution, Market.USA, universeSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Resolution.Daily, and Market.USA
        /// </summary>
        /// <param name="T">The data type</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="universeSettings">The settings used for securities added by this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse(PyObject T, string name, UniverseSettings universeSettings, PyObject selector)
        {
            AddUniverse(CreateType(T), SecurityType.Equity, name, Resolution.Daily, Market.USA, universeSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property.
        /// </summary>
        /// <param name="T">The data type</param>
        /// <param name="securityType">The security type the universe produces</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="market">The market for selected symbols</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse(PyObject T, SecurityType securityType, string name, Resolution resolution, string market, PyObject selector)
        {
            AddUniverse(CreateType(T), securityType, name, resolution, market, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm
        /// </summary>
        /// <param name="T">The data type</param>
        /// <param name="securityType">The security type the universe produces</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="market">The market for selected symbols</param>
        /// <param name="universeSettings">The subscription settings to use for newly created subscriptions</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse(PyObject T, SecurityType securityType, string name, Resolution resolution, string market, UniverseSettings universeSettings, PyObject selector)
        {
            AddUniverse(CreateType(T), securityType, name, resolution, market, universeSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm
        /// </summary>
        /// <param name="dataType">The data type</param>
        /// <param name="securityType">The security type the universe produces</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="market">The market for selected symbols</param>
        /// <param name="universeSettings">The subscription settings to use for newly created subscriptions</param>
        /// <param name="pySelector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse(Type dataType, SecurityType securityType, string name, Resolution resolution, string market, UniverseSettings universeSettings, PyObject pySelector)
        {
            var marketHoursDbEntry = _marketHoursDatabase.GetEntry(market, name, securityType);
            var dataTimeZone = marketHoursDbEntry.DataTimeZone;
            var exchangeTimeZone = marketHoursDbEntry.ExchangeHours.TimeZone;
            var symbol = QuantConnect.Symbol.Create(name, securityType, market);
            var config = new SubscriptionDataConfig(dataType, symbol, resolution, dataTimeZone, exchangeTimeZone, false, false, true, true, isFilteredSubscription: false);

            var selector = PythonUtil.ToFunc<IEnumerable<IBaseData>, object[]>(pySelector);

            AddUniverse(new FuncUniverse(config, universeSettings, SecurityInitializer, d => selector(d)
                .Select(x => x is Symbol ? (Symbol)x : QuantConnect.Symbol.Create((string)x, securityType, market))));
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="resolution">The resolution at which to send data to the indicator, null to use the same resolution as the subscription</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<IBaseDataBar> indicator, Resolution? resolution = null)
        {
            RegisterIndicator<IBaseDataBar>(symbol, indicator, resolution);
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="resolution">The resolution at which to send data to the indicator, null to use the same resolution as the subscription</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<TradeBar> indicator, Resolution? resolution = null)
        {
            RegisterIndicator<TradeBar>(symbol, indicator, resolution);
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="resolution">The resolution at which to send data to the indicator, null to use the same resolution as the subscription</param>
        /// <param name="selector">Selects a value from the BaseData send into the indicator, if null defaults to a cast (x => (T)x)</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<IBaseDataBar> indicator, Resolution? resolution, Func<IBaseData, IBaseDataBar> selector)
        {
            RegisterIndicator<IBaseDataBar>(symbol, indicator, resolution, selector);
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="resolution">The resolution at which to send data to the indicator, null to use the same resolution as the subscription</param>
        /// <param name="selector">Selects a value from the BaseData send into the indicator, if null defaults to a cast (x => (T)x)</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<TradeBar> indicator, Resolution? resolution, Func<IBaseData, TradeBar> selector)
        {
            RegisterIndicator<TradeBar>(symbol, indicator, resolution, selector);
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="resolution">The resolution at which to send data to the indicator, null to use the same resolution as the subscription</param>
        /// <param name="selector">Selects a value from the BaseData send into the indicator, if null defaults to a cast (x => (T)x)</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<IBaseDataBar> indicator, TimeSpan? resolution, Func<IBaseData, IBaseDataBar> selector)
        {
            RegisterIndicator<IBaseDataBar>(symbol, indicator, resolution, selector);
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="resolution">The resolution at which to send data to the indicator, null to use the same resolution as the subscription</param>
        /// <param name="selector">Selects a value from the BaseData send into the indicator, if null defaults to a cast (x => (T)x)</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<TradeBar> indicator, TimeSpan? resolution, Func<IBaseData, TradeBar> selector)
        {
            RegisterIndicator<TradeBar>(symbol, indicator, resolution, selector);
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="consolidator">The consolidator to receive raw subscription data</param>
        /// <param name="selector">Selects a value from the BaseData send into the indicator, if null defaults to a cast (x => (T)x)</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<IBaseDataBar> indicator, IDataConsolidator consolidator, Func<IBaseData, IBaseDataBar> selector)
        {
            RegisterIndicator<IBaseDataBar>(symbol, indicator, consolidator, selector);
        }

        /// <summary>
        /// Registers the consolidator to receive automatic updates as well as configures the indicator to receive updates
        /// from the consolidator.
        /// </summary>
        /// <param name="symbol">The symbol to register against</param>
        /// <param name="indicator">The indicator to receive data from the consolidator</param>
        /// <param name="consolidator">The consolidator to receive raw subscription data</param>
        /// <param name="selector">Selects a value from the BaseData send into the indicator, if null defaults to a cast (x => (T)x)</param>
        public void RegisterIndicator(Symbol symbol, IndicatorBase<TradeBar> indicator, IDataConsolidator consolidator, Func<IBaseData, TradeBar> selector)
        {
            RegisterIndicator<TradeBar>(symbol, indicator, consolidator, selector);
        }

        /// <summary>
        /// Plots the value of each indicator on the chart
        /// </summary>
        /// <param name="chart">The chart's name</param>
        /// <param name="first">The first indicator to plot</param>
        /// <param name="second">The second indicator to plot</param>
        /// <param name="third">The third indicator to plot</param>
        /// <param name="fourth">The fourth indicator to plot</param>
        /// <seealso cref="Plot(string,string,decimal)"/>
        public void Plot(string chart, Indicator first, Indicator second = null, Indicator third = null, Indicator fourth = null)
        {
            Plot(chart, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Plots the value of each indicator on the chart
        /// </summary>
        /// <param name="chart">The chart's name</param>
        /// <param name="first">The first indicator to plot</param>
        /// <param name="second">The second indicator to plot</param>
        /// <param name="third">The third indicator to plot</param>
        /// <param name="fourth">The fourth indicator to plot</param>
        /// <seealso cref="Plot(string,string,decimal)"/>
        public void Plot(string chart, BarIndicator first, BarIndicator second = null, BarIndicator third = null, BarIndicator fourth = null)
        {
            Plot(chart, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Plots the value of each indicator on the chart
        /// </summary>
        /// <param name="chart">The chart's name</param>
        /// <param name="first">The first indicator to plot</param>
        /// <param name="second">The second indicator to plot</param>
        /// <param name="third">The third indicator to plot</param>
        /// <param name="fourth">The fourth indicator to plot</param>
        /// <seealso cref="Plot(string,string,decimal)"/>
        public void Plot(string chart, TradeBarIndicator first, TradeBarIndicator second = null, TradeBarIndicator third = null, TradeBarIndicator fourth = null)
        {
            Plot(chart, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Automatically plots each indicator when a new value is available
        /// </summary>
        public void PlotIndicator(string chart, Indicator first, Indicator second = null, Indicator third = null, Indicator fourth = null)
        {
            PlotIndicator(chart, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Automatically plots each indicator when a new value is available
        /// </summary>
        public void PlotIndicator(string chart, BarIndicator first, BarIndicator second = null, BarIndicator third = null, BarIndicator fourth = null)
        {
            PlotIndicator(chart, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Automatically plots each indicator when a new value is available
        /// </summary>
        public void PlotIndicator(string chart, TradeBarIndicator first, TradeBarIndicator second = null, TradeBarIndicator third = null, TradeBarIndicator fourth = null)
        {
            PlotIndicator(chart, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Automatically plots each indicator when a new value is available, optionally waiting for indicator.IsReady to return true
        /// </summary>
        public void PlotIndicator(string chart, bool waitForReady, Indicator first, Indicator second = null, Indicator third = null, Indicator fourth = null)
        {
            PlotIndicator(chart, waitForReady, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Automatically plots each indicator when a new value is available, optionally waiting for indicator.IsReady to return true
        /// </summary>
        public void PlotIndicator(string chart, bool waitForReady, BarIndicator first, BarIndicator second = null, BarIndicator third = null, BarIndicator fourth = null)
        {
            PlotIndicator(chart, waitForReady, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Automatically plots each indicator when a new value is available, optionally waiting for indicator.IsReady to return true
        /// </summary>
        public void PlotIndicator(string chart, bool waitForReady, TradeBarIndicator first, TradeBarIndicator second = null, TradeBarIndicator third = null, TradeBarIndicator fourth = null)
        {
            PlotIndicator(chart, waitForReady, new[] { first, second, third, fourth }.Where(x => x != null).ToArray());
        }

        /// <summary>
        /// Creates a new FilteredIdentity indicator for the symbol The indicator will be automatically
        /// updated on the symbol's subscription resolution
        /// </summary>
        /// <param name="symbol">The symbol whose values we want as an indicator</param>
        /// <param name="selector">Selects a value from the BaseData, if null defaults to the .Value property (x => x.Value)</param>
        /// <param name="filter">Filters the IBaseData send into the indicator, if null defaults to true (x => true) which means no filter</param>
        /// <param name="fieldName">The name of the field being selected</param>
        /// <returns>A new FilteredIdentity indicator for the specified symbol and selector</returns>
        public FilteredIdentity FilteredIdentity(Symbol symbol, PyObject selector = null, PyObject filter = null, string fieldName = null)
        {
            var resolution = GetSubscription(symbol).Resolution;
            return FilteredIdentity(symbol, resolution, selector, filter, fieldName);
        }

        /// <summary>
        /// Creates a new FilteredIdentity indicator for the symbol The indicator will be automatically
        /// updated on the symbol's subscription resolution
        /// </summary>
        /// <param name="symbol">The symbol whose values we want as an indicator</param>
        /// <param name="resolution">The desired resolution of the data</param>
        /// <param name="selector">Selects a value from the BaseData, if null defaults to the .Value property (x => x.Value)</param>
        /// <param name="filter">Filters the IBaseData send into the indicator, if null defaults to true (x => true) which means no filter</param>
        /// <param name="fieldName">The name of the field being selected</param>
        /// <returns>A new FilteredIdentity indicator for the specified symbol and selector</returns>
        public FilteredIdentity FilteredIdentity(Symbol symbol, Resolution resolution, PyObject selector = null, PyObject filter = null, string fieldName = null)
        {
            var name = CreateIndicatorName(symbol, fieldName ?? "close", resolution);
            var pyselector = PythonUtil.ToFunc<IBaseData, IBaseDataBar>(selector);
            var pyfilter = PythonUtil.ToFunc<IBaseData, bool>(filter);
            var filteredIdentity = new FilteredIdentity(name, pyfilter);
            RegisterIndicator(symbol, filteredIdentity, resolution, pyselector);
            return filteredIdentity;
        }

        /// <summary>
        /// Creates a new FilteredIdentity indicator for the symbol The indicator will be automatically
        /// updated on the symbol's subscription resolution
        /// </summary>
        /// <param name="symbol">The symbol whose values we want as an indicator</param>
        /// <param name="resolution">The desired resolution of the data</param>
        /// <param name="selector">Selects a value from the BaseData, if null defaults to the .Value property (x => x.Value)</param>
        /// <param name="filter">Filters the IBaseData send into the indicator, if null defaults to true (x => true) which means no filter</param>
        /// <param name="fieldName">The name of the field being selected</param>
        /// <returns>A new FilteredIdentity indicator for the specified symbol and selector</returns>
        public FilteredIdentity FilteredIdentity(Symbol symbol, TimeSpan resolution, PyObject selector = null, PyObject filter = null, string fieldName = null)
        {
            var name = string.Format("{0}({1}_{2})", symbol, fieldName ?? "close", resolution);
            var pyselector = PythonUtil.ToFunc<IBaseData, IBaseDataBar>(selector);
            var pyfilter = PythonUtil.ToFunc<IBaseData, bool>(filter);
            var filteredIdentity = new FilteredIdentity(name, pyfilter);
            RegisterIndicator(symbol, filteredIdentity, ResolveConsolidator(symbol, resolution), pyselector);
            return filteredIdentity;
        }

        /// <summary>
        /// Gets the historical data for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="tickers">The symbols to retrieve historical data for</param>
        /// <param name="periods">The number of bars to request</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>A python dictionary with pandas DataFrame containing the requested historical data</returns>
        public PyObject History(PyObject tickers, int periods, Resolution? resolution = null)
        {
            var symbols = GetSymbolsFromPyObject(tickers);
            if (symbols == null) return null;

            return _converter.GetDataFrame(History(symbols, periods, resolution));
        }

        /// <summary>
        /// Gets the historical data for the specified symbols over the requested span.
        /// The symbols must exist in the Securities collection.
        /// </summary>
        /// <param name="tickers">The symbols to retrieve historical data for</param>
        /// <param name="span">The span over which to retrieve recent historical data</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>A python dictionary with pandas DataFrame containing the requested historical data</returns>
        public PyObject History(PyObject tickers, TimeSpan span, Resolution? resolution = null)
        {
            var symbols = GetSymbolsFromPyObject(tickers);
            if (symbols == null) return null;

            return _converter.GetDataFrame(History(symbols, span, resolution));
        }

        /// <summary>
        /// Gets the historical data for the specified symbol between the specified dates. The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="tickers">The symbols to retrieve historical data for</param>
        /// <param name="start">The start time in the algorithm's time zone</param>
        /// <param name="end">The end time in the algorithm's time zone</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>A python dictionary with pandas DataFrame containing the requested historical data</returns>
        public PyObject History(PyObject tickers, DateTime start, DateTime end, Resolution? resolution = null)
        {
            var symbols = GetSymbolsFromPyObject(tickers);
            if (symbols == null) return null;

            return _converter.GetDataFrame(History(symbols, start, end, resolution));
        }

        /// <summary>
        /// Sets the specified function as the benchmark, this function provides the value of
        /// the benchmark at each date/time requested
        /// </summary>
        /// <param name="benchmark">The benchmark producing function</param>
        public void SetBenchmark(PyObject benchmark)
        {
            using (Py.GIL())
            {
                var pyBenchmark = PythonUtil.ToFunc<DateTime, decimal>(benchmark);
                if (pyBenchmark != null)
                {
                    SetBenchmark(pyBenchmark);
                    return;
                }
                SetBenchmark((Symbol)benchmark.AsManagedObject(typeof(Symbol)));
            }
        }

        /// <summary>
        /// Sets the brokerage to emulate in backtesting or paper trading.
        /// This can be used to set a custom brokerage model.
        /// </summary>
        /// <param name="model">The brokerage model to use</param>
        public void SetBrokerageModel(PyObject model)
        {
            SetBrokerageModel(new BrokerageModelPythonWrapper(model));
        }

        /// <summary>
        /// Sets the security initializer function, used to initialize/configure securities after creation
        /// </summary>
        /// <param name="securityInitializer">The security initializer function or class</param>
        public void SetSecurityInitializer(PyObject securityInitializer)
        {
            var securityInitializer1 = PythonUtil.ToAction<Security>(securityInitializer);
            if (securityInitializer1 != null)
            {
                SetSecurityInitializer(securityInitializer1);
                return;
            }

            var securityInitializer2 = PythonUtil.ToAction<Security, bool>(securityInitializer);
            if (securityInitializer2 != null)
            {
                SetSecurityInitializer(securityInitializer2);
                return;
            }

            SetSecurityInitializer(new SecurityInitializerPythonWrapper(securityInitializer));
        }

        /// <summary>
        /// Gets the symbols/string from a PyObject
        /// </summary>
        /// <param name="pyObject">PyObject containing symbols</param>
        /// <returns>List of symbols</returns>
        public List<Symbol> GetSymbolsFromPyObject(PyObject pyObject)
        {
            using (Py.GIL())
            {
                // If not a PyList, convert it into one
                if (!PyList.IsListType(pyObject))
                {
                    var tmp = new PyList();
                    tmp.Append(pyObject);
                    pyObject = tmp;
                }

                var symbols = new List<Symbol>();
                foreach (PyObject item in pyObject)
                {
                    var symbol = (Symbol)item.AsManagedObject(typeof(Symbol));

                    if (string.IsNullOrWhiteSpace(symbol.Value))
                    {
                        continue;
                    }

                    symbols.Add(symbol);
                }
                return symbols.Count == 0 ? null : symbols;
            }
        }

        /// <summary>
        /// Creates a type with a given name
        /// </summary>
        /// <param name="type">Python object</param>
        /// <returns>Type object</returns>
        private Type CreateType(PyObject type)
        {
            using (Py.GIL())
            {
                var an = new AssemblyName(type.Repr().Split('.')[1].Replace("\'>", ""));
                var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                return moduleBuilder.DefineType(an.Name,
                        TypeAttributes.Public |
                        TypeAttributes.Class |
                        TypeAttributes.AutoClass |
                        TypeAttributes.AnsiClass |
                        TypeAttributes.BeforeFieldInit |
                        TypeAttributes.AutoLayout,
                        // If the type has IsAuthCodeSet member, it is a PythonQuandl
                        type.HasAttr("IsAuthCodeSet") ? typeof(PythonQuandl) : typeof(PythonData))
                    .CreateType();
            }
        }
    }
}