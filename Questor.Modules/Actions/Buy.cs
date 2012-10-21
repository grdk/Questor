﻿
namespace Questor.Modules.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.States;
    using global::Questor.Modules.Logging;

    public class Buy
    {
        public int Item { get; set; }

        public int Unit { get; set; }

        private DateTime _lastAction;

        private bool _returnBuy;

        public void ProcessState()
        {
            if (!Cache.Instance.InStation)
                return;

            if (Cache.Instance.InSpace)
                return;

            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20)) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return;

            DirectMarketWindow marketWindow = Cache.Instance.DirectEve.Windows.OfType<DirectMarketWindow>().FirstOrDefault();

            switch (_States.CurrentBuyState)
            {
                case BuyState.Idle:
                case BuyState.Done:
                    break;

                case BuyState.Begin:

                    // Close the market window if there is one
                    if (marketWindow != null)
                        marketWindow.Close();
                    _States.CurrentBuyState = BuyState.OpenMarket;
                    break;

                case BuyState.OpenMarket:
                    // Close the market window if there is one
                    //if (marketWindow != null)
                    //    marketWindow.Close();

                    if (marketWindow == null)
                    {
                        Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenMarket);
                        break;
                    }

                    if (!marketWindow.IsReady)
                        break;

                    Logging.Log("Buy", "Opening Market", Logging.White);
                    _States.CurrentBuyState = BuyState.LoadItem;

                    break;

                case BuyState.LoadItem:

                    _lastAction = DateTime.Now;

                    if (marketWindow != null && marketWindow.DetailTypeId != Item)
                    {
                        marketWindow.LoadTypeId(Item);
                        _States.CurrentBuyState = BuyState.BuyItem;

                        break;
                    }

                    break;

                case BuyState.BuyItem:

                    if (DateTime.Now.Subtract(_lastAction).TotalSeconds < 5)
                        break;

                    if (marketWindow != null)
                    {
                        IEnumerable<DirectOrder> orders = marketWindow.SellOrders.Where(o => o.StationId == Cache.Instance.DirectEve.Session.StationId);

                        DirectOrder order = orders.OrderBy(o => o.Price).FirstOrDefault();
                        if (order != null)
                        {
                            // Calculate how much we still need
                            if (order.VolumeEntered >= Unit)
                            {
                                order.Buy(Unit, DirectOrderRange.Station);
                                _States.CurrentBuyState = BuyState.WaitForItems;
                            }
                            else
                            {
                                order.Buy(Unit, DirectOrderRange.Station);
                                Unit = Unit - order.VolumeEntered;
                                Logging.Log("Buy", "Missing " + Convert.ToString(Unit) + " units", Logging.White);
                                _returnBuy = true;
                                _States.CurrentBuyState = BuyState.WaitForItems;
                            }
                        }
                    }

                    break;

                case BuyState.WaitForItems:
                    // Wait 5 seconds after moving
                    if (DateTime.Now.Subtract(_lastAction).TotalSeconds < 5)
                        break;

                    // Close the market window if there is one
                    if (marketWindow != null)
                        marketWindow.Close();

                    if (_returnBuy)
                    {
                        Logging.Log("Buy", "Return Buy", Logging.White);
                        _returnBuy = false;
                        _States.CurrentBuyState = BuyState.OpenMarket;
                        break;
                    }

                    Logging.Log("Buy", "Done", Logging.White);
                    _States.CurrentBuyState = BuyState.Done;

                    break;
            }
        }
    }
}