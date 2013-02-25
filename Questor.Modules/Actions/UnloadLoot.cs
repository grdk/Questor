﻿// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace Questor.Modules.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.States;

    public class UnloadLoot
    {
        private static DateTime _nextUnloadAction = DateTime.UtcNow;
        private static DateTime _lastUnloadAction = DateTime.MinValue;

        private static DateTime _lastPulse;

        private static bool AmmoIsBeingMoved;
        private static bool LootIsBeingMoved;
        private static bool AllLootWillFit;
        private static IEnumerable<DirectItem> ammoToMove;
        private static IEnumerable<DirectItem> scriptsToMove;
        private static IEnumerable<DirectItem> commonMissionCompletionItemsToMove;
        private static IEnumerable<DirectItem> missionGateKeysToMove; 

        //public UnloadLoot()
        //{
        //    ItemsToMove = new List<ItemCache>();
        //}

        //public double LootValue { get; set; }

        private bool MoveLoot()
        {
            if (DateTime.UtcNow < _nextUnloadAction)
            {
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveLoot", "will Continue in [ " + Math.Round(_nextUnloadAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + " ] sec", Logging.White);
                return false;
            }

            if (LootIsBeingMoved && AllLootWillFit)
            {
                if (Cache.Instance.DirectEve.GetLockedItems().Count != 0)
                {
                    if (DateTime.UtcNow.Subtract(_lastUnloadAction).TotalSeconds > 120)
                    {
                        Logging.Log("UnloadLootState.MoveLoot", "Moving Loot timed out, clearing item locks", Logging.Orange);
                        Cache.Instance.DirectEve.UnlockItems();
                        _lastUnloadAction = DateTime.UtcNow.AddSeconds(-10);
                        _States.CurrentUnloadLootState = UnloadLootState.Begin;
                        return false;
                    }

                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveLoot", "Waiting for Locks to clear. GetLockedItems().Count [" + Cache.Instance.DirectEve.GetLockedItems().Count + "]", Logging.Teal);
                    return false;
                }
            }

            if (DateTime.UtcNow.Subtract(Cache.Instance.LastStackLootHangar).TotalSeconds < 10)
            {
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveLoot", "if (DateTime.UtcNow.Subtract(Cache.Instance.LastStackLootHangar).TotalSeconds < 30)", Logging.Teal);
                if (!Cache.Instance.CloseLootHangar("UnloadLootState.MoveLoot")) return false;
                Logging.Log("UnloadLoot.MoveLoot", "Loot was worth an estimated [" + Statistics.Instance.LootValue.ToString("#,##0") + "] isk in buy-orders", Logging.Teal);
                LootIsBeingMoved = false;
                AllLootWillFit = false;
                _States.CurrentUnloadLootState = UnloadLootState.Done;
                return true;
            }

            if (!Cache.Instance.OpenCargoHold("UnloadLoot")) return false;

            if (Cache.Instance.CargoHold.IsValid)
            {
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveLoot", "if (Cache.Instance.CargoHold.IsValid)", Logging.White);
                IEnumerable<DirectItem> lootToMove = Cache.Instance.CargoHold.Items.ToList();

                //IEnumerable<DirectItem> somelootToMove = lootToMove;
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveLoot", "foreach (DirectItem item in lootToMove) (start)", Logging.White);

                int y = lootToMove.Count();
                int x = 1;
                    
                foreach (DirectItem item in lootToMove)
                {
                    if (!Cache.Instance.InvTypesById.ContainsKey(item.TypeId))
                        continue;

                    if (item.Volume != 0)
                    {
                        if (Settings.Instance.DebugLootValue) Logging.Log("UnloadLoot.Lootvalue","[" + x + "of" + y + "] ItemName [" + item.TypeName + "] ItemTypeID [" + item.TypeId + "] AveragePrice[" + (int)item.AveragePrice() + "]",Logging.Debug);
                        Statistics.Instance.LootValue += (int)item.AveragePrice() * Math.Max(item.Quantity, 1);
                    }
                    x++;
                }

                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveLoot", "foreach (DirectItem item in lootToMove) (done)", Logging.White);
                if (lootToMove.Any() && !LootIsBeingMoved)
                {
                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveLoot", "if (lootToMove.Any() && !LootIsBeingMoved))", Logging.White);

                    if (!Cache.Instance.ReadyLootHangar("UnloadLoot.MoveLoot")) return false;

                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveLoot", "if (Cache.Instance.LootHangar.IsValid)", Logging.White);

                    if (string.IsNullOrEmpty(Settings.Instance.LootHangar)) // if we do NOT have the loot hangar configured.
                    {
                        /*
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.Moveloot", "LootHangar setting is not configured, assuming lothangar is local items hangar (and its 999 item limit)", Logging.White);

                        // Move loot to the loot hangar
                        int roominHangar = (999 - Cache.Instance.LootHangar.Items.Count);
                        if (roominHangar > lootToMove.Count())
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.Moveloot", "LootHangar has plenty of room to move loot all in one go", Logging.White);
                            Cache.Instance.LootHangar.Add(lootToMove);
                            AllLootWillFit = true;
                            _lootToMoveWillStillNotFitCount = 0;
                            return;
                        }

                        AllLootWillFit = false;
                        Logging.Log("Unloadloot", "LootHangar is almost full and contains [" + Cache.Instance.LootHangar.Items.Count + "] of 999 total possible stacks", Logging.Orange);
                        if (roominHangar > 50)
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "LootHangar has more than 50 item slots left", Logging.White);
                            somelootToMove = lootToMove.Where(i => Settings.Instance.Ammo.All(a => a.TypeId != i.TypeId)).ToList().GetRange(0, 49).ToList();
                        }
                        else if (roominHangar > 20)
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "LootHangar has more than 20 item slots left", Logging.White);
                            somelootToMove = lootToMove.Where(i => Settings.Instance.Ammo.All(a => a.TypeId != i.TypeId)).ToList().GetRange(0, 19).ToList();
                        }

                        if (somelootToMove.Any())
                        {
                            Logging.Log("UnloadLoot", "Moving [" + somelootToMove.Count() + "]  of [" + lootToMove.Count() + "] items into the LootHangar", Logging.White);
                            Cache.Instance.LootHangar.Add(somelootToMove);
                            return;
                        }

                        if (_lootToMoveWillStillNotFitCount < 7)
                        {
                            _lootToMoveWillStillNotFitCount++;
                            if (!Cache.Instance.StackLootHangar("Unloadloot")) return;
                            return;
                        }

                        Logging.Log("Unloadloot", "We tried to stack the loothangar 7 times and we still could not fit all the LootToMove into the LootHangar [" + Cache.Instance.LootHangar.Items.Count + " items ]", Logging.Red);
                        _States.CurrentQuestorState = QuestorState.Error;
                        return;
                        */
                    }

                    //
                    // if we are using the corp hangar then just grab all the loot in one go.
                    //
                    if (lootToMove.Any() && !LootIsBeingMoved)
                    {
                        //Logging.Log("UnloadLoot", "Moving [" + lootToMove.Count() + "] items from CargoHold to LootHangar which has [" + Cache.Instance.LootHangar.Items.Count() + "] items in it now.", Logging.White);
                        Logging.Log("UnloadLoot.MoveLoot", "Moving [" + lootToMove.Count() + "] items from CargoHold to LootHangar", Logging.White);
                        AllLootWillFit = true;
                        LootIsBeingMoved = true;
                        Cache.Instance.LootHangar.Add(lootToMove);
                        _nextUnloadAction = DateTime.UtcNow.AddSeconds(5);
                        return false;
                    }
                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveLoot", "1) if (lootToMove.Any()) is false", Logging.White);
                    return false;
                }

                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveLoot", "2) if (lootToMove.Any()) is false", Logging.White);
                //
                // Stack LootHangar
                //
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveLoot", "if (!Cache.Instance.StackLootHangar(UnloadLoot.MoveLoot)) return;", Logging.White);
                _nextUnloadAction = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(4, 6));
                if (!Cache.Instance.StackLootHangar("UnloadLoot.MoveLoot")) return false;
                return true;
            }

            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveLoot", "Cache.Instance.CargoHold is not yet valid", Logging.White);
            return false;
        }

        private bool MoveAmmo()
        {
            if (DateTime.UtcNow < _nextUnloadAction)
            {
                Logging.Log("UnloadLoot.MoveAmmo", "will Continue in [ " + Math.Round(_nextUnloadAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + " ] sec", Logging.White);
                return false;
            }

            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "Entering MoveAmmo(), again...", Logging.White);
            
            if (AmmoIsBeingMoved)
            {
                if (Cache.Instance.DirectEve.GetLockedItems().Count != 0)
                {
                    if (DateTime.UtcNow.Subtract(_lastUnloadAction).TotalSeconds > 120)
                    {
                        Logging.Log("UnloadLoot.MoveAmmo", "Moving Ammo timed out, clearing item locks", Logging.Orange);
                        Cache.Instance.DirectEve.UnlockItems();
                        _lastUnloadAction = DateTime.UtcNow.AddSeconds(-10);
                        _States.CurrentUnloadLootState = UnloadLootState.Begin;
                        return false;
                    }

                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveAmmo", "Waiting for Locks to clear. GetLockedItems().Count [" + Cache.Instance.DirectEve.GetLockedItems().Count + "]", Logging.Teal);
                    return false;
                }
                AmmoIsBeingMoved = false;
                return false;
            }

            if (DateTime.UtcNow.Subtract(Cache.Instance.LastStackAmmoHangar).TotalSeconds < 10)
            {
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveAmmo", "if (DateTime.UtcNow.Subtract(Cache.Instance.LastStackAmmoHangar).TotalSeconds < 30)", Logging.Teal);
                if (!Cache.Instance.CloseAmmoHangar("UnloadLootState.MoveAmmo")) return false;
                Logging.Log("UnloadLoot.MoveAmmo", "Done Moving Ammo", Logging.White);
                AmmoIsBeingMoved = false;
                _States.CurrentUnloadLootState = UnloadLootState.MoveLoot;
                return true;
            }

            if (!Cache.Instance.OpenCargoHold("UnloadLoot.MoveAmmo")) return false;

            if (Cache.Instance.CargoHold.Window.IsReady)
            {
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveAmmo", "if (Cache.Instance.CargoHold.IsValid && Cache.Instance.CargoHold.Items.Any())", Logging.Teal);

                if (!Cache.Instance.ReadyAmmoHangar("UnloadLoot.MoveAmmo")) return false;

                if (Cache.Instance.AmmoHangar.IsValid)
                {
                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLootState.MoveAmmo", "if (Cache.Instance.AmmoHangar.IsValid)", Logging.Teal);

                    //
                    // Add Ammo to the list of things to move
                    //
                    try
                    {
                        ammoToMove = Cache.Instance.CargoHold.Items.Where(i => Settings.Instance.Ammo.Any(a => a.TypeId == i.TypeId) || Settings.Instance.CapacitorInjectorScript == i.TypeId).ToList();
                    }
                    catch (Exception exception)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "No Ammo Found in CargoHold: moving on. [" + exception + "]", Logging.White);
                    }

                    if (ammoToMove != null)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (ammoToMove != null)", Logging.White);
                        if (ammoToMove.Any())
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (ammoToMove.Any())", Logging.White);
                            if (!Cache.Instance.ReadyAmmoHangar("UnloadLoot")) return false;
                            Logging.Log("UnloadLoot.MoveAmmo", "Moving [" + ammoToMove.Count() + "] Ammo Stacks to AmmoHangar", Logging.White);
                            AmmoIsBeingMoved = true;
                            Cache.Instance.AmmoHangar.Add(ammoToMove);
                            _nextUnloadAction = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(2, 4));
                            return false;
                        }
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "No Ammo Found in CargoHold: moving on.", Logging.White);
                    }

                    //
                    // Add gatekeys to the list of things to move to the AmmoHangar, they are not mission completion items but are used during missions so should be avail
                    // to all pilots (thus the use of the ammo hangar)
                    //
                    try
                    {
                        missionGateKeysToMove = Cache.Instance.CargoHold.Items.Where(i => i.TypeId == (int)TypeID.AngelDiamondTag 
                                                                                       || i.TypeId == (int)TypeID.GuristasDiamondTag
                                                                                       || i.TypeId == (int)TypeID.ImperialNavyGatePermit
                                                                                       || i.GroupId == (int)Group.AccelerationGateKeys).ToList();
                    }
                    catch (Exception exception)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "No Mission GateKeys Found in CargoHold: moving on. [" + exception + "]", Logging.White);
                    }

                    if (missionGateKeysToMove != null)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (missionGateKeysToMove != null)", Logging.White);
                        if (missionGateKeysToMove.Any())
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (missionGateKeysToMove.Any())", Logging.White);
                            if (!Cache.Instance.ReadyAmmoHangar("UnloadLoot")) return false;
                            Logging.Log("UnloadLoot.MoveAmmo", "Moving [" + missionGateKeysToMove.Count() + "] Mission GateKeys to AmmoHangar", Logging.White);
                            AmmoIsBeingMoved = true;
                            Cache.Instance.AmmoHangar.Add(missionGateKeysToMove);
                            _nextUnloadAction = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(2, 4));
                            return false;
                        }

                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "No Mission GateKeys Found in CargoHold: moving on.", Logging.White);
                    }
                    

                    //
                    // Add mission item  to the list of things to move to the itemhangar as they will be needed to complete the mission
                    //
                    try
                    {

                        //Cache.Instance.InvTypesById.ContainsKey(i.TypeId)
                        commonMissionCompletionItemsToMove = Cache.Instance.CargoHold.Items.Where(i => i.GroupId == (int)Group.Livestock 
                                                                                                    || i.GroupId == (int)Group.MiscSpecialMissionItems
                                                                                                    || !Cache.Instance.UnloadLootTheseItemsAreLootById.ContainsKey(i.TypeId)
                                                                                                    || (i.GroupId == (int)Group.Commodities && i.TypeId != (int)TypeID.MetalScraps && i.TypeId != (int)TypeID.ReinforcedMetalScraps)).ToList();
                    }
                    catch (Exception exception)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "No Mission CompletionItems Found in CargoHold: moving on. [" + exception + "]", Logging.White);
                    }

                    
                    if (commonMissionCompletionItemsToMove != null)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (commonMissionCompletionItemsToMove != null)", Logging.White);
                        if (commonMissionCompletionItemsToMove.Any())
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "if (commonMissionCompletionItemsToMove.Any())", Logging.White);
                            if (!Cache.Instance.OpenItemsHangar("UnloadLoot.MoveAmmo")) return false;
                            Logging.Log("UnloadLoot.MoveAmmo", "Moving [" + commonMissionCompletionItemsToMove.Count() + "] Mission Completion items to ItemHangar", Logging.White);
                            Cache.Instance.ItemHangar.Add(commonMissionCompletionItemsToMove);
                            AmmoIsBeingMoved = true;
                            _nextUnloadAction = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(2, 4));
                            return false;
                        }
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "No Mission CompletionItems Found in CargoHold: moving on.", Logging.White);
                    }
                    
                    //
                    // Add Scripts (by groupID) to the list of things to move
                    //

                    try
                    {
                        //
                        // items to move has to be cleared here before assigning but is currently not being cleared here
                        //
                        scriptsToMove = Cache.Instance.CargoHold.Items.Where(i =>
                            i.TypeId == (int)TypeID.AncillaryShieldBoosterScript ||
                            i.TypeId == (int)TypeID.CapacitorInjectorScript ||
                            i.TypeId == (int)TypeID.FocusedWarpDisruptionScript ||
                            i.TypeId == (int)TypeID.OptimalRangeDisruptionScript ||
                            i.TypeId == (int)TypeID.OptimalRangeScript ||
                            i.TypeId == (int)TypeID.ScanResolutionDampeningScript ||
                            i.TypeId == (int)TypeID.ScanResolutionScript ||
                            i.TypeId == (int)TypeID.TargetingRangeDampeningScript ||
                            i.TypeId == (int)TypeID.TargetingRangeScript ||
                            i.TypeId == (int)TypeID.TrackingSpeedDisruptionScript ||
                            i.TypeId == (int)TypeID.TrackingSpeedScript ||
                            i.GroupId == (int)Group.CapacitorGroupCharge).ToList();
                    }
                    catch (Exception exception)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "MoveAmmo: No Scripts Found in CargoHold: moving on. [" + exception + "]", Logging.White);
                    }

                    if (scriptsToMove != null)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (scriptsToMove != null)", Logging.White);
                        if (scriptsToMove.Any())
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (scriptsToMove.Any())", Logging.White);
                            if (!Cache.Instance.OpenItemsHangar("UnloadLoot.MoveAmmo")) return false;
                            Logging.Log("UnloadLoot.MoveAmmo", "Moving [" + scriptsToMove.Count() + "] Scripts to ItemHangar", Logging.White);
                            AmmoIsBeingMoved = true;
                            Cache.Instance.ItemHangar.Add(scriptsToMove);
                            _nextUnloadAction = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(2, 4));
                            return false;
                        }
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "No Scripts Found in CargoHold: moving on.", Logging.White);
                    }

                    //
                    // Stack AmmoHangar
                    //
                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "if (!Cache.Instance.StackAmmoHangar(UnloadLoot.MoveAmmo)) return;", Logging.White);
                    _nextUnloadAction = DateTime.UtcNow.AddSeconds(Cache.Instance.RandomNumber(2, 4)); 
                    if (!Cache.Instance.StackAmmoHangar("UnloadLoot.MoveAmmo")) return false;
                    return true;

                }
                
                if (Settings.Instance.DebugUnloadLoot) Logging.Log("Unloadloot.MoveAmmo", "Cache.Instance.AmmoHangar is Not yet valid", Logging.Teal);
                return false;
            }

            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot.MoveAmmo", "Cache.Instance.CargoHold is Not yet valid", Logging.Teal);
            return false;
        }

        public void ProcessState()
        {
            // Only pulse state changes every 1.5s
            if (DateTime.UtcNow.Subtract(_lastPulse).TotalMilliseconds < Time.Instance.QuestorPulse_milliseconds) //default: 1500ms
                return;
            _lastPulse = DateTime.UtcNow;

            if (!Cache.Instance.InStation)
                return;

            if (Cache.Instance.InSpace)
                return;

            if (DateTime.UtcNow < Cache.Instance.LastInSpace.AddSeconds(20)) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return;

            switch (_States.CurrentUnloadLootState)
            {
                case UnloadLootState.Idle:
                case UnloadLootState.Done:
                    break;

                case UnloadLootState.Begin:
                    if (DateTime.UtcNow < _nextUnloadAction)
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "will Continue in [ " + Math.Round(_nextUnloadAction.Subtract(DateTime.UtcNow).TotalSeconds, 0) + " ] sec", Logging.White);
                        break;
                    }
                    AmmoIsBeingMoved = false;
                    LootIsBeingMoved = false;
                    _lastUnloadAction = DateTime.UtcNow.AddSeconds(-10);
                    _States.CurrentUnloadLootState = UnloadLootState.MoveAmmo;
                    break;

                case UnloadLootState.MoveAmmo:
                    if (!MoveAmmo()) return;
                    _States.CurrentUnloadLootState = UnloadLootState.MoveLoot;
                    break;

                case UnloadLootState.MoveLoot:
                    if (!MoveLoot()) return;
                    _States.CurrentUnloadLootState = UnloadLootState.Done;
                    break;
            }
        }
    }
}