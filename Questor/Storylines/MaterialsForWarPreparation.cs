using System.Collections.Generic;
using System.Globalization;
using Questor.Modules.Caching;
using Questor.Modules.Logging;
using Questor.Modules.Lookup;
using Questor.Modules.States;

namespace Questor.Storylines
{
    using System;
    using System.Linq;
    using DirectEve;

    public class MaterialsForWarPreparation : IStoryline
    {
        //private bool OreLoaded = false;
        private DateTime _nextAction;

        /// <summary>
        /// Arm does nothing but get into a (assembled) shuttle
        /// </summary>
        /// <returns></returns>
        public StorylineState Arm(Storyline storyline)
        {
            if (_nextAction > DateTime.UtcNow)
            {
                return StorylineState.Arm;
            }

            if (Cache.Instance.DirectEve.ActiveShip == null)
            {
                if (Settings.Instance.DebugArm) Logging.Log("StorylineState.Arm", "if (Cache.Instance.DirectEve.ActiveShip == null)", Logging.Debug);
                _nextAction = DateTime.UtcNow.AddSeconds(3);
                return StorylineState.Arm;
            }

            if (Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() != Settings.Instance.TransportShipName.ToLower())
            {
                // Open the ship hangar
                if (!Cache.Instance.OpenShipsHangar("MaterialsForWarPreparation")) return StorylineState.Arm;

                List<DirectItem> ships = Cache.Instance.ShipHangar.Items;
                foreach (DirectItem ship in ships.Where(ship => ship.GivenName != null && ship.GivenName.ToLower() == Settings.Instance.TransportShipName.ToLower()))
                {
                    Logging.Log("MaterialsForWarPreparation", "Making [" + ship.GivenName + "] active", Logging.White);
                    ship.ActivateShip();
                    Cache.Instance.NextArmAction = DateTime.UtcNow.AddSeconds(Modules.Lookup.Time.Instance.SwitchShipsDelay_seconds);
                    return StorylineState.Arm;
                }

                if (Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() != Settings.Instance.TransportShipName.ToLower())
                {
                    Logging.Log("StorylineState.Arm", "Missing TransportShip named [" + Settings.Instance.TransportShipName + "]", Logging.Debug);
                    return StorylineState.GotoAgent;
                }
            }

            if (!Cache.Instance.OpenItemsHangar("StorylineState.Arm")) return StorylineState.Arm;

            IEnumerable<DirectItem> items = Cache.Instance.ItemHangar.Items.Where(k => k.TypeId == Settings.Instance.MaterialsForWarOreID).ToList();
            if (!items.Any())
            {
                if (Settings.Instance.DebugArm) Logging.Log("StorylineState.Arm", "Ore for MaterialsForWar: typeID [" + Settings.Instance.MaterialsForWarOreID + "] not found in ItemHangar", Logging.Debug);
                if (!Cache.Instance.ReadyAmmoHangar("StorylineState.Arm")) return StorylineState.Arm;
                items = Cache.Instance.AmmoHangar.Items.Where(k => k.TypeId == Settings.Instance.MaterialsForWarOreID).ToList();
                if (!items.Any())
                {
                    if (Settings.Instance.DebugArm) Logging.Log("StorylineState.Arm", "Ore for MaterialsForWar: typeID [" + Settings.Instance.MaterialsForWarOreID + "] not found in AmmoHangar", Logging.Debug);
                    //
                    // if we do not have the ore... either we can blacklist it right here, or continue normally
                    //
                    return StorylineState.GotoAgent;
                    //return StorylineState.BlacklistAgent;
                }
            }

            Cache.Instance.CargoHold = Cache.Instance.DirectEve.GetShipsCargo();

            int oreIncargo = 0;
            foreach (DirectItem cargoItem in Cache.Instance.CargoHold.Items.ToList())
            {
                if (cargoItem.TypeId != Settings.Instance.MaterialsForWarOreID)
                    continue;

                oreIncargo += cargoItem.Quantity;
                continue;
            }

            int oreToLoad = Settings.Instance.MaterialsForWarOreQty - oreIncargo;
            if (oreToLoad <= 0)
            {
                //OreLoaded = true;
                return StorylineState.GotoAgent;
            }

            if (!Cache.Instance.ReadyAmmoHangar("StorylineState.Arm")) return StorylineState.Arm;

            DirectItem item = items.FirstOrDefault();
            if (item != null)
            {
                int moveOreQuantity = Math.Min(item.Stacksize, oreToLoad);
                Cache.Instance.CargoHold.Add(item, moveOreQuantity);
                Logging.Log("StorylineState.Arm", "Moving [" + moveOreQuantity + "] units of Ore [" + item.TypeName + "] Stacksize: [" + item.Stacksize + "] from hangar to CargoHold", Logging.White);
                _nextAction = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(3,6));
                return StorylineState.Arm;  // you can only move one set of items per frame
            }

