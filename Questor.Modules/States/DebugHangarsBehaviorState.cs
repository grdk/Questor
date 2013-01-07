﻿namespace Questor.Modules.States
{
    public enum DebugHangarsBehaviorState
    {
        ReadyItemsHangar,
        StackItemsHangar,
        CloseItemsHangar,
        OpenShipsHangar,
        StackShipsHangar,
        CloseShipsHangar,
        OpenLootContainer,
        StackLootContainer,
        CloseLootContainer,
        OpenCorpAmmoHangar,
        StackCorpAmmoHangar,
        CloseCorpAmmoHangar,
        OpenCorpLootHangar,
        StackCorpLootHangar,
        CloseCorpLootHangar,
        OpenAmmoHangar,
        StackAmmoHangar,
        CloseAmmoHangar,
        OpenLootHangar,
        StackLootHangar,
        CloseLootHangar,
        CloseAllInventoryWindows,
        GetAmmoHangarID,
        GetLootHangarID,
        OpenCargoHold,
        StackCargoHold,
        CloseCargoHold,
        Default,
        Idle,
        CombatHelper,
        Salvage,
        Arm,
        LocalWatch,
        DelayedGotoBase,
        GotoBase,
        UnloadLoot,
        GotoNearestStation,
        Error,
        Paused,
        Panic,
        Traveler,
        OpenInventory,
        ListInvTree,
        OpenOreHold,
    }
}