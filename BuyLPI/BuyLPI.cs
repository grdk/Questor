﻿// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace BuyLPI
{
    using System;
    using System.Linq;
    using System.Threading;
    using DirectEve;
    using System.Globalization;
    using Questor.Modules.Logging;
    using Questor.Modules.Caching;
    using Questor.Modules.BackgroundTasks;

    internal class BuyLPI
    {
        private const int WaitMillis = 3500;
        private static long _lastLoyaltyPoints;
        private static DateTime _nextAction;
        private static DateTime _loyaltyPointTimeout;
        private static string _type;
        private static int? _quantity;
        private static int? _totalQuantityOfOrders;
        private static DateTime _done = DateTime.Now.AddYears(10);
        private static DirectEve _directEve;
        private static DateTime _lastPulse;
        private static Cleanup _cleanup;

        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Logging.Log("BuyLPI", "Syntax:", Logging.White);
                Logging.Log("BuyLPI", "DotNet BuyLPI BuyLPI <TypeName or TypeId> [Quantity]", Logging.White);
                Logging.Log("BuyLPI", "(Quantity is optional)", Logging.White);
                Logging.Log("BuyLPI", "", Logging.White);
                Logging.Log("BuyLPI", "Example:", Logging.White);
                Logging.Log("BuyLPI", "DotNet BuyLPI BuyLPI \"Caldari Navy Mjolnir Torpedo\" 10", Logging.White);
                Logging.Log("BuyLPI", "*OR*", Logging.White);
                Logging.Log("BuyLPI", "DotNet BuyLPI BuyLPI 27339 10", Logging.White);
                return;
            }

            if (args.Length >= 1)
            {
                _type = args[0];
            }

            if (args.Length >= 2)
            {
                int dummy;
                if (!int.TryParse(args[1], out dummy))
                {
                    Logging.Log("BuyLPI", "Quantity must be an integer, 0 - " + int.MaxValue, Logging.White);
                    return;
                }

                if (dummy < 0)
                {
                    Logging.Log("BuyLPI", "Quantity must be a positive number", Logging.White);
                    return;
                }

                _quantity = dummy;
                _totalQuantityOfOrders = dummy;
            }

            Logging.Log("BuyLPI", "Starting BuyLPI...", Logging.White);
            _cleanup = new Cleanup();
            _directEve = new DirectEve();
            Cache.Instance.DirectEve = _directEve;
            _directEve.OnFrame += OnFrame;

            // Sleep until we're done
            while (_done.AddSeconds(5) > DateTime.Now)
                Thread.Sleep(50);

            _directEve.Dispose();
            Logging.Log("BuyLPI", "BuyLPI finished.", Logging.White);
        }

        private static void OnFrame(object sender, EventArgs eventArgs)
        {
            // New frame, invalidate old cache
            Cache.Instance.InvalidateCache();

            Cache.Instance.LastFrame = DateTime.Now;

            // Only pulse state changes every 1.5s
            if (DateTime.Now.Subtract(_lastPulse).TotalMilliseconds < 300)
                return;
            _lastPulse = DateTime.Now;

            // Session is not ready yet, do not continue
            if (!Cache.Instance.DirectEve.Session.IsReady)
                return;

            if (Cache.Instance.DirectEve.Session.IsReady)
                Cache.Instance.LastSessionIsReady = DateTime.Now;

            // We are not in space or station, don't do shit yet!
            if (!Cache.Instance.InSpace && !Cache.Instance.InStation)
            {
                Cache.Instance.NextInSpaceorInStation = DateTime.Now.AddSeconds(12);
                Cache.Instance.LastSessionChange = DateTime.Now;
                return;
            }

            if (DateTime.Now < Cache.Instance.NextInSpaceorInStation)
                return;

            // Start _cleanup.ProcessState
            // Description: Closes Windows, and eventually other things considered 'cleanup' useful to more than just Questor(Missions) but also Anomalies, Mining, etc
            //
            _cleanup.ProcessState();

            // Done
            // Cleanup State: ProcessState

            if (DateTime.Now > _done)
                return;

            // Wait for the next action
            if (_nextAction >= DateTime.Now)
            {
                return;
            }

            if (!Cache.Instance.OpenItemsHangarAsLootHangar("BuyLPI")) return;

            DirectLoyaltyPointStoreWindow lpstore = _directEve.Windows.OfType<DirectLoyaltyPointStoreWindow>().FirstOrDefault();
            if (lpstore == null)
            {
                _nextAction = DateTime.Now.AddMilliseconds(WaitMillis);
                _directEve.ExecuteCommand(DirectCmd.OpenLpstore);

                Logging.Log("BuyLPI", "Opening loyalty point store", Logging.White);
                return;
            }

            // Wait for the amount of LP to change
            if (_lastLoyaltyPoints == lpstore.LoyaltyPoints)
                return;

            // Do not expect it to be 0 (probably means its reloading)
            if (lpstore.LoyaltyPoints == 0)
            {
                if (_loyaltyPointTimeout < DateTime.Now)
                {
                    Logging.Log("BuyLPI", "It seems we have no loyalty points left", Logging.White);
                    _done = DateTime.Now;
                    return;
                }
                return;
            }

            _lastLoyaltyPoints = lpstore.LoyaltyPoints;

            // Find the offer
            DirectLoyaltyPointOffer offer = lpstore.Offers.FirstOrDefault(o => o.TypeId.ToString(CultureInfo.InvariantCulture) == _type || String.Compare(o.TypeName, _type, StringComparison.OrdinalIgnoreCase) == 0);
            if (offer == null)
            {
                Logging.Log("BuyLPI", " Can't find offer with type name/id: [" + _type + "]", Logging.White);
                _done = DateTime.Now;
                return;
            }

            // Check LP
            if (_lastLoyaltyPoints < offer.LoyaltyPointCost)
            {
                Logging.Log("BuyLPI", "Not enough loyalty points left: you have [" + _lastLoyaltyPoints + "] and you need [" + offer.LoyaltyPointCost + "]", Logging.White);
                _done = DateTime.Now;
                return;
            }

            // Check ISK
            if (_directEve.Me.Wealth < offer.IskCost)
            {
                Logging.Log("BuyLPI", "Not enough ISK left: you have [" + Math.Round(_directEve.Me.Wealth, 0) + "] and you need  [" + offer.IskCost + "]", Logging.White);
                _done = DateTime.Now;
                return;
            }

            // Check items
            foreach (DirectLoyaltyPointOfferRequiredItem requiredItem in offer.RequiredItems)
            {
                DirectItem item = Cache.Instance.LootHangar.Items.FirstOrDefault(i => i.TypeId == requiredItem.TypeId);
                if (item == null || item.Quantity < requiredItem.Quantity)
                {
                    Logging.Log("BuyLPI", "Missing [" + requiredItem.Quantity + "] x [" +
                                                    requiredItem.TypeName + "]", Logging.White);
                    _done = DateTime.Now;
                    return;
                }
            }

            // All passed, accept offer
            if (_quantity != null)
                if (_totalQuantityOfOrders != null)
                    Logging.Log("BuyLPI", "Accepting " + offer.TypeName + " [ " + _quantity.Value + " ] of [ " + _totalQuantityOfOrders.Value + " ] orders and will cost another [" + Math.Round(((offer.IskCost * _quantity.Value) / (double)1000000), 2) + "mil isk]", Logging.White);
            offer.AcceptOfferFromWindow();

            // Set next action + loyalty point timeout
            _nextAction = DateTime.Now.AddMilliseconds(WaitMillis);
            _loyaltyPointTimeout = DateTime.Now.AddSeconds(25);

            if (_quantity.HasValue)
            {
                _quantity = _quantity.Value - 1;
                if (_quantity.Value <= 0)
                {
                    Logging.Log("BuyLPI", "Quantity limit reached", Logging.White);
                    _done = DateTime.Now;
                    return;
                }
            }
        }
    }
}