            Logging.Log("StorylineState.Arm", "defined TransportShip found, going in active ship", Logging.White);
            return StorylineState.GotoAgent;
        }

        /// <summary>
        /// Check if we have kernite in station
        /// </summary>
        /// <returns></returns>
        public StorylineState PreAcceptMission(Storyline storyline)
        {
            DirectEve directEve = Cache.Instance.DirectEve;
            if (_nextAction > DateTime.UtcNow)
                return StorylineState.PreAcceptMission;

            // the ore and ore quantity can be stored in the characters settings xml this is to facility mission levels other than 4.
            //The defaults are for level 4 so it will not break for those people that do not include these in their settings file
            //  Level 1         <MaterialsForWarOreID>1230</MaterialsForWarOreID>
            //                  <MaterialsForWarOreQty>999</MaterialsForWarOreQty>
            //  Level 4         <MaterialsForWarOreID>20</MaterialsForWarOreID>
            //                  <MaterialsForWarOreQty>8000</MaterialsForWarOreQty>

            int oreid = Settings.Instance.MaterialsForWarOreID; //1230;
            int orequantity = Settings.Instance.MaterialsForWarOreQty; //999

            // Open the item hangar
            if (!Cache.Instance.OpenItemsHangar("MaterialsForWarPreparation")) return StorylineState.PreAcceptMission;

            //if (Cache.Instance.ItemHangar.Window == null)
            //{
            //    Logging.Log("MaterialsForWar", "PreAcceptMission: ItemHangar is null", Logging.Orange);
            //    if (!Cache.Instance.ReadyItemsHangar("MaterialsForWarPreparation")) return StorylineState.PreAcceptMission;
            //    return StorylineState.PreAcceptMission;
            //}

            // Is there a market window?
            DirectMarketWindow marketWindow = directEve.Windows.OfType<DirectMarketWindow>().FirstOrDefault();

            // Do we have the ore we need in the Item Hangar?.

            if (Cache.Instance.ItemHangar.Items.Where(i => i.TypeId == oreid).Sum(i => i.Quantity) >= orequantity)
            {
                DirectItem thisOreInhangar = Cache.Instance.ItemHangar.Items.FirstOrDefault(i => i.TypeId == oreid);
                if (thisOreInhangar != null)
                {
                    Logging.Log("MaterialsForWarPreparation", "We have [" + Cache.Instance.ItemHangar.Items.Where(i => i.TypeId == oreid).Sum(i => i.Quantity).ToString(CultureInfo.InvariantCulture) + "] " + thisOreInhangar.TypeName + " in the item hangar accepting mission", Logging.White);
                }

                // Close the market window if there is one
                if (marketWindow != null)
                {
                    marketWindow.Close();
                }

                return StorylineState.AcceptMission;
            }

            if (!Cache.Instance.OpenCargoHold("MaterialsForWarPreparation")) return StorylineState.PreAcceptMission;

            if (Cache.Instance.CargoHold.Items.Where(i => i.TypeId == oreid).Sum(i => i.Quantity) >= orequantity)
            {
                DirectItem thisOreInhangar = Cache.Instance.CargoHold.Items.FirstOrDefault(i => i.TypeId == oreid);
                if (thisOreInhangar != null)
                {
                    Logging.Log("MaterialsForWarPreparation", "We have [" + Cache.Instance.CargoHold.Items.Where(i => i.TypeId == oreid).Sum(i => i.Quantity).ToString(CultureInfo.InvariantCulture) + "] " + thisOreInhangar.TypeName + " in the CargoHold accepting mission", Logging.White);
                }

                // Close the market window if there is one
                if (marketWindow != null)
                {
                    marketWindow.Close();
                }

                return StorylineState.AcceptMission;
            }

            if (Cache.Instance.DirectEve.HasSupportInstances())
            {
                // We do not have enough ore, open the market window
                if (marketWindow == null)
                {
                    _nextAction = DateTime.UtcNow.AddSeconds(10);

                    Logging.Log("MaterialsForWarPreparation", "Opening market window", Logging.White);

                    directEve.ExecuteCommand(DirectCmd.OpenMarket);
                    return StorylineState.PreAcceptMission;
                }

                // Wait for the window to become ready (this includes loading the ore info)
                if (!marketWindow.IsReady)
                {
                    return StorylineState.PreAcceptMission;
                }

                // Are we currently viewing ore orders?
                if (marketWindow.DetailTypeId != oreid)
                {
                    // No, load the ore orders
                    marketWindow.LoadTypeId(oreid);

                    Logging.Log("MaterialsForWarPreparation", "Loading market window", Logging.White);

                    _nextAction = DateTime.UtcNow.AddSeconds(5);
                    return StorylineState.PreAcceptMission;
                }

                // Get the median sell price
                InvType type = Cache.Instance.InvTypesById[20];
                double? maxPrice = type.MedianSell * 4;

                // Do we have orders that sell enough ore for the mission?
                IEnumerable<DirectOrder> orders = marketWindow.SellOrders.Where(o => o.StationId == directEve.Session.StationId && o.Price < maxPrice).ToList();
                if (!orders.Any() || orders.Sum(o => o.VolumeRemaining) < orequantity)
                {
                    Logging.Log("MaterialsForWarPreparation", "Not enough (reasonably priced) ore available! Blacklisting agent for this Questor session!", Logging.Orange);

                    // Close the market window
                    marketWindow.Close();

                    // No, black list the agent in this Questor session (note we will never decline storylines!)
                    return StorylineState.BlacklistAgent;
                }

                // How much ore do we still need?
                int neededQuantity = orequantity - Cache.Instance.ItemHangar.Items.Where(i => i.TypeId == oreid).Sum(i => i.Quantity);
                if (neededQuantity > 0)
                {
                    // Get the first order
                    DirectOrder order = orders.OrderBy(o => o.Price).FirstOrDefault();
                    if (order != null)
                    {
                        // Calculate how much ore we still need
                        int remaining = Math.Min(neededQuantity, order.VolumeRemaining);
                        order.Buy(remaining, DirectOrderRange.Station);

                        Logging.Log("MaterialsForWarPreparation", "Buying [" + remaining + "] ore", Logging.White);

                        // Wait for the order to go through
                        _nextAction = DateTime.UtcNow.AddSeconds(10);
                    }
                }
                return StorylineState.PreAcceptMission;
            }

            Logging.Log("MaterialsForWarPreparation", "No DirectEVE Instances Available: free version detected. Buy/Sell support not available. Blacklisting agent for this Questor session!", Logging.Orange);

            // Close the market window
            if (marketWindow != null) marketWindow.Close();
            // No, black list the agent in this Questor session (note we will never decline storylines!)
            return StorylineState.BlacklistAgent;
        }

        /// <summary>
        /// We have no combat/delivery part in this mission, just accept it
        /// </summary>
        /// <returns></returns>
        public StorylineState PostAcceptMission(Storyline storyline)
        {
            // Close the market window (if its open)
            return StorylineState.CompleteMission;
        }

        /// <summary>
        /// We have no execute mission code
        /// </summary>
        /// <returns></returns>
        public StorylineState ExecuteMission(Storyline storyline)
        {
            return StorylineState.CompleteMission;
        }
    }
}