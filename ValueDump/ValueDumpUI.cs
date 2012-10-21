//------------------------------------------------------------------------------
//  <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//    Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//    Please look in the accompanying license.htm file for the license that
//    applies to this source code. (a copy can also be found at:
//    http://www.thehackerwithin.com/license.htm)
//  </copyright>
//-------------------------------------------------------------------------------

#define manual

namespace ValueDump
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;
    using System.Xml.Linq;
    using DirectEve;
    using Valuedump;
    using Questor.Modules.Caching;
    using Questor.Modules.Lookup;
    using Questor.Modules.Logging;
    using Questor.Modules.States;

    public partial class ValueDumpUI : Form
    {
        private DateTime _lastPulse;

        private Dictionary<int, InvTypeMarket> InvTypesById { get; set; }

        private List<ItemCacheMarket> Items { get; set; }

        private List<ItemCacheMarket> ItemsToSell { get; set; }

        private List<ItemCacheMarket> ItemsToRefine { get; set; }

        private DirectEve _directEve { get; set; }

        public string CharacterName { get; set; }

        public string InvTypesXMLData
        {
            get
            {
                return Settings.Instance.Path + "\\InvTypes.xml";
            }
        }

        public ValueDumpUI()
        {
            Logging.Log("ValueDump","Starting ValueDump",Logging.Orange);
            InitializeComponent();

            InvTypesById = new Dictionary<int, InvTypeMarket>();
            Logging.Log("Valuedump", "Load InvTypes.xml from [" + InvTypesXMLData + "]", Logging.White);
            try
            {
                XDocument invTypes = XDocument.Load(InvTypesXMLData);
                if (invTypes.Root != null)
                    foreach (XElement element in invTypes.Root.Elements("invtype"))
                        InvTypesById.Add((int)element.Attribute("id"), new InvTypeMarket(element));
            }
            catch (Exception ex)
            {
                Logging.Log("ValueDump","Unable to load [" + InvTypesXMLData + "] exception was [" + ex.Message + "]",Logging.Orange);
            }

            Items = new List<ItemCacheMarket>();
            ItemsToSell = new List<ItemCacheMarket>();
            ItemsToRefine = new List<ItemCacheMarket>();

            _directEve = new DirectEve();
            Cache.Instance.DirectEve = _directEve;
            _directEve.OnFrame += OnFrame;
        }

        private InvTypeMarket _currentMineral;
        private ItemCacheMarket _currentItem;
        private DateTime _lastExecute = DateTime.MinValue;

        private void OnFrame(object sender, EventArgs e)
        {
            Cache.Instance.LastFrame = DateTime.Now;
            // Only pulse state changes every .5s
            if (DateTime.Now.Subtract(_lastPulse).TotalMilliseconds < Time.Instance.ValueDumpPulse_milliseconds) //default: 500ms
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

            // New frame, invalidate old cache
            Cache.Instance.InvalidateCache();

            // Update settings (settings only load if character name changed)
            if (!Settings.Instance.DefaultSettingsLoaded)
            {
                Settings.Instance.LoadSettings();
            }

            if (DateTime.Now.Subtract(Cache.Instance.LastUpdateOfSessionRunningTime).TotalSeconds <
                Time.Instance.SessionRunningTimeUpdate_seconds)
            {
                Cache.Instance.SessionRunningTime =
                    (int)DateTime.Now.Subtract(Cache.Instance.QuestorStarted_DateTime).TotalMinutes;
                Cache.Instance.LastUpdateOfSessionRunningTime = DateTime.Now;
            }

            if ( _States.CurrentValueDumpState == ValueDumpState.Idle)
                return;

            XDocument invIgnore = XDocument.Load(Settings.Instance.Path + "\\InvIgnore.xml"); //items to ignore
            DirectMarketWindow marketWindow = Cache.Instance.DirectEve.Windows.OfType<DirectMarketWindow>().FirstOrDefault();
            DirectMarketActionWindow sellWindow = Cache.Instance.DirectEve.Windows.OfType<DirectMarketActionWindow>().FirstOrDefault(w => w.IsSellAction);
            DirectReprocessingWindow reprocessingWindow = Cache.Instance.DirectEve.Windows.OfType<DirectReprocessingWindow>().FirstOrDefault();
            bool doNotSellTheseItems = false;

            switch (_States.CurrentValueDumpState)
            {
                case ValueDumpState.CheckMineralPrices:
                    if (RefineCheckBox.Checked)
                        _currentMineral = InvTypesById.Values.FirstOrDefault(i => i.ReprocessValue.HasValue && i.LastUpdate < DateTime.Now.AddDays(-7));
                    else
                        _currentMineral = InvTypesById.Values.FirstOrDefault(i => i.Id != 27029 && i.GroupId == 18 && i.LastUpdate < DateTime.Now.AddHours(-4));
                    //_currentMineral = InvTypesById.Values.FirstOrDefault(i => i.Id != 27029 && i.GroupId == 18 && i.LastUpdate < DateTime.Now.AddMinutes(-1));
                    //_currentMineral = InvTypesById.Values.FirstOrDefault(i => i.Id == 20236 && i.LastUpdate < DateTime.Now.AddMinutes(-1));
                    if (_currentMineral == null)
                    {
                        if (DateTime.Now.Subtract(_lastExecute).TotalSeconds > Time.Instance.Marketlookupdelay_seconds)
                        {
                             _States.CurrentValueDumpState = ValueDumpState.SaveMineralPrices;
                            if (marketWindow != null)
                                marketWindow.Close();
                        }
                    }
                    else
                    {
                        //State = ValueDumpState.GetMineralPrice;
                        if (marketWindow == null)
                        {
                            if (DateTime.Now.Subtract(_lastExecute).TotalSeconds > Time.Instance.Marketlookupdelay_seconds)
                            {
                                Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenMarket);
                                _lastExecute = DateTime.Now;
                            }
                            return;
                        }

                        if (!marketWindow.IsReady)
                            return;

                        if (marketWindow.DetailTypeId != _currentMineral.Id)
                        {
                            if (DateTime.Now.Subtract(_lastExecute).TotalSeconds < Time.Instance.Marketlookupdelay_seconds)
                                return;

                            Logging.Log("Valuedump", "Loading orders for " + _currentMineral.Name, Logging.White);

                            marketWindow.LoadTypeId(_currentMineral.Id);
                            _lastExecute = DateTime.Now;
                            return;
                        }

                        if (marketWindow.BuyOrders.All(o => o.StationId != Cache.Instance.DirectEve.Session.StationId))
                        {
                            _currentMineral.LastUpdate = DateTime.Now;

                            Logging.Log("Valuedump", "No buy orders found for " + _currentMineral.Name, Logging.White);
                             _States.CurrentValueDumpState = ValueDumpState.CheckMineralPrices;
                        }

                        // Take top 5 orders, average the buy price and consider that median-buy (it's not really median buy but its what we want)
                        //_currentMineral.MedianBuy = marketWindow.BuyOrders.Where(o => o.StationId == DirectEve.Session.StationId).OrderByDescending(o => o.Price).Take(5).Average(o => o.Price);

                        // Take top 1% orders and count median-buy price (no botter covers more than 1% Jita orders anyway)
                        List<DirectOrder> orders = marketWindow.BuyOrders.Where(o => o.StationId == Cache.Instance.DirectEve.Session.StationId && o.MinimumVolume == 1).OrderByDescending(o => o.Price).ToList();
                        double totalAmount = orders.Sum(o => (double)o.VolumeRemaining);
                        double amount = 0, value = 0, count = 0;
                        for (int i = 0; i < orders.Count(); i++)
                        {
                            amount += orders[i].VolumeRemaining;
                            value += orders[i].VolumeRemaining * orders[i].Price;
                            count++;
                            //Logging.Log(_currentMineral.Name + " " + count + ": " + orders[i].VolumeRemaining.ToString("#,##0") + " items @ " + orders[i].Price);
                            if (amount / totalAmount > 0.01)
                                break;
                        }
                        _currentMineral.MedianBuy = value / amount;
                        Logging.Log("Valuedump", "Average buy price for " + _currentMineral.Name + " is " + _currentMineral.MedianBuy.Value.ToString("#,##0.00") + " (" + count + " / " + orders.Count() + " orders, " + amount.ToString("#,##0") + " / " + totalAmount.ToString("#,##0") + " items)", Logging.White);

                        if (marketWindow.SellOrders.All(o => o.StationId != Cache.Instance.DirectEve.Session.StationId))
                        {
                            _currentMineral.LastUpdate = DateTime.Now;

                            Logging.Log("Valuedump", "No sell orders found for " + _currentMineral.Name, Logging.White);
                             _States.CurrentValueDumpState = ValueDumpState.CheckMineralPrices;
                        }

                        // Take top 1% orders and count median-sell price
                        orders = marketWindow.SellOrders.Where(o => o.StationId == Cache.Instance.DirectEve.Session.StationId).OrderBy(o => o.Price).ToList();
                        totalAmount = orders.Sum(o => (double)o.VolumeRemaining);
                        amount = 0; value = 0; count = 0;
                        for (int i = 0; i < orders.Count(); i++)
                        {
                            amount += orders[i].VolumeRemaining;
                            value += orders[i].VolumeRemaining * orders[i].Price;
                            count++;
                            //Logging.Log(_currentMineral.Name + " " + count + ": " + orders[i].VolumeRemaining.ToString("#,##0") + " items @ " + orders[i].Price);
                            if (amount / totalAmount > 0.01)
                                break;
                        }
                        _currentMineral.MedianSell = value / amount - 0.01;
                        Logging.Log("Valuedump", "Average sell price for " + _currentMineral.Name + " is " + _currentMineral.MedianSell.Value.ToString("#,##0.00") + " (" + count + " / " + orders.Count() + " orders, " + amount.ToString("#,##0") + " / " + totalAmount.ToString("#,##0") + " items)", Logging.White);

                        if (_currentMineral.MedianSell.HasValue && !double.IsNaN(_currentMineral.MedianSell.Value))
                            _currentMineral.MedianAll = _currentMineral.MedianSell;
                        else if (_currentMineral.MedianBuy.HasValue && !double.IsNaN(_currentMineral.MedianBuy.Value))
                            _currentMineral.MedianAll = _currentMineral.MedianBuy;
                        _currentMineral.LastUpdate = DateTime.Now;
                        //State = ValueDumpState.CheckMineralPrices;
                    }
                    break;

                case ValueDumpState.GetMineralPrice:
                    break;

                case ValueDumpState.SaveMineralPrices:
                    Logging.Log("Valuedump", "Updating reprocess prices", Logging.White);

                    // a quick price check table
                    Dictionary<string, double> mineralPrices = new Dictionary<string, double>();
                    foreach (InvTypeMarket i in InvTypesById.Values)
                        if (InvTypeMarket.Minerals.Contains(i.Name))
#if manual
                            mineralPrices.Add(i.Name, i.MedianSell ?? 0);
#else
                            MineralPrices.Add(i.Name, i.MedianBuy ?? 0);
#endif

                    foreach (InvTypeMarket i in InvTypesById.Values)
                    {
                        double temp = 0;
                        foreach (string m in InvTypeMarket.Minerals)
                            if (i.Reprocess[m].HasValue && i.Reprocess[m] > 0)
                            {
                                var d = i.Reprocess[m];
                                if (d != null) temp += d.Value * mineralPrices[m];
                            }
                        if (temp > 0)
                            i.ReprocessValue = temp;
                        else
                            i.ReprocessValue = null;
                    }

                    Logging.Log("Valuedump", "Saving InvTypes.xml", Logging.White);

                    XDocument xdoc = new XDocument(new XElement("invtypes"));
                    foreach (InvTypeMarket type in InvTypesById.Values.OrderBy(i => i.Id))
                        if (xdoc.Root != null) xdoc.Root.Add(type.Save());
                    xdoc.Save(InvTypesXMLData);

                     _States.CurrentValueDumpState = ValueDumpState.Idle;
                    break;

                case ValueDumpState.GetItems:
                    if (!Cache.Instance.OpenItemsHangar("ValueDump")) break;
                    Logging.Log("ValueDump", "Loading hangar items", Logging.White);

                    // Clear out the old
                    Items.Clear();
                    List<DirectItem> hangarItems = Cache.Instance.ItemHangar.Items;
                    if (hangarItems != null)
                        Items.AddRange(hangarItems.Where(i => i.ItemId > 0 && i.Quantity > 0).Select(i => new ItemCacheMarket(i, RefineCheckBox.Checked)));

                     _States.CurrentValueDumpState = ValueDumpState.UpdatePrices;
                    break;

                case ValueDumpState.UpdatePrices:
                    bool updated = false;

                    foreach (ItemCacheMarket item in Items)
                    {
                        InvTypeMarket invType;
                        if (!InvTypesById.TryGetValue(item.TypeId, out invType))
                        {
                            Logging.Log("Valuedump", "Unknown TypeId " + item.TypeId + " for " + item.Name + ", adding to the list", Logging.Orange);
                            invType = new InvTypeMarket(item);
                            InvTypesById.Add(item.TypeId, invType);
                            updated = true;
                            continue;
                        }
                        item.InvType = invType;

                        bool updItem = false;
                        foreach (ItemCacheMarket material in item.RefineOutput)
                        {
                            try
                            {
                                if (!InvTypesById.TryGetValue(material.TypeId, out invType))
                                {
                                    Logging.Log("Valuedump", "Unknown TypeId [" + material.TypeId + "] for [" + material.Name + "]", Logging.White);
                                    continue;
                                }
                                material.InvType = invType;

                                double matsPerItem = (double)material.Quantity / item.PortionSize;
                                bool exists = InvTypesById[item.TypeId].Reprocess[material.Name].HasValue;
                                if ((!exists && matsPerItem > 0) || (exists && InvTypesById[item.TypeId].Reprocess[material.Name] != matsPerItem))
                                {
                                    if (exists)
                                        Logging.Log("ValueDump",
                                            Logging.Orange + " [" + Logging.White +
                                            item.Name +
                                            Logging.Orange + "][" + Logging.White +
                                            material.Name +
                                            Logging.Orange + "] old value: [" + Logging.White +
                                            InvTypesById[item.TypeId].Reprocess[material.Name] + ", new value: " +
                                            Logging.Orange + "[" + Logging.White + matsPerItem +
                                            Logging.Orange + "]", Logging.White);
                                    InvTypesById[item.TypeId].Reprocess[material.Name] = matsPerItem;
                                    updItem = true;
                                }
                            }
                            catch(Exception ex)
                            {
                                Logging.Log("ValueDump","Unknown TypeId [" + material.TypeId + "] for [" + material.Name + "] Exception was: " + ex,Logging.Orange);
                                continue;
                            }
                            
                        }

                        if (updItem)
                            Logging.Log("ValueDump", "Updated [" + item.Name + "] refine materials", Logging.White);
                        updated |= updItem;
                    }

                    if (updated)
                         _States.CurrentValueDumpState = ValueDumpState.SaveMineralPrices;
                    else
                         _States.CurrentValueDumpState = ValueDumpState.Idle;

                    if (cbxSell.Checked || RefineCheckBox.Checked)
                    {
                        // Copy the items to sell list
                        ItemsToSell.Clear();
                        ItemsToRefine.Clear();
                        if (cbxUndersell.Checked)
#if manual
                            ItemsToSell.AddRange(Items.Where(i => i.InvType != null && i.MarketGroupId > 0));
#else
                            ItemsToSell.AddRange(Items.Where(i => i.InvType != null && i.MarketGroupId > 0));
#endif
                        else
#if manual
                            ItemsToSell.AddRange(Items.Where(i => i.InvType != null && i.MarketGroupId > 0 && i.InvType.MedianBuy.HasValue));
#else
                            ItemsToSell.AddRange(Items.Where(i => i.InvType != null && i.MarketGroupId > 0 && i.InvType.MedianBuy.HasValue));
#endif
                         _States.CurrentValueDumpState = ValueDumpState.NextItem;
                    }
                    break;

                case ValueDumpState.NextItem:
                    if (ItemsToSell.Count == 0)
                    {
                        if (ItemsToRefine.Count != 0)
                             _States.CurrentValueDumpState = ValueDumpState.RefineItems;
                        else
                             _States.CurrentValueDumpState = ValueDumpState.Idle;
                        break;
                    }
                    //if (!_form.RefineCheckBox.Checked)
                    Logging.Log("ValueDump", ItemsToSell.Count + " items left to sell", Logging.White);

                    _currentItem = ItemsToSell[0];
                    ItemsToSell.RemoveAt(0);

                    // Do not sell containers
                    if (_currentItem.GroupId == 448 || _currentItem.GroupId == 649)
                    {
                        Logging.Log("ValueDump", "Skipping " + _currentItem.Name, Logging.White);
                        break;
                    }
                    // Do not sell items in invignore.xml
                    if (invIgnore.Root != null)
                        foreach (XElement element in invIgnore.Root.Elements("invtype"))
                        {
                            if (_currentItem.TypeId == (int)element.Attribute("id"))
                            {
                                Logging.Log("ValueDump", "Skipping (block list) " + _currentItem.Name, Logging.White);
                                doNotSellTheseItems = true;
                                break;
                            }
                        }
                    if (doNotSellTheseItems)
                        break;

                     _States.CurrentValueDumpState = ValueDumpState.StartQuickSell;
                    break;

                case ValueDumpState.StartQuickSell:
                    if (DateTime.Now.Subtract(_lastExecute).TotalSeconds < Time.Instance.Marketsellorderdelay_seconds)
                        break;
                    _lastExecute = DateTime.Now;

                    DirectItem directItem = Cache.Instance.ItemHangar.Items.FirstOrDefault(i => i.ItemId == _currentItem.Id);
                    if (directItem == null)
                    {
                        Logging.Log("ValueDump", "Item " + _currentItem.Name + " no longer exists in the hanger", Logging.White);
                        break;
                    }

                    // Update Quantity
                    _currentItem.QuantitySold = _currentItem.Quantity - directItem.Quantity;

                    if (cbxSell.Checked)
                    {
                        Logging.Log("ValueDump", "Starting QuickSell for " + _currentItem.Name, Logging.White);
                        if (!directItem.QuickSell())
                        {
                            _lastExecute = DateTime.Now.AddSeconds(-5);

                            Logging.Log("ValueDump", "QuickSell failed for " + _currentItem.Name + ", retrying in 5 seconds", Logging.White);
                            break;
                        }

                        _States.CurrentValueDumpState = ValueDumpState.WaitForSellWindow;
                    }
                    else
                    {
                        _States.CurrentValueDumpState = ValueDumpState.InspectRefinery;
                    }
                    break;

                case ValueDumpState.WaitForSellWindow:
                    if (sellWindow == null || !sellWindow.IsReady || sellWindow.Item.ItemId != _currentItem.Id)
                        break;

                    // Mark as new execution
                    _lastExecute = DateTime.Now;

                    Logging.Log("ValueDump", "Inspecting sell order for " + _currentItem.Name, Logging.White);
                     _States.CurrentValueDumpState = ValueDumpState.InspectOrder;
                    break;

                case ValueDumpState.InspectOrder:
                    // Let the order window stay open for a few seconds
                    if (DateTime.Now.Subtract(_lastExecute).TotalSeconds < Time.Instance.Marketbuyorderdelay_seconds)
                        break;

                    if (sellWindow != null && (!sellWindow.OrderId.HasValue || !sellWindow.Price.HasValue || !sellWindow.RemainingVolume.HasValue))
                    {
                        Logging.Log("ValueDump", "No order available for " + _currentItem.Name, Logging.White);

                        sellWindow.Cancel();
                         _States.CurrentValueDumpState = ValueDumpState.WaitingToFinishQuickSell;
                        break;
                    }

                    if (sellWindow != null)
                    {
                        double price = sellWindow.Price.Value;
                        int quantity =
                            (int)
                            Math.Min(_currentItem.Quantity - _currentItem.QuantitySold, sellWindow.RemainingVolume.Value);
                        double totalPrice = quantity*price;

                        string otherPrices = " ";
                        if (_currentItem.InvType.MedianBuy.HasValue)
                            otherPrices += "[Median buy price: " +
                                           (_currentItem.InvType.MedianBuy.Value*quantity).ToString("#,##0.00") + "]";
                        else
                            otherPrices += "[No median buy price]";

                        if (RefineCheckBox.Checked)
                        {
                            int portions = quantity/_currentItem.PortionSize;
                            double refinePrice = _currentItem.RefineOutput.Any()
                                                     ? _currentItem.RefineOutput.Sum(
                                                         m => m.Quantity*m.InvType.MedianBuy ?? 0)*portions
                                                     : 0;
                            refinePrice *= (double) RefineEfficiencyInput.Value/100;

                            otherPrices += "[Refine price: " + refinePrice.ToString("#,##0.00") + "]";

                            if (refinePrice > totalPrice)
                            {
                                Logging.Log("ValueDump.InspectRefinery", "[" + _currentItem.Name + "[" + quantity + "units] is worth more as mins [Refine each: " + (refinePrice / portions).ToString("#,##0.00") + "][Sell each: " + price.ToString("#,##0.00") + "][Refine total: " + refinePrice.ToString("#,##0.00") + "][Sell total: " + totalPrice.ToString("#,##0.00") + "]", Logging.White);
                                // Add it to the refine list
                                ItemsToRefine.Add(_currentItem);

                                sellWindow.Cancel();
                                _States.CurrentValueDumpState = ValueDumpState.WaitingToFinishQuickSell;
                                break;
                            }
                        }

                        if (!cbxUndersell.Checked)
                        {
                            if (!_currentItem.InvType.MedianBuy.HasValue)
                            {
                                Logging.Log("ValueDump", "No historical price available for " + _currentItem.Name,
                                            Logging.White);

                                sellWindow.Cancel();
                                _States.CurrentValueDumpState = ValueDumpState.WaitingToFinishQuickSell;
                                break;
                            }

                            double perc = price/_currentItem.InvType.MedianBuy.Value;
                            double total = _currentItem.InvType.MedianBuy.Value*_currentItem.Quantity;
                            // If percentage < 85% and total price > 1m isk then skip this item (we don't undersell)
                            if (perc < 0.85 && total > 1000000)
                            {
                                Logging.Log("ValueDump", "Not underselling item " + _currentItem.Name +
                                                           Logging.Orange + " [" + Logging.White +
                                                           "Median buy price: " +
                                                           _currentItem.InvType.MedianBuy.Value.ToString("#,##0.00") +
                                                           Logging.Orange + "][" + Logging.White +
                                                           "Sell price: " + price.ToString("#,##0.00") +
                                                           Logging.Orange + "][" + Logging.White +
                                                           perc.ToString("0%") +
                                                           Logging.Orange + "]", Logging.White);

                                sellWindow.Cancel();
                                _States.CurrentValueDumpState = ValueDumpState.WaitingToFinishQuickSell;
                                break;
                            }
                        }

                        // Update quantity sold
                        _currentItem.QuantitySold += quantity;

                        // Update station price
                        if (!_currentItem.StationBuy.HasValue)
                            _currentItem.StationBuy = price;
                        _currentItem.StationBuy = (_currentItem.StationBuy + price)/2;

                        if (cbxSell.Checked)
                        {
                            Logging.Log("ValueDump", "Selling " + quantity + " of " + _currentItem.Name +
                                                       Logging.Orange + " [" + Logging.White +
                                                       "Sell price: " + (price*quantity).ToString("#,##0.00") +
                                                       Logging.Orange + "]" + Logging.White +
                                                       otherPrices, Logging.White);
                            sellWindow.Accept();
                            // Update quantity sold
                            _currentItem.QuantitySold += quantity;
                            // Re-queue to check again
                            if (_currentItem.QuantitySold < _currentItem.Quantity)
                                ItemsToSell.Add(_currentItem);
                            _lastExecute = DateTime.Now;
                            _States.CurrentValueDumpState = ValueDumpState.WaitingToFinishQuickSell;
                            break;
                        }
                    }
                    break;

                case ValueDumpState.InspectRefinery:
                    if (_currentItem.InvType.MedianBuy != null)
                    {
                        double priceR = _currentItem.InvType.MedianBuy.Value;
                        int quantityR = _currentItem.Quantity;
                        double totalPriceR = quantityR * priceR;
                        int portions = quantityR / _currentItem.PortionSize;
                        double refinePrice = _currentItem.RefineOutput.Any() ? _currentItem.RefineOutput.Sum(m => m.Quantity * m.InvType.MedianBuy ?? 0) * portions : 0;
                        refinePrice *= (double)RefineEfficiencyInput.Value / 100;

                        if (refinePrice > totalPriceR || totalPriceR <= 1500000 || _currentItem.TypeId == 30497)
                        {
                            Logging.Log("ValueDump.InspectRefinery", "[" + _currentItem.Name + "[" + quantityR + "units] is worth more as mins [Refine each: " + (refinePrice / portions).ToString("#,##0.00") + "][Sell each: " + priceR.ToString("#,##0.00") + "][Refine total: " + refinePrice.ToString("#,##0.00") + "][Sell total: " + totalPriceR.ToString("#,##0.00") + "]", Logging.White);
                            // Add it to the refine list
                            ItemsToRefine.Add(_currentItem);
                        }
                        else
                        {
                            if (Settings.Instance.DebugValuedump)
                            {
                                Logging.Log("ValueDump.InspectRefinery","[" + _currentItem.Name + "[" + quantityR + "units] is worth more to sell [Refine each: " + (refinePrice/portions).ToString("#,##0.00") + "][Sell each: " + priceR.ToString("#,##0.00") + "][Refine total: " + refinePrice.ToString("#,##0.00") + "][Sell total: " + totalPriceR.ToString("#,##0.00") + "]", Logging.White);
                            }
                        }
                    }
                    /*else
                    {
                        Logging.Log("Selling gives a better price for item " + _currentItem.Name + " [Refine price: " + refinePrice.ToString("#,##0.00") + "][Sell price: " + totalPrice_r.ToString("#,##0.00") + "]");
                    }*/

                    _lastExecute = DateTime.Now;
                     _States.CurrentValueDumpState = ValueDumpState.NextItem;
                    break;

                case ValueDumpState.WaitingToFinishQuickSell:
                    if (sellWindow == null || !sellWindow.IsReady || sellWindow.Item.ItemId != _currentItem.Id)
                    {
                        DirectWindow modal = Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.IsModal);
                        if (modal != null)
                            modal.Close();

                         _States.CurrentValueDumpState = ValueDumpState.NextItem;
                        break;
                    }
                    break;

                case ValueDumpState.RefineItems:
                    const bool refine = true;
                    if (refine)
                    {
                        if (reprocessingWindow == null)
                        {
                            if (DateTime.Now.Subtract(_lastExecute).TotalSeconds > Time.Instance.Marketlookupdelay_seconds)
                            {
                                IEnumerable<DirectItem> refineItems = Cache.Instance.ItemHangar.Items.Where(i => ItemsToRefine.Any(r => r.Id == i.ItemId));
                                Cache.Instance.DirectEve.ReprocessStationItems(refineItems);

                                _lastExecute = DateTime.Now;
                            }
                            return;
                        }

                        if (reprocessingWindow.NeedsQuote)
                        {
                            if (DateTime.Now.Subtract(_lastExecute).TotalSeconds > Time.Instance.Marketlookupdelay_seconds)
                            {
                                reprocessingWindow.GetQuotes();
                                _lastExecute = DateTime.Now;
                            }

                            return;
                        }

                        // Wait till we have a quote
                        if (reprocessingWindow.Quotes.Count == 0)
                        {
                            _lastExecute = DateTime.Now;
                            return;
                        }

                        // Wait another 5 seconds to view the quote and then reprocess the stuff
                        if (DateTime.Now.Subtract(_lastExecute).TotalSeconds > Time.Instance.Marketlookupdelay_seconds)
                        {
                            // TODO: We should wait for the items to appear in our hangar and then sell them...
                            reprocessingWindow.Reprocess();
                            _States.CurrentValueDumpState = ValueDumpState.Idle;
                        }
                    }
                    else
                    {
                        IEnumerable<DirectItem> refineItems = Cache.Instance.ItemHangar.Items.Where(i => ItemsToRefine.Any(r => r.Id == i.ItemId));
                        if (!Cache.Instance.OpenCargoHold("ValueDump")) break;

                        if (!Cache.Instance.ReadyAmmoHangar("ValueDump")) break;
                        if (refineItems != null)
                        {
                            Logging.Log("Arm", "Moving loot to refine to CargoHold", Logging.White);

                            Cache.Instance.CargoHold.Add(refineItems);
                            _lastExecute = DateTime.Now;
                            break;
                        }
                        else
                        {
                            _States.CurrentValueDumpState = ValueDumpState.Idle;
                        }

                    }
                    break;
            }
        }

        private void BtnHangarClick(object sender, EventArgs e)
        {
             _States.CurrentValueDumpState = ValueDumpState.GetItems;
            ProcessItems();
        }

        private void ProcessItems()
        {
            // Wait for the items to load
            Logging.Log("ValueDump", "Waiting for items", Logging.White);
            while ( _States.CurrentValueDumpState != ValueDumpState.Idle)
            {
                System.Threading.Thread.Sleep(50);
                Application.DoEvents();
            }

            lvItems.Items.Clear();
            foreach (ItemCacheMarket item in Items.Where(i => i.InvType != null).OrderByDescending(i => i.InvType.MedianBuy * i.Quantity))
            {
                ListViewItem listItem = new ListViewItem(item.Name);
                listItem.SubItems.Add(string.Format("{0:#,##0}", item.Quantity));
                listItem.SubItems.Add(string.Format("{0:#,##0}", item.QuantitySold));
                listItem.SubItems.Add(string.Format("{0:#,##0}", item.InvType.MedianBuy));
                listItem.SubItems.Add(string.Format("{0:#,##0}", item.StationBuy));

                if (cbxSell.Checked)
                    listItem.SubItems.Add(string.Format("{0:#,##0}", item.StationBuy * item.QuantitySold));
                else
                    listItem.SubItems.Add(string.Format("{0:#,##0}", item.InvType.MedianBuy * item.Quantity));

                lvItems.Items.Add(listItem);
            }

            if (cbxSell.Checked)
            {
                tbTotalMedian.Text = string.Format("{0:#,##0}", Items.Where(i => i.InvType != null).Sum(i => i.InvType.MedianBuy * i.QuantitySold));
                tbTotalSold.Text = string.Format("{0:#,##0}", Items.Sum(i => i.StationBuy * i.QuantitySold));
            }
            else
            {
                tbTotalMedian.Text = string.Format("{0:#,##0}", Items.Where(i => i.InvType != null).Sum(i => i.InvType.MedianBuy * i.Quantity));
                tbTotalSold.Text = "";
            }
        }

        private void ValueDumpUIFormClosed(object sender, FormClosedEventArgs e)
        {
            Cache.Instance.DirectEve.Dispose();
            Cache.Instance.DirectEve = null;
        }

        private void BtnStopClick(object sender, EventArgs e)
        {
             _States.CurrentValueDumpState = ValueDumpState.Idle;
        }

        private void UpdateMineralPricesButtonClick(object sender, EventArgs e)
        {
             _States.CurrentValueDumpState = ValueDumpState.CheckMineralPrices;
        }

        private void LvItemsColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListViewColumnSort oCompare = new ListViewColumnSort();

            if (lvItems.Sorting == SortOrder.Ascending)
                oCompare.Sorting = SortOrder.Descending;
            else
                oCompare.Sorting = SortOrder.Ascending;
            lvItems.Sorting = oCompare.Sorting;
            oCompare.ColumnIndex = e.Column;

            switch (e.Column)
            {
                case 1:
                    oCompare.CompararPor = ListViewColumnSort.TipoCompare.Cadena;
                    break;
                case 2:
                    oCompare.CompararPor = ListViewColumnSort.TipoCompare.Numero;
                    break;
                case 3:
                    oCompare.CompararPor = ListViewColumnSort.TipoCompare.Numero;
                    break;
                case 4:
                    oCompare.CompararPor = ListViewColumnSort.TipoCompare.Numero;
                    break;
                case 5:
                    oCompare.CompararPor = ListViewColumnSort.TipoCompare.Numero;
                    break;
                case 6:
                    oCompare.CompararPor = ListViewColumnSort.TipoCompare.Numero;
                    break;
            }

            lvItems.ListViewItemSorter = oCompare;
        }

        private void ValueDumpUILoad(object sender, EventArgs e)
        {
        }

        private void LvItemsSelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void RefineCheckBoxCheckedChanged(object sender, EventArgs e)
        {
        }
    }
}