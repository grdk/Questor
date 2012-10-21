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
    using Questor.Modules.BackgroundTasks;

    public class UnloadLoot
    {
        public const int StationContainer = 17366;

        private static DateTime _nextUnloadAction = DateTime.Now;
        private static DateTime _lastUnloadAction = DateTime.MinValue;
        private static int _lootToMoveWillStillNotFitCount;
        private static DateTime _lastPulse;

        //public double LootValue { get; set; }

        public void ProcessState()
        {
            // Only pulse state changes every 1.5s
            if (DateTime.Now.Subtract(_lastPulse).TotalMilliseconds < Time.Instance.QuestorPulse_milliseconds) //default: 1500ms
                return;
            _lastPulse = DateTime.Now;

            if (!Cache.Instance.InStation)
                return;

            if (Cache.Instance.InSpace)
                return;

            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20)) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return;

            switch (_States.CurrentUnloadLootState)
            {
                case UnloadLootState.Idle:
                case UnloadLootState.Done:
                    break;

                case UnloadLootState.Begin:
                    if (!Cache.Instance.OpenCargoHold("UnloadLoot")) break;
                    if (DateTime.Now < _nextUnloadAction)
                    {
                        Logging.Log("Unloadloot", "will Continue in [ " + Math.Round(_nextUnloadAction.Subtract(DateTime.Now).TotalSeconds, 0) + " ] sec", Logging.White);
                        break;
                    }
                    if (Cache.Instance.CargoHold.Items.Count == 0 && Cache.Instance.CargoHold.IsValid)
                        _States.CurrentUnloadLootState = UnloadLootState.Done;
                    else
                        _States.CurrentUnloadLootState = UnloadLootState.MoveCommonMissionCompletionItemsToAmmoHangar;
                    break;

                case UnloadLootState.MoveCommonMissionCompletionItemsToAmmoHangar:
                    if (!Cache.Instance.OpenCargoHold("UnloadLoot")) return;
                    if (!Cache.Instance.ReadyAmmoHangar("UnloadLoot")) return;
                    //
                    // how do we get IsMissionItem to work for us here? (see ItemCache)
                    // Zbikoki's Hacker Card 28260, Reports 3814, Gate Key 2076, Militants 25373, Marines 3810, i.groupid == 314 (Misc Mission Items, mainly for storylines) and i.GroupId == 283 (Misc Mission Items, mainly for storylines)
                    //
                    IEnumerable<DirectItem> itemsToMove = Cache.Instance.CargoHold.Items.Where(i => (i.TypeName ?? string.Empty).ToLower() == Cache.Instance.BringMissionItem || i.TypeId == 17192 || i.TypeId == 17206 || i.GroupId == 283 || i.GroupId == 314);

                    if (Cache.Instance.AmmoHangar != null)
                        Cache.Instance.AmmoHangar.Add(itemsToMove);

                    _States.CurrentUnloadLootState = UnloadLootState.MoveAmmo;
                    break;

                case UnloadLootState.MoveLoot:
                    if (!Cache.Instance.OpenCargoHold("UnloadLoot")) return;
                    if (!Cache.Instance.OpenLootHangar("UnloadLoot")) return;

                    IEnumerable<DirectItem> lootToMove = Cache.Instance.CargoHold.Items.Where(i => (i.TypeName ?? string.Empty).ToLower() != Cache.Instance.BringMissionItem && Settings.Instance.Ammo.All(a => a.TypeId != i.TypeId)).ToList();
                    IEnumerable<DirectItem> somelootToMove = Cache.Instance.CargoHold.Items.Where(i => (i.TypeName ?? string.Empty).ToLower() != Cache.Instance.BringMissionItem && Settings.Instance.Ammo.All(a => a.TypeId != i.TypeId)).ToList();
                    foreach (DirectItem item in lootToMove)
                    {
                        if (!Cache.Instance.InvTypesById.ContainsKey(item.TypeId))
                            continue;

                        InvType invType = Cache.Instance.InvTypesById[item.TypeId];
                        Statistics.Instance.LootValue += (int)(invType.MedianBuy ?? 0) * Math.Max(item.Quantity, 1);
                    }

                    if (string.IsNullOrEmpty(Settings.Instance.LootHangar)) // if we do NOT have the loot hangar configured. 
                    {
                        if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "loothangar setting is not configured, assuming lothangar is local items hangar (and its 999 item limit)", Logging.White);
                        // Move loot to the loot hangar
                        int roominHangar = (999 - Cache.Instance.LootHangar.Items.Count);
                        if (roominHangar > lootToMove.Count())
                        {
                            if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "Loothangar has plenty of room to move loot all in one go", Logging.White);
                            Cache.Instance.LootHangar.Add(lootToMove);
                            _lootToMoveWillStillNotFitCount = 0;
                        }
                        else
                        {
                            Logging.Log("Unloadloot",
                                        "Loothangar is almost full and contains [" +
                                        Cache.Instance.LootHangar.Items.Count + "] of 999 total possible stacks",
                                        Logging.Orange);
                            if (roominHangar > 50)
                            {
                                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "Loothangar has more than 50 item slots left", Logging.White);
                                somelootToMove =
                                    Cache.Instance.CargoHold.Items.Where(
                                        i =>
                                        (i.TypeName ?? string.Empty).ToLower() != Cache.Instance.BringMissionItem &&
                                        Settings.Instance.Ammo.All(a => a.TypeId != i.TypeId)).ToList().GetRange(0, 49).ToList();
                            }
                            else if (roominHangar > 20)
                            {
                                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "Loothangar has more than 20 item slots left", Logging.White);
                                somelootToMove =
                                    Cache.Instance.CargoHold.Items.Where(
                                        i =>
                                        (i.TypeName ?? string.Empty).ToLower() != Cache.Instance.BringMissionItem &&
                                        Settings.Instance.Ammo.All(a => a.TypeId != i.TypeId)).ToList().GetRange(0, 19).ToList();
                            }

                            if (somelootToMove.Any())
                            {
                                Logging.Log("UnloadLoot", "Moving [" + somelootToMove.Count() + "]  of [" + lootToMove.Count() + "] items into the loot hangar", Logging.White);
                                Cache.Instance.LootHangar.Add(somelootToMove);
                            }
                            else
                            {
                                if (_lootToMoveWillStillNotFitCount < 7)
                                {
                                    _lootToMoveWillStillNotFitCount++;
                                    Cache.Instance.StackLootHangar("Unloadloot");
                                }
                                else
                                {
                                    Logging.Log("Unloadloot",
                                                "We tried to stack the loothangar 7 times and we still could not fit all the LootToMove [" +
                                                Cache.Instance.CargoHold.Items.Where(
                                                    i =>
                                                    (i.TypeName ?? string.Empty).ToLower() !=
                                                    Cache.Instance.BringMissionItem &&
                                                    Settings.Instance.Ammo.All(a => a.TypeId != i.TypeId)).ToList() +
                                                "] into the LootHangar [" + Cache.Instance.LootHangar.Items.Count + "]",
                                                Logging.Red);
                                    _States.CurrentQuestorState = QuestorState.Error;
                                }
                                return;
                            }
                        }
                    }
                    else //we must be using the corp hangar as the loothangar
                    {
                        if (lootToMove.Any())
                        {
                            Logging.Log("UnloadLoot", "Moving [" + lootToMove.Count() + "]  of [" + Cache.Instance.LootHangar.Items.Count + "] items into the loot hangar", Logging.White);
                            Cache.Instance.LootHangar.Add(lootToMove);
                            return;
                        }
                    }

                    Logging.Log("UnloadLoot", "Loot was worth an estimated [" + Statistics.Instance.LootValue.ToString("#,##0") + "] isk in buy-orders", Logging.Teal);

                    //Move bookmarks to the bookmarks hangar
                    if (!string.IsNullOrEmpty(Settings.Instance.BookmarkHangar) && Settings.Instance.CreateSalvageBookmarks)
                    {
                        Logging.Log("UnloadLoot", "Creating salvage bookmarks in hangar", Logging.White);
                        List<DirectBookmark> bookmarks = Cache.Instance.BookmarksByLabel(Settings.Instance.BookmarkPrefix + " ");
                        var salvageBMs = new List<long>();
                        if (bookmarks.Any())
                        {
                            foreach (DirectBookmark bookmark in bookmarks)
                            {
                                if (bookmark.BookmarkId != null) salvageBMs.Add((long)bookmark.BookmarkId);
                                if (salvageBMs.Count == 5)
                                {
                                    if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "found 5 bookmarks to move to the bookmark hangar, moving those, deleting our local copies and re-checking for more BMs to move", Logging.White);
                                    Cache.Instance.ItemHangar.AddBookmarks(salvageBMs);
                                    salvageBMs.Clear();
                                    return;
                                }
                            }
                            if (salvageBMs.Count > 0)
                            {
                                if (Settings.Instance.DebugUnloadLoot) Logging.Log("UnloadLoot", "found [" + salvageBMs.Count + "] BMs to move. Moving those, deleting local copies and re-checking for more BMs to move", Logging.White);
                                Cache.Instance.ItemHangar.AddBookmarks(salvageBMs);
                                salvageBMs.Clear();
                                return;
                            }
                        }
                    }
                    _States.CurrentUnloadLootState = UnloadLootState.WaitForMove;
                    break;

                case UnloadLootState.MoveAmmo:
                    if (!Cache.Instance.OpenCargoHold("UnloadLoot")) return;
                    if (!Cache.Instance.ReadyAmmoHangar("UnloadLoot")) return;
                    //
                    // if items in the hangar + items to move is greater than 1000 then we need to do something else (what?)
                    // - we could possibly move less items at a time, and stack in between?
                    // - maybe like 10 items at a time until we cant even move 10...
                    //
                    // could we get fancy and move things into a freight container??!?
                    if (Cache.Instance.AmmoHangar.IsValid)
                    {
                        Logging.Log("UnloadLoot", "Moving Ammo to AmmoHangar [" + Cache.Instance.AmmoHangar.DataId + "]", Logging.White);
                        // Move the mission item & ammo to the ammo hangar
                        Cache.Instance.AmmoHangar.Add(Cache.Instance.CargoHold.Items.Where(i => ((i.TypeName ?? string.Empty).ToLower() == Cache.Instance.BringMissionItem || Settings.Instance.Ammo.Any(a => a.TypeId == i.TypeId))));
                        //Cache.Instance.AmmoHangar.Add(Cache.Instance.CargoHold.Items.Where(i => Settings.Instance.Ammo.Any(a => a.TypeId == i.TypeId)));
                        Logging.Log("UnloadLoot", "Waiting for items to move to AmmoHangar", Logging.White);
                        _nextUnloadAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(2, 4));
                        _lastUnloadAction = DateTime.Now;
                        _States.CurrentUnloadLootState = UnloadLootState.StackAmmoHangar;
                        break;
                    }
                    Logging.Log("UnloadLoot", "AmmoHangar is not yet ready. waiting...", Logging.White);
                    break;

                case UnloadLootState.WaitForMove:
                    if (!Cache.Instance.OpenCargoHold("UnloadLoot")) return;

                    if (Cache.Instance.CargoHold.Items.Count != 0)
                    {
                        //Logging.Log("UnloadLoot","WaitForMove: 1");
                        _nextUnloadAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                        _lastUnloadAction = DateTime.Now;
                        break;
                    }

                    // Wait x seconds after moving
                    if (DateTime.Now < _nextUnloadAction)
                        break;

                    if (Cache.Instance.DirectEve.GetLockedItems().Count == 0)
                    {
                        if (Cache.Instance.CorpBookmarkHangar != null && Settings.Instance.CreateSalvageBookmarks)
                        {
                            Logging.Log("UnloadLoot", "Moving salvage bookmarks to corporate hangar", Logging.White);
                            Cache.Instance.CorpBookmarkHangar.Add(Cache.Instance.ItemHangar.Items.Where(i => i.TypeId == 51));
                        }
                        _nextUnloadAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(1, 5));
                        Logging.Log("UnloadLoot", "Stacking items in Loot Hangar: resuming in [ " + Math.Round(_nextUnloadAction.Subtract(DateTime.Now).TotalSeconds, 0) + " sec ]", Logging.White);
                        _States.CurrentUnloadLootState = UnloadLootState.StackLootHangar;
                        break;
                    }

                    if (DateTime.Now.Subtract(_lastUnloadAction).TotalSeconds > 120)
                    {
                        Logging.Log("UnloadLoot", "Moving items timed out, clearing item locks", Logging.Orange);
                        Cache.Instance.DirectEve.UnlockItems();
                        _States.CurrentUnloadLootState = UnloadLootState.MoveLoot;
                        break;
                    }
                    break;

                case UnloadLootState.StackAmmoHangar:
                    // Don't stack until 5 seconds after the cargo has cleared
                    if (DateTime.Now < _nextUnloadAction)
                        break;

                    if (!Cache.Instance.StackAmmoHangar("UnloadLoot.StackAmmoHangar")) break;

                    _States.CurrentUnloadLootState = UnloadLootState.WaitForAmmoHangarStacking;
                    break;

                case UnloadLootState.WaitForAmmoHangarStacking:
                    // Wait 5 seconds after stacking
                    if (DateTime.Now < _nextUnloadAction)
                        break;

                    if (Cache.Instance.DirectEve.GetLockedItems().Count == 0)
                    {
                        Logging.Log("UnloadLoot.AmmoHangarStacking", "Done stacking AmmoHangar", Logging.White);
                        _nextUnloadAction = DateTime.Now.AddMilliseconds(Cache.Instance.RandomNumber(1000, 3000));
                        _States.CurrentUnloadLootState = UnloadLootState.CloseAmmoHangar;
                        break;
                    }

                    if (DateTime.Now.Subtract(_lastUnloadAction).TotalSeconds > 120)
                    {
                        Logging.Log("UnloadLoot", "Stacking items in AmmoHangar timed out, clearing item locks", Logging.Orange);
                        Cache.Instance.DirectEve.UnlockItems();

                        Logging.Log("UnloadLoot", "Done stacking AmmoHangar", Logging.White);
                        _States.CurrentUnloadLootState = UnloadLootState.MoveAmmo;
                        break;
                    }
                    break;

                case UnloadLootState.CloseAmmoHangar:
                    if (!Cleanup.CloseInventoryWindows()) break;
                    _States.CurrentUnloadLootState = UnloadLootState.MoveLoot;
                    break;

                case UnloadLootState.StackLootHangar:
                    // Don't stack until 5 seconds after the cargo has cleared
                    if (DateTime.Now < _nextUnloadAction)
                        break;

                    if (!Cache.Instance.StackLootHangar("UnloadLoot.StackLootHangar"))
                    {
                        return;
                    }

                    _nextUnloadAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(1, 3));
                    _States.CurrentUnloadLootState = UnloadLootState.WaitForLootHangarStacking;
                    break;

                case UnloadLootState.WaitForLootHangarStacking:
                    // Wait 5 seconds after stacking
                    if (DateTime.Now < _nextUnloadAction)
                        break;

                    if (Cache.Instance.DirectEve.GetLockedItems().Count == 0)
                    {
                        Logging.Log("UnloadLoot.LootHangarStacking", "Done, stacking LootHangar", Logging.White);
                        _States.CurrentUnloadLootState = UnloadLootState.CloseLootHangar;
                        break;
                    }

                    if (DateTime.Now.Subtract(_lastUnloadAction).TotalSeconds > 120)
                    {
                        Logging.Log("UnloadLoot", "Stacking items in LootHangar timed out, clearing item locks", Logging.Orange);
                        Cache.Instance.DirectEve.UnlockItems();
                        Logging.Log("UnloadLoot", "Done, stacking LootHangar", Logging.White);
                        _States.CurrentUnloadLootState = UnloadLootState.CloseLootHangar;
                        break;
                    }
                    break;

                case UnloadLootState.CloseLootHangar:
                    if (!Cleanup.CloseInventoryWindows()) break;
                    _States.CurrentUnloadLootState = UnloadLootState.Done;
                    break;
            }
        }
    }
}