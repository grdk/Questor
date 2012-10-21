// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace Questor.Modules.Caching
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;
    using System.Xml.XPath;
    using global::Questor.Modules.Actions;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.States;
    using global::Questor.Modules.Logging;
    using DirectEve;
    using InnerSpaceAPI;

    public class Cache
    {
        /// <summary>
        ///   Singleton implementation
        /// </summary>
        private static Cache _instance = new Cache();

        /// <summary>
        ///   Active Drones
        /// </summary>
        private List<EntityCache> _activeDrones;

        private DirectAgent _agent;

        /// <summary>
        ///   Agent cache
        /// </summary>
        private long? _agentId;

        /// <summary>
        ///   Current Storyline Mission Agent
        /// </summary>
        public long CurrentStorylineAgentId { get; set; }

        /// <summary>
        ///   Agent blacklist
        /// </summary>
        public List<long> AgentBlacklist;

        /// <summary>
        ///   Approaching cache
        /// </summary>
        //private int? _approachingId;
        private EntityCache _approaching;

        /// <summary>
        ///   BigObjects we are likely to bump into (mainly LCOs)
        /// </summary>
        private List<EntityCache> _bigObjects;

        /// <summary>
        ///   BigObjects we are likely to bump into (mainly LCOs)
        /// </summary>
        private List<EntityCache> _gates;

        /// <summary>
        ///   BigObjects we are likely to bump into (mainly LCOs)
        /// </summary>
        private List<EntityCache> _bigObjectsAndGates;

        /// <summary>
        ///   objects we are likely to bump into (Anything that isnt an NPC a wreck or a can)
        /// </summary>
        private List<EntityCache> _objects;

        /// <summary>
        ///   Returns all non-empty wrecks and all containers
        /// </summary>
        private List<EntityCache> _containers;

        /// <summary>
        ///   Entities cache (all entities within 256km)
        /// </summary>
        private List<EntityCache> _entities;

        /// <summary>
        ///   Damaged drones
        /// </summary>
        public IEnumerable<EntityCache> DamagedDrones;

        /// <summary>
        ///   Entities by Id
        /// </summary>
        private readonly Dictionary<long, EntityCache> _entitiesById;

        /// <summary>
        ///   Module cache
        /// </summary>
        private List<ModuleCache> _modules;

        /// <summary>
        ///   Priority targets (e.g. warp scramblers or mission kill targets)
        /// </summary>
        public List<PriorityTarget> _priorityTargets;

        public String _priorityTargets_text;

        public DirectLocation MissionSolarSystem;

        public string DungeonId;

        /// <summary>
        ///   Star cache
        /// </summary>
        private EntityCache _star;

        /// <summary>
        ///   Station cache
        /// </summary>
        private List<EntityCache> _stations;

        /// <summary>
        ///   Stargate cache
        /// </summary>
        private List<EntityCache> _stargates;

        /// <summary>
        ///   Stargate by name
        /// </summary>
        private EntityCache _stargate;

        /// <summary>
        ///   Targeted by cache
        /// </summary>
        private List<EntityCache> _targetedBy;

        /// <summary>
        ///   Targeting cache
        /// </summary>
        private List<EntityCache> _targeting;

        /// <summary>
        ///   Targets cache
        /// </summary>
        private List<EntityCache> _targets;

        /// <summary>
        ///   Aggressed cache
        /// </summary>
        private List<EntityCache> _aggressed;

        /// <summary>
        ///   IDs in Inventory window tree (on left)
        /// </summary>
        public List<long> _IDsinInventoryTree;

        /// <summary>
        ///   Returns all unlooted wrecks & containers
        /// </summary>
        private List<EntityCache> _unlootedContainers;

        private List<EntityCache> _unlootedWrecksAndSecureCans;

        private List<DirectWindow> _windows;

        public void DirecteveDispose()
        {
            Logging.Log("QuestorUI", "started calling DirectEve.Dispose()", Logging.White);
            Cache.Instance.DirectEve.Dispose(); //could this hang?
            Logging.Log("QuestorUI", "finished calling DirectEve.Dispose()", Logging.White);
        }

        public Cache()
        {
            //string line = "Cache: new cache instance being instantiated";
            //InnerSpace.Echo(string.Format("{0:HH:mm:ss} {1}", DateTime.Now, line));
            //line = string.Empty;

            string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (path != null)
            {
                ShipTargetValues = new List<ShipTargetValue>();
                XDocument values = XDocument.Load(System.IO.Path.Combine(path, "ShipTargetValues.xml"));
                if (values.Root != null)
                    foreach (XElement value in values.Root.Elements("ship"))
                        ShipTargetValues.Add(new ShipTargetValue(value));

                InvTypesById = new Dictionary<int, InvType>();
                XDocument invTypes = XDocument.Load(System.IO.Path.Combine(path, "InvTypes.xml"));
                if (invTypes.Root != null)
                    foreach (XElement element in invTypes.Root.Elements("invtype"))
                        InvTypesById.Add((int)element.Attribute("id"), new InvType(element));
            }

            _priorityTargets = new List<PriorityTarget>();
            LastModuleTargetIDs = new Dictionary<long, long>();
            TargetingIDs = new Dictionary<long, DateTime>();
            _entitiesById = new Dictionary<long, EntityCache>();

            LootedContainers = new HashSet<long>();
            IgnoreTargets = new HashSet<string>();
            MissionItems = new List<string>();
            ChangeMissionShipFittings = false;
            UseMissionShip = false;
            ArmLoadedCache = false;
            MissionAmmo = new List<Ammo>();
            MissionUseDrones = null;

            PanicAttemptsThisPocket = 0;
            LowestShieldPercentageThisPocket = 100;
            LowestArmorPercentageThisPocket = 100;
            LowestCapacitorPercentageThisPocket = 100;
            PanicAttemptsThisMission = 0;
            LowestShieldPercentageThisMission = 100;
            LowestArmorPercentageThisMission = 100;
            LowestCapacitorPercentageThisMission = 100;
            LastKnownGoodConnectedTime = DateTime.Now;
        }

        /// <summary>
        ///   List of containers that have been looted
        /// </summary>
        public HashSet<long> LootedContainers { get; private set; }

        /// <summary>
        ///   List of targets to ignore
        /// </summary>
        public HashSet<string> IgnoreTargets { get; private set; }

        public static Cache Instance
        {
            get { return _instance; }
        }

        public bool ExitWhenIdle = false;
        public bool StopBot = false;
        public bool DoNotBreakInvul = false;
        public bool UseDrones = true;
        public bool LootAlreadyUnloaded = false;
        public bool MissionLoot = false;
        public bool SalvageAll = false;
        public bool RouteIsAllHighSecBool = false;

        public double Wealth { get; set; }

        public double WealthatStartofPocket { get; set; }

        public int PocketNumber { get; set; }

        public bool OpenWrecks = false;
        public bool NormalApproach = true;
        public bool CourierMission = false;
        public bool RepairAll = false;
        public bool doneUsingRepairWindow = false;
        public string MissionName = "";
        public int MissionsThisSession = 0;
        public int StopSessionAfterMissionNumber = int.MaxValue;
        public bool ConsoleLogOpened = false;
        public int TimeSpentReloading_seconds = 0;
        public int TimeSpentInMission_seconds = 0;
        public int TimeSpentInMissionInRange = 0;
        public int TimeSpentInMissionOutOfRange = 0;
        public int GreyListedMissionsDeclined = 0;
        public string LastGreylistMissionDeclined = string.Empty;
        public int BlackListedMissionsDeclined = 0;
        public string LastBlacklistMissionDeclined = string.Empty;

        public long AmmoHangarID = -99;
        public long LootHangarID = -99;

        public DirectAgentMission Mission;

        public bool DronesKillHighValueTargets { get; set; }

        public bool InMission { get; set; }

        public DateTime QuestorStarted_DateTime = DateTime.Now;

        public DateTime NextSalvageTrip = DateTime.Now;

        public bool MissionXMLIsAvailable { get; set; }

        public string MissionXmlPath { get; set; }

        public XDocument InvTypes;
        public string Path;

        public bool LocalSafe(int maxBad, double stand)
        {
            int number = 0;
            var local = (DirectChatWindow)GetWindowByName("Local");
            foreach (DirectCharacter localMember in local.Members)
            {
                float[] alliance = { DirectEve.Standings.GetPersonalRelationship(localMember.AllianceId), DirectEve.Standings.GetCorporationRelationship(localMember.AllianceId), DirectEve.Standings.GetAllianceRelationship(localMember.AllianceId) };
                float[] corporation = { DirectEve.Standings.GetPersonalRelationship(localMember.CorporationId), DirectEve.Standings.GetCorporationRelationship(localMember.CorporationId), DirectEve.Standings.GetAllianceRelationship(localMember.CorporationId) };
                float[] personal = { DirectEve.Standings.GetPersonalRelationship(localMember.CharacterId), DirectEve.Standings.GetCorporationRelationship(localMember.CharacterId), DirectEve.Standings.GetAllianceRelationship(localMember.CharacterId) };

                if (alliance.Min() <= stand || corporation.Min() <= stand || personal.Min() <= stand)
                {
                    Logging.Log("Cache.LocalSafe", "Bad Standing Pilot Detected: [ " + localMember.Name + "] " + " [ " + number + " ] so far... of [ " + maxBad + " ] allowed", Logging.Orange);
                    number++;
                }
                if (number > maxBad)
                {
                    Logging.Log("Cache.LocalSafe", "[" + number + "] Bad Standing pilots in local, We should stay in station", Logging.Orange);
                    return false;
                }
            }
            return true;
        }

        public DirectEve DirectEve { get; set; }

        public Dictionary<int, InvType> InvTypesById { get; private set; }

        /// <summary>
        ///   List of ship target values, higher target value = higher kill priority
        /// </summary>
        public List<ShipTargetValue> ShipTargetValues { get; private set; }

        /// <summary>
        ///   Best damage type for the mission
        /// </summary>
        public DamageType DamageType { get; set; }

        /// <summary>
        ///   Best orbit distance for the mission
        /// </summary>
        public int OrbitDistance { get; set; }

        /// <summary>
        ///   Force Salvaging after mission
        /// </summary>
        public bool AfterMissionSalvaging { get; set; }

        public double MaxRange
        {
            get { return Math.Min(Cache.Instance.WeaponRange, Cache.Instance.DirectEve.ActiveShip.MaxTargetRange); }
        }

        /// <summary>
        ///   Returns the maximum weapon distance
        /// </summary>
        public int WeaponRange
        {
            get
            {
                // Get ammo based on current damage type
                IEnumerable<Ammo> ammo = Settings.Instance.Ammo.Where(a => a.DamageType == DamageType).ToList();

                try
                {
                    // Is our ship's cargo available?
                    if ((Cache.Instance.CargoHold != null) && (Cache.Instance.CargoHold.IsValid))
                        ammo = ammo.Where(a => Cache.Instance.CargoHold.Items.Any(i => a.TypeId == i.TypeId && i.Quantity >= Settings.Instance.MinimumAmmoCharges));
                    else
                        return System.Convert.ToInt32(Cache.Instance.DirectEve.ActiveShip.MaxTargetRange);

                    // Return ship range if there's no ammo left
                    if (!ammo.Any())
                        return System.Convert.ToInt32(Cache.Instance.DirectEve.ActiveShip.MaxTargetRange);

                    return ammo.Max(a => a.Range);
                }
                catch (Exception ex)
                {
                    if (Settings.Instance.DebugExceptions) Logging.Log("Cache.WeaponRange", "exception was:" + ex.Message, Logging.Teal);
                    // Return max range
                    if (Cache.Instance.DirectEve.ActiveShip != null)
                    {
                        return System.Convert.ToInt32(Cache.Instance.DirectEve.ActiveShip.MaxTargetRange);
                    }
                    return 0;
                }
            }
        }

        /// <summary>
        ///   Last target for a certain module
        /// </summary>
        public Dictionary<long, long> LastModuleTargetIDs { get; private set; }

        /// <summary>
        ///   Targeting delay cache (used by LockTarget)
        /// </summary>
        public Dictionary<long, DateTime> TargetingIDs { get; private set; }

        /// <summary>
        ///   Used for Drones to know that it should retract drones
        /// </summary>
        public bool IsMissionPocketDone { get; set; }

        public string ExtConsole { get; set; }

        public string ConsoleLog { get; set; }

        public string ConsoleLogRedacted { get; set; }

        public bool AllAgentsStillInDeclineCoolDown { get; set; }

        private string _agentName = "";

        private DateTime _nextOpenContainerInSpaceAction;

        public DateTime NextOpenContainerInSpaceAction
        {
            get
            {
                return _nextOpenContainerInSpaceAction;
            }
            set
            {
                _nextOpenContainerInSpaceAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextOpenJournalWindowAction;

        public DateTime NextOpenJournalWindowAction
        {
            get
            {
                return _nextOpenJournalWindowAction;
            }
            set
            {
                _nextOpenJournalWindowAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextOpenLootContainerAction;

        public DateTime NextOpenLootContainerAction
        {
            get
            {
                return _nextOpenLootContainerAction;
            }
            set
            {
                _nextOpenLootContainerAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextOpenCorpBookmarkHangarAction;

        public DateTime NextOpenCorpBookmarkHangarAction
        {
            get
            {
                return _nextOpenCorpBookmarkHangarAction;
            }
            set
            {
                _nextOpenCorpBookmarkHangarAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextDroneBayAction;

        public DateTime NextDroneBayAction
        {
            get
            {
                return _nextDroneBayAction;
            }
            set
            {
                _nextDroneBayAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextOpenHangarAction;

        public DateTime NextOpenHangarAction
        {
            get { return _nextOpenHangarAction; }
            set
            {
                _nextOpenHangarAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextOpenCargoAction;

        public DateTime NextOpenCargoAction
        {
            get
            {
                return _nextOpenCargoAction;
            }
            set
            {
                _nextOpenCargoAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _lastAction = DateTime.Now;

        public DateTime LastAction
        {
            get
            {
                return _lastAction;
            }
            set
            {
                _lastAction = value;
            }
        }

        private DateTime _nextArmAction = DateTime.Now;

        public DateTime NextArmAction
        {
            get
            {
                return _nextArmAction;
            }
            set
            {
                _nextArmAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextSalvageAction = DateTime.Now;

        public DateTime NextSalvageAction
        {
            get
            {
                return _nextSalvageAction;
            }
            set
            {
                _nextSalvageAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextLootAction = DateTime.Now;

        public DateTime NextLootAction
        {
            get
            {
                return _nextLootAction;
            }
            set
            {
                _nextLootAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _lastJettison = DateTime.Now;

        public DateTime LastJettison
        {
            get
            {
                return _lastJettison;
            }
            set
            {
                _lastJettison = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextDefenseModuleAction = DateTime.Now;

        public DateTime NextDefenseModuleAction
        {
            get
            {
                return _nextDefenseModuleAction;
            }
            set
            {
                _nextDefenseModuleAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextAfterburnerAction = DateTime.Now;

        public DateTime NextAfterburnerAction
        {
            get { return _nextAfterburnerAction; }
            set
            {
                _nextAfterburnerAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextRepModuleAction = DateTime.Now;

        public DateTime NextRepModuleAction
        {
            get { return _nextRepModuleAction; }
            set
            {
                _nextRepModuleAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextActivateSupportModules = DateTime.Now;

        public DateTime NextActivateSupportModules
        {
            get { return _nextActivateSupportModules; }
            set
            {
                _nextActivateSupportModules = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextRemoveBookmarkAction = DateTime.Now;

        public DateTime NextRemoveBookmarkAction
        {
            get { return _nextRemoveBookmarkAction; }
            set
            {
                _nextRemoveBookmarkAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextApproachAction = DateTime.Now;

        public DateTime NextApproachAction
        {
            get { return _nextApproachAction; }
            set
            {
                _nextApproachAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextOrbit;

        public DateTime NextOrbit
        {
            get { return _nextOrbit; }
            set
            {
                _nextOrbit = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextWarpTo;

        public DateTime NextWarpTo
        {
            get { return _nextWarpTo; }
            set
            {
                _nextWarpTo = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextTravelerAction = DateTime.Now;

        public DateTime NextTravelerAction
        {
            get { return _nextTravelerAction; }
            set
            {
                _nextTravelerAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextTargetAction = DateTime.Now;

        public DateTime NextTargetAction
        {
            get { return _nextTargetAction; }
            set
            {
                _nextTargetAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextWeaponAction = DateTime.Now;
        private DateTime _nextReload = DateTime.Now;

        public DateTime NextReload
        {
            get { return _nextReload; }
            set
            {
                _nextReload = value;
                _lastAction = DateTime.Now;
            }
        }

        public DateTime NextWeaponAction
        {
            get { return _nextWeaponAction; }
            set
            {
                _nextWeaponAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextWebAction = DateTime.Now;

        public DateTime NextWebAction
        {
            get { return _nextWebAction; }
            set
            {
                _nextWebAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextNosAction = DateTime.Now;

        public DateTime NextNosAction
        {
            get { return _nextNosAction; }
            set
            {
                _nextNosAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextPainterAction = DateTime.Now;

        public DateTime NextPainterAction
        {
            get { return _nextPainterAction; }
            set
            {
                _nextPainterAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextActivateAction = DateTime.Now;

        public DateTime NextActivateAction
        {
            get { return _nextActivateAction; }
            set
            {
                _nextActivateAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextBookmarkPocketAttempt = DateTime.Now;

        public DateTime NextBookmarkPocketAttempt
        {
            get { return _nextBookmarkPocketAttempt; }
            set
            {
                _nextBookmarkPocketAttempt = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextAlign = DateTime.Now;

        public DateTime NextAlign
        {
            get { return _nextAlign; }
            set
            {
                _nextAlign = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextUndockAction = DateTime.Now;

        public DateTime NextUndockAction
        {
            get { return _nextUndockAction; }
            set
            {
                _nextUndockAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextDockAction = DateTime.Now; //unused

        public DateTime NextDockAction
        {
            get { return _nextDockAction; }
            set
            {
                _nextDockAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextDroneRecall;

        public DateTime NextDroneRecall
        {
            get { return _nextDroneRecall; }
            set
            {
                _nextDroneRecall = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextStartupAction;

        public DateTime NextStartupAction
        {
            get { return _nextStartupAction; }
            set
            {
                _nextStartupAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextRepairItemsAction;

        public DateTime NextRepairItemsAction
        {
            get { return _nextRepairItemsAction; }
            set
            {
                _nextRepairItemsAction = value;
                _lastAction = DateTime.Now;
            }
        }

        private DateTime _nextRepairDronesAction;

        public DateTime NextRepairDronesAction
        {
            get { return _nextRepairDronesAction; }
            set
            {
                _nextRepairDronesAction = value;
                _lastAction = DateTime.Now;
            }
        }

        public DateTime LastLocalWatchAction = DateTime.Now;
        public DateTime LastWalletCheck = DateTime.Now;
        public DateTime LastScheduleCheck = DateTime.Now;

        public DateTime LastUpdateOfSessionRunningTime;
        public DateTime NextInSpaceorInStation;
        public DateTime NextTimeCheckAction = DateTime.Now;

        public DateTime LastFrame = DateTime.Now;
        public DateTime LastSessionIsReady = DateTime.Now;
        public DateTime LastLogMessage = DateTime.Now;

        public int WrecksThisPocket;
        public int WrecksThisMission;
        public DateTime LastLoggingAction = DateTime.MinValue;

        public DateTime LastSessionChange = DateTime.Now;

        public bool Paused { get; set; }

        public int RepairCycleTimeThisPocket { get; set; }

        public int PanicAttemptsThisPocket { get; set; }

        private int GetShipsDroneBayAttempts { get; set; }

        public double LowestShieldPercentageThisMission { get; set; }

        public double LowestArmorPercentageThisMission { get; set; }

        public double LowestCapacitorPercentageThisMission { get; set; }

        public double LowestShieldPercentageThisPocket { get; set; }

        public double LowestArmorPercentageThisPocket { get; set; }

        public double LowestCapacitorPercentageThisPocket { get; set; }

        public int PanicAttemptsThisMission { get; set; }

        public DateTime StartedBoosting { get; set; }

        public int RepairCycleTimeThisMission { get; set; }

        public DateTime LastKnownGoodConnectedTime { get; set; }

        public long TotalMegaBytesOfMemoryUsed { get; set; }

        public double MyWalletBalance { get; set; }

        public string CurrentPocketAction { get; set; }

        public float AgentEffectiveStandingtoMe;
        public string AgentEffectiveStandingtoMeText;
        public bool MissionBookmarkTimerSet = false;
        public DateTime MissionBookmarkTimeout = DateTime.MaxValue;

        public long AgentStationID { get; set; }

        public string AgentStationName { get; set; }

        public long AgentSolarSystemID { get; set; }

        public string AgentSolarSystemName { get; set; }

        public string CurrentAgentText = string.Empty;

        public string CurrentAgent
        {
            get
            {
                if (Settings.Instance.CharacterXMLExists)
                {
                    if (_agentName == "")
                    {
                        try
                        {
                            _agentName = SwitchAgent;
                            Logging.Log("Cache.CurrentAgent", "[ " + CurrentAgent + " ] AgentID [ " + AgentId + " ]",
                                        Logging.White);
                            Cache.Instance.CurrentAgentText = CurrentAgent;
                        }
                        catch (Exception ex)
                        {
                            Logging.Log("Cache", "AgentId", "Unable to get agent details: trying again in a moment [" + ex.Message + "]");
                            return "";
                        }
                    }

                    return _agentName;
                }
                return "";
            }
            set
            {
                _agentName = value;
            }
        }

        public string SwitchAgent
        {
            get
            {
                AgentsList agent = Settings.Instance.AgentsList.OrderBy(j => j.Priorit).FirstOrDefault(i => DateTime.Now >= i.DeclineTimer);
                if (agent == null)
                {
                    try
                    {
                        agent = Settings.Instance.AgentsList.OrderBy(j => j.Priorit).FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("Cache", "SwitchAgent", "Unable to process agent section of [" + Settings.Instance.SettingsPath + "] make sure you have a valid agent listed! Pausing so you can fix it. [" + ex.Message + "]");
                        Cache.Instance.Paused = true;
                    }
                    AllAgentsStillInDeclineCoolDown = true; //this literally means we have no agents available at the moment (decline timer likely)
                }
                else
                    AllAgentsStillInDeclineCoolDown = false; //this literally means we DO have agents available (at least one agents decline timer has expired and is clear to use)

                if (agent != null) return agent.Name;
                return null;
            }
        }

        public long AgentId
        {
            get
            {
                if (Settings.Instance.CharacterXMLExists)
                {
                    try
                    {
                        _agent = DirectEve.GetAgentByName(CurrentAgent);
                        _agentId = _agent.AgentId;

                        return (long) _agentId;
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("Cache", "AgentId", "Unable to get agent details: trying again in a moment [" + ex.Message + "]");
                        return -1;
                    }
                }
                return -1;
            }
        }

        public DirectAgent Agent
        {
            get
            {
                if (Settings.Instance.CharacterXMLExists)
                {
                    try
                    {
                        _agent = DirectEve.GetAgentByName(CurrentAgent);
                        if (_agent != null)
                        {
                            _agentId = _agent.AgentId;
                            //Logging.Log("Cache: CurrentAgent", "Processing Agent Info...", Logging.White);
                            Cache.Instance.AgentStationName = Cache.Instance.DirectEve.GetLocationName(Cache.Instance._agent.StationId);
                            Cache.Instance.AgentStationID = Cache.Instance._agent.StationId;
                            Cache.Instance.AgentSolarSystemName = Cache.Instance.DirectEve.GetLocationName(Cache.Instance._agent.SolarSystemId);
                            Cache.Instance.AgentSolarSystemID = Cache.Instance._agent.SolarSystemId;
                            //Logging.Log("Cache: CurrentAgent", "AgentStationName [" + Cache.Instance.AgentStationName + "]", Logging.White);
                            //Logging.Log("Cache: CurrentAgent", "AgentStationID [" + Cache.Instance.AgentStationID + "]", Logging.White);
                            //Logging.Log("Cache: CurrentAgent", "AgentSolarSystemName [" + Cache.Instance.AgentSolarSystemName + "]", Logging.White);
                            //Logging.Log("Cache: CurrentAgent", "AgentSolarSystemID [" + Cache.Instance.AgentSolarSystemID + "]", Logging.White);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("Cache", "Agent", "Unable to process agent section of [" + Settings.Instance.SettingsPath + "] make sure you have a valid agent listed! Pausing so you can fix it. [" + ex.Message + "]");
                        Cache.Instance.Paused = true;
                    }
                    if (_agentId != null) return _agent ?? (_agent = DirectEve.GetAgentById(_agentId.Value));
                }
                return null;
            }
        }

        public IEnumerable<ModuleCache> Modules
        {
            get { return _modules ?? (_modules = DirectEve.Modules.Select(m => new ModuleCache(m)).ToList()); }
        }

        //
        // this CAN and should just list all possible weapon system groupIDs
        //
        public IEnumerable<ModuleCache> Weapons
        {
            get
            {
                if (Cache.Instance.MissionWeaponGroupId != 0)
                    return Modules.Where(m => m.GroupId == Cache.Instance.MissionWeaponGroupId);
                
                return Modules.Where(m => m.GroupId == Settings.Instance.WeaponGroupId); // ||
                //m.GroupId == (int)Group.ProjectileWeapon ||
                //m.GroupId == (int)Group.EnergyWeapon ||
                //m.GroupId == (int)Group.HybridWeapon ||
                //m.GroupId == (int)Group.CruiseMissileLaunchers ||
                //m.GroupId == (int)Group.RocketLaunchers ||
                //m.GroupId == (int)Group.StandardMissileLaunchers ||
                //m.GroupId == (int)Group.TorpedoLaunchers ||
                //m.GroupId == (int)Group.AssaultMissilelaunchers ||
                //m.GroupId == (int)Group.HeavyMissilelaunchers ||
                //m.GroupId == (int)Group.DefenderMissilelaunchers);
            }
        }

        public IEnumerable<EntityCache> Containers
        {
            get
            {
                return _containers ?? (_containers = Entities.Where(e =>
                          e.IsContainer && e.HaveLootRights && (e.GroupId != (int)Group.Wreck || !e.IsWreckEmpty) &&
                          (e.Name != "Abandoned Container")).
                          ToList());
            }
        }

        public IEnumerable<EntityCache> Wrecks
        {
            get { return _containers ?? (_containers = Entities.Where(e => (e.GroupId != (int)Group.Wreck)).ToList()); }
        }

        public IEnumerable<EntityCache> UnlootedContainers
        {
            get
            {
                return _unlootedContainers ?? (_unlootedContainers = Entities.Where(e =>
                          e.IsContainer &&
                          e.HaveLootRights &&
                          (!LootedContainers.Contains(e.Id) || e.GroupId == (int)Group.Wreck)).OrderBy(
                              e => e.Distance).
                              ToList());
            }
        }

        //This needs to include items you can steal from (thus gain aggro)
        public IEnumerable<EntityCache> UnlootedWrecksAndSecureCans
        {
            get
            {
                return _unlootedWrecksAndSecureCans ?? (_unlootedWrecksAndSecureCans = Entities.Where(e =>
                          (e.GroupId == (int)Group.Wreck || e.GroupId == (int)Group.SecureContainer ||
                           e.GroupId == (int)Group.AuditLogSecureContainer ||
                           e.GroupId == (int)Group.FreightContainer) && !e.IsWreckEmpty).OrderBy(e => e.Distance).
                          ToList());
            }
        }

        public IEnumerable<EntityCache> Targets
        {
            get
            {
                if (_targets == null)
                    _targets = Entities.Where(e => e.IsTarget).ToList();

                // Remove the target info (its been targeted)
                foreach (EntityCache target in _targets.Where(t => TargetingIDs.ContainsKey(t.Id)))
                    TargetingIDs.Remove(target.Id);

                return _targets;
            }
        }

        public IEnumerable<EntityCache> Targeting
        {
            get { return _targeting ?? (_targeting = Entities.Where(e => e.IsTargeting).ToList()); }
        }

        public List<long> IDsinInventoryTree
        {
            get
            {
                Logging.Log("Cache.IDsinInventoryTree", "Refreshing IDs from inventory tree, it has been longer than 30 seconds since the last refresh", Logging.Teal);
                return _IDsinInventoryTree ?? (_IDsinInventoryTree = Cache.Instance.PrimaryInventoryWindow.GetIdsFromTree(false));
            }
        }


        public IEnumerable<EntityCache> TargetedBy
        {
            get { return _targetedBy ?? (_targetedBy = Entities.Where(e => e.IsTargetedBy).ToList()); }
        }

        public IEnumerable<EntityCache> Aggressed
        {
            get { return _aggressed ?? (_aggressed = Entities.Where(e => e.IsTargetedBy && e.IsAttacking).ToList()); }
        }

        public IEnumerable<EntityCache> Entities
        {
            get
            {
                if (!InSpace)
                    return new List<EntityCache>();

                return _entities ?? (_entities = DirectEve.Entities.Select(e => new EntityCache(e)).Where(e => e.IsValid).ToList());
            }
        }

        public IEnumerable<EntityCache> EntitiesNotSelf
        {
            get
            {
                if (!InSpace)
                    return new List<EntityCache>();

                return DirectEve.Entities.Select(e => new EntityCache(e)).Where(e => e.IsValid && e.Name != Settings.Instance.CharacterName).ToList();
            }
        }

        public bool InSpace
        {
            get
            {
                try
                {
                    if (DirectEve.Session.IsInSpace && !DirectEve.Session.IsInStation && DirectEve.Session.IsReady && DirectEve.ActiveShip.Entity != null)
                    {
                        Cache.Instance.LastInSpace = DateTime.Now;
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    if (Settings.Instance.DebugExceptions) Logging.Log("Cache.InSpace", "if (DirectEve.Session.IsInSpace && !DirectEve.Session.IsInStation && DirectEve.Session.IsReady && DirectEve.ActiveShip.Entity != null) <---must have failed exception was [" + ex.Message + "]", Logging.Teal);
                    return false;
                }
            }
        }

        public bool InStation
        {
            get
            {
                try
                {
                    if (DirectEve.Session.IsInStation && !DirectEve.Session.IsInSpace && DirectEve.Session.IsReady)
                    {
                        Cache.Instance.LastInStation = DateTime.Now;
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    if (Settings.Instance.DebugExceptions) Logging.Log("Cache.InStation", "if (DirectEve.Session.IsInStation && !DirectEve.Session.IsInSpace && DirectEve.Session.IsReady) <---must have failed exception was [" + ex.Message + "]", Logging.Teal);
                    return false;
                }
            }
        }

        public bool InWarp
        {
            get { return DirectEve.ActiveShip != null && (DirectEve.ActiveShip.Entity != null && DirectEve.ActiveShip.Entity.Mode == 3); }
        }

        public bool IsOrbiting
        {
            get { return DirectEve.ActiveShip.Entity != null && DirectEve.ActiveShip.Entity.Mode == 4; }
        }

        public bool IsApproaching
        {
            get
            {
                //Logging.Log("Cache.IsApproaching: " + DirectEve.ActiveShip.Entity.Mode.ToString(CultureInfo.InvariantCulture));
                return DirectEve.ActiveShip.Entity != null && DirectEve.ActiveShip.Entity.Mode == 1;
            }
        }

        public bool IsApproachingOrOrbiting
        {
            get { return DirectEve.ActiveShip.Entity != null && (DirectEve.ActiveShip.Entity.Mode == 1 || DirectEve.ActiveShip.Entity.Mode == 4); }
        }

        public IEnumerable<EntityCache> ActiveDrones
        {
            get { return _activeDrones ?? (_activeDrones = DirectEve.ActiveDrones.Select(d => new EntityCache(d)).ToList()); }
        }

        public IEnumerable<EntityCache> Stations
        {
            get { return _stations ?? (_stations = Entities.Where(e => e.CategoryId == (int)CategoryID.Station).ToList()); }
        }

        public EntityCache ClosestStation
        {
            get { return Stations.OrderBy(s => s.Distance).FirstOrDefault() ?? Entities.OrderByDescending(s => s.Distance).FirstOrDefault(); }
        }

        public EntityCache StationByName(string stationName)
        {
            EntityCache station = Stations.First(x => x.Name.ToLower() == stationName.ToLower());
            return station;
        }

        public IEnumerable<DirectSolarSystem> SolarSystems
        {
            get
            {
                var solarSystems = DirectEve.SolarSystems.Values.OrderBy(s => s.Name).ToList();
                return solarSystems;
            }
        }

        public IEnumerable<EntityCache> Stargates
        {
            get { return _stargates ?? (_stargates = Entities.Where(e => e.GroupId == (int)Group.Stargate).ToList()); }
        }

        public EntityCache ClosestStargate
        {
            get { return Stargates.OrderBy(s => s.Distance).FirstOrDefault() ?? Entities.OrderByDescending(s => s.Distance).FirstOrDefault(); }
        }

        public EntityCache StargateByName(string locationName)
        {
            {
                return _stargate ?? (_stargate =
                        Cache.Instance.EntitiesByName(locationName).FirstOrDefault(
                            e => e.GroupId == (int)Group.Stargate));
            }
        }

        public IEnumerable<EntityCache> BigObjects
        {
            get
            {
                return _bigObjects ?? (_bigObjects = Entities.Where(e =>
                       e.GroupId == (int)Group.LargeCollidableStructure ||
                       e.GroupId == (int)Group.LargeCollidableObject ||
                       e.GroupId == (int)Group.LargeCollidableShip ||
                       e.CategoryId == (int)CategoryID.Asteroid ||
                       e.GroupId == (int)Group.SpawnContainer &&
                       e.Distance < (double)Distance.DirectionalScannerCloseRange).OrderBy(t => t.Distance).ToList());
            }
        }

        public IEnumerable<EntityCache> AccelerationGates
        {
            get
            {
                return _gates ?? (_gates = Entities.Where(e =>
                       e.GroupId == (int)Group.AccellerationGate &&
                       e.Distance < (double)Distance.OnGridWithMe).OrderBy(t => t.Distance).ToList());
            }
        }

        public IEnumerable<EntityCache> BigObjectsandGates
        {
            get
            {
                return _bigObjectsAndGates ?? (_bigObjectsAndGates = Entities.Where(e =>
                       e.GroupId == (int)Group.LargeCollidableStructure ||
                       e.GroupId == (int)Group.LargeCollidableObject ||
                       e.GroupId == (int)Group.LargeCollidableShip ||
                       e.CategoryId == (int)CategoryID.Asteroid ||
                       e.GroupId == (int)Group.AccellerationGate ||
                       e.GroupId == (int)Group.SpawnContainer &&
                       e.Distance < (double)Distance.DirectionalScannerCloseRange).OrderBy(t => t.Distance).ToList());
            }
        }

        public IEnumerable<EntityCache> Objects
        {
            get
            {
                return _objects ?? (_objects = Entities.Where(e =>
                    //e.CategoryId != (int)CategoryID.Entity && 
                       !e.IsPlayer &&
                       e.GroupId != (int)Group.SpawnContainer &&
                       e.GroupId != (int)Group.Wreck &&
                       //e.GroupId != (int)Group.Stargate &&
                       //e.GroupId != (int)Group.Station &&
                       e.Distance < 200000).OrderBy(t => t.Distance).ToList());
            }
        }

        public EntityCache Star
        {
            get { return _star ?? (_star = Entities.FirstOrDefault(e => e.CategoryId == (int)CategoryID.Celestial && e.GroupId == (int)Group.Star)); }
        }

        public IEnumerable<EntityCache> PriorityTargets
        {
            get
            {
                _priorityTargets.RemoveAll(pt => pt.Entity == null);
                return _priorityTargets.OrderBy(pt => pt.Priority).ThenBy(pt => (pt.Entity.ShieldPct + pt.Entity.ArmorPct + pt.Entity.StructurePct)).ThenBy(pt => pt.Entity.Distance).Select(pt => pt.Entity);
            }
        }

        public EntityCache Approaching
        {
            get
            {
                if (_approaching == null)
                {
                    DirectEntity ship = DirectEve.ActiveShip.Entity;
                    if (ship != null && ship.IsValid)
                        _approaching = EntityById(ship.FollowId);
                }

                return _approaching != null && _approaching.IsValid ? _approaching : null;
            }
            set { _approaching = value; }
        }

        public List<DirectWindow> Windows
        {
            get
            {
                if (Cache.Instance.InSpace || Cache.Instance.InStation)
                {
                    return _windows ?? (_windows = DirectEve.Windows);
                }
                return null;
            }
        }

        /// <summary>
        ///   Returns the mission for a specific agent
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns>null if no mission could be found</returns>
        public DirectAgentMission GetAgentMission(long agentId)
        {
            return DirectEve.AgentMissions.FirstOrDefault(m => m.AgentId == agentId);
        }

        /// <summary>
        ///   Returns the mission objectives from
        /// </summary>
        public List<string> MissionItems { get; private set; }

        /// <summary>
        ///   Returns the item that needs to be brought on the mission
        /// </summary>
        /// <returns></returns>
        public string BringMissionItem { get; private set; }

        public int BringMissionItemQuantity { get; private set; }

        public string BringOptionalMissionItem { get; private set; }

        public int BringOptionalMissionItemQuantity { get; private set; }

        /// <summary>         
        ///   Range for warp to mission bookmark         
        /// </summary>
        public double MissionWarpAtDistanceRange { get; set; } //in km

        public string Fitting { get; set; } // stores name of the final fitting we want to use

        public string MissionShip { get; set; } //stores name of mission specific ship

        public string DefaultFitting { get; set; } //stores name of the default fitting

        public string CurrentFit { get; set; }

        public string FactionFit { get; set; }

        public string FactionName { get; set; }

        public bool ArmLoadedCache { get; set; } // flags whether arm has already loaded the mission

        public bool UseMissionShip { get; set; } // flags whether we're using a mission specific ship

        public bool ChangeMissionShipFittings { get; set; } // used for situations in which missionShip's specified, but no faction or mission fittings are; prevents default

        public List<Ammo> MissionAmmo;

        public int MissionWeaponGroupId { get; set; }

        public bool? MissionUseDrones { get; set; }

        public bool? MissionKillSentries { get; set; }

        public bool StopTimeSpecified = true;

        public DateTime StopTime = DateTime.Now.AddHours(10);

        public DateTime ManualStopTime = DateTime.Now.AddHours(10);

        public DateTime ManualRestartTime = DateTime.Now.AddHours(10);

        public DateTime StartTime { get; set; }

        public int MaxRuntime { get; set; }

        public DateTime LastInStation = DateTime.MinValue;

        public DateTime LastInSpace { get; set; }

        public DateTime LastInWarp = DateTime.Now.AddMinutes(5);

        public bool CloseQuestorCMDLogoff; //false;

        public bool CloseQuestorCMDExitGame = true;

        public bool CloseQuestorEndProcess = false;

        public bool GotoBaseNow; //false;

        public string ReasonToStopQuestor { get; set; }

        public string SessionState { get; set; }

        public double SessionIskGenerated { get; set; }

        public double SessionLootGenerated { get; set; }

        public double SessionLPGenerated { get; set; }

        public int SessionRunningTime { get; set; }

        public double SessionIskPerHrGenerated { get; set; }

        public double SessionLootPerHrGenerated { get; set; }

        public double SessionLPPerHrGenerated { get; set; }

        public double SessionTotalPerHrGenerated { get; set; }

        public bool QuestorJustStarted = true;

        public DateTime EnteredCloseQuestor_DateTime;

        public bool DropMode { get; set; }

        public DirectWindow GetWindowByCaption(string caption)
        {
            return Windows.FirstOrDefault(w => w.Caption.Contains(caption));
        }

        public DirectWindow GetWindowByName(string name)
        {
            // Special cases
            if (name == "Local")
                return Windows.FirstOrDefault(w => w.Name.StartsWith("chatchannel_solarsystemid"));

            return Windows.FirstOrDefault(w => w.Name == name);
        }

        /// <summary>
        ///   Return entities by name
        /// </summary>
        /// <param name = "name"></param>
        /// <returns></returns>
        public IEnumerable<EntityCache> EntitiesByName(string name)
        {
            return Entities.Where(e => e.Name == name).ToList();
        }

        /// <summary>
        ///   Return entity by name
        /// </summary>
        /// <param name = "name"></param>
        /// <returns></returns>
        public EntityCache EntityByName(string name)
        {
            return Entities.FirstOrDefault(e => System.String.Compare(e.Name, name, System.StringComparison.OrdinalIgnoreCase) == 0);
        }

        public IEnumerable<EntityCache> EntitiesByNamePart(string name)
        {
            return Entities.Where(e => e.Name.Contains(name)).ToList();
        }

        /// <summary>
        ///   Return entities that contain the name
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EntityCache> EntitiesThatContainTheName(string label)
        {
            return Entities.Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains(label)).ToList();
        }

        /// <summary>
        ///   Return a cached entity by Id
        /// </summary>
        /// <param name = "id"></param>
        /// <returns></returns>
        public EntityCache EntityById(long id)
        {
            if (_entitiesById.ContainsKey(id))
                return _entitiesById[id];

            EntityCache entity = Entities.FirstOrDefault(e => e.Id == id);
            _entitiesById[id] = entity;
            return entity;
        }

        /// <summary>
        ///   Returns the first mission bookmark that starts with a certain string
        /// </summary>
        /// <returns></returns>
        public DirectAgentMissionBookmark GetMissionBookmark(long agentId, string startsWith)
        {
            // Get the missions
            DirectAgentMission missionForBookmarkInfo = GetAgentMission(agentId);
            if (missionForBookmarkInfo == null)
            {
                Logging.Log("Cache.DirectAgentMissionBookmark", "missionForBookmarkInfo [null] <---bad  parameters passed to us:  agentid [" + agentId + "] startswith [" + startsWith + "]", Logging.White);
                return null;
            }

            // Did we accept this mission?
            if (missionForBookmarkInfo.State != (int)MissionState.Accepted || missionForBookmarkInfo.AgentId != agentId)
            {
                //Logging.Log("missionForBookmarkInfo.State: [" + missionForBookmarkInfo.State.ToString(CultureInfo.InvariantCulture) + "]");
                //Logging.Log("missionForBookmarkInfo.AgentId: [" + missionForBookmarkInfo.AgentId.ToString(CultureInfo.InvariantCulture) + "]");
                //Logging.Log("agentId: [" + agentId.ToString(CultureInfo.InvariantCulture) + "]");
                return null;
            }

            return missionForBookmarkInfo.Bookmarks.FirstOrDefault(b => b.Title.ToLower().StartsWith(startsWith.ToLower()));
        }

        /// <summary>
        ///   Return a bookmark by id
        /// </summary>
        /// <param name = "bookmarkId"></param>
        /// <returns></returns>
        public DirectBookmark BookmarkById(long bookmarkId)
        {
            return DirectEve.Bookmarks.FirstOrDefault(b => b.BookmarkId == bookmarkId);
        }

        /// <summary>
        ///   Returns bookmarks that start with the supplied label
        /// </summary>
        /// <param name = "label"></param>
        /// <returns></returns>
        public List<DirectBookmark> BookmarksByLabel(string label)
        {
            // Does not seems to refresh the Corporate Bookmark list so it's having troubles to find Corporate Bookmarks
            return DirectEve.Bookmarks.Where(b => !string.IsNullOrEmpty(b.Title) && b.Title.StartsWith(label)).OrderBy(f => f.LocationId).ToList();
        }

        /// <summary>
        ///   Returns bookmarks that contain the supplied label anywhere in the title
        /// </summary>
        /// <param name = "label"></param>
        /// <returns></returns>
        public List<DirectBookmark> BookmarksThatContain(string label)
        {
            return DirectEve.Bookmarks.Where(b => !string.IsNullOrEmpty(b.Title) && b.Title.Contains(label)).ToList();
        }

        /// <summary>
        ///   Invalidate the cached items
        /// </summary>
        public void InvalidateCache()
        {
            //
            // this list of variables is cleared every pulse. 
            //
            _activeDrones = null;
            _agent = null;
            _aggressed = null;
            _approaching = null;
            _activeDrones = null;
            _bigObjects = null;
            _bigObjectsAndGates = null;
            _containers = null;
            _entities = null;
            _entitiesById.Clear();
            _gates = null;
            _IDsinInventoryTree = null; 
            _modules = null;
            _objects = null;
            _priorityTargets.ForEach(pt => pt.ClearCache());
            _star = null;
            _stations = null;
            _stargates = null;
            _targets = null;
            _targeting = null;
            _targetedBy = null;
            _unlootedContainers = null;
            _windows = null;
        }

        public string FilterPath(string path)
        {
            if (path == null)
                return string.Empty;

            path = path.Replace("\"", "");
            path = path.Replace("?", "");
            path = path.Replace("\\", "");
            path = path.Replace("/", "");
            path = path.Replace("'", "");
            path = path.Replace("*", "");
            path = path.Replace(":", "");
            path = path.Replace(">", "");
            path = path.Replace("<", "");
            path = path.Replace(".", "");
            path = path.Replace(",", "");
            while (path.IndexOf("  ", System.StringComparison.Ordinal) >= 0)
                path = path.Replace("  ", " ");
            return path.Trim();
        }

        /// <summary>
        ///   Loads mission objectives from XML file
        /// </summary>
        /// <param name = "agentId"> </param>
        /// <param name = "pocketId"> </param>
        /// <param name = "missionMode"> </param>
        /// <returns></returns>
        public IEnumerable<Actions.Action> LoadMissionActions(long agentId, int pocketId, bool missionMode)
        {
            DirectAgentMission missiondetails = GetAgentMission(agentId);
            if (missiondetails == null && missionMode)
                return new Actions.Action[0];

            if (missiondetails != null)
            {
                Cache.Instance.SetmissionXmlPath(FilterPath(missiondetails.Name));
                if (!File.Exists(Cache.Instance.MissionXmlPath))
                {
                    //No mission file but we need to set some cache settings
                    OrbitDistance = Settings.Instance.OrbitDistance;
                    AfterMissionSalvaging = Settings.Instance.AfterMissionSalvaging;
                    return new Actions.Action[0];
                }
                //
                // this loads the settings from each pocket... but NOT any settings global to the mission
                //
                try
                {
                    XDocument xdoc = XDocument.Load(Cache.Instance.MissionXmlPath);
                    if (xdoc.Root != null)
                    {
                        XElement xElement = xdoc.Root.Element("pockets");
                        if (xElement != null)
                        {
                            IEnumerable<XElement> pockets = xElement.Elements("pocket");
                            foreach (XElement pocket in pockets)
                            {
                                if ((int)pocket.Attribute("id") != pocketId)
                                    continue;

                                if (pocket.Element("damagetype") != null)
                                    DamageType = (DamageType)Enum.Parse(typeof(DamageType), (string)pocket.Element("damagetype"), true);

                                if (pocket.Element("orbitdistance") != null) 	//Load OrbitDistance from mission.xml, if present
                                {
                                    OrbitDistance = (int)pocket.Element("orbitdistance");
                                    Logging.Log("Cache", "Using Mission Orbit distance [" + OrbitDistance + "]", Logging.White);
                                }
                                else //Otherwise, use value defined in charname.xml file
                                {
                                    OrbitDistance = Settings.Instance.OrbitDistance;
                                    Logging.Log("Cache", "Using Settings Orbit distance [" + OrbitDistance + "]", Logging.White);
                                }
                                if (pocket.Element("afterMissionSalvaging") != null) 	//Load afterMissionSalvaging setting from mission.xml, if present
                                {
                                    AfterMissionSalvaging = (bool)pocket.Element("afterMissionSalvaging");
                                }
                                if (pocket.Element("dronesKillHighValueTargets") != null) 	//Load afterMissionSalvaging setting from mission.xml, if present
                                {
                                    DronesKillHighValueTargets = (bool)pocket.Element("dronesKillHighValueTargets");
                                }
                                else //Otherwise, use value defined in charname.xml file
                                {
                                    DronesKillHighValueTargets = Settings.Instance.DronesKillHighValueTargets;
                                    //Logging.Log(string.Format("Cache: Using Character Setting DroneKillHighValueTargets  {0}", DronesKillHighValueTargets));
                                }
                                var actions = new List<Actions.Action>();
                                XElement elements = pocket.Element("actions");
                                if (elements != null)
                                {
                                    foreach (XElement element in elements.Elements("action"))
                                    {
                                        var action = new Actions.Action
                                            {
                                                State = (ActionState)Enum.Parse(typeof (ActionState), (string) element.Attribute("name"),true)
                                            };
                                        XAttribute xAttribute = element.Attribute("name");
                                        if (xAttribute != null && xAttribute.Value == "ClearPocket")
                                        {
                                            action.AddParameter("", "");
                                        }
                                        else
                                        {
                                            foreach (XElement parameter in element.Elements("parameter"))
                                                action.AddParameter((string)parameter.Attribute("name"), (string)parameter.Attribute("value"));
                                        }
                                        actions.Add(action);
                                    }
                                }
                                return actions;
                            }
                            //actions.Add(action);
                        }
                        else
                        {
                            return new Actions.Action[0];
                        }
                    }
                    else
                    {
                        { return new Actions.Action[0]; }
                    }

                    // if we reach this code there is no mission XML file, so we set some things -- Assail

                    OrbitDistance = Settings.Instance.OrbitDistance;
                    Logging.Log("Cache", "Using Settings Orbit distance [" + Settings.Instance.OrbitDistance + "]", Logging.White);

                    return new Actions.Action[0];
                }
                catch (Exception ex)
                {
                    Logging.Log("Cache", "Error loading mission XML file [" + ex.Message + "]", Logging.Orange);
                    return new Actions.Action[0];
                }
            }
            return new Actions.Action[0];
        }

        public void SetmissionXmlPath(string missionName)
        {
            if (!string.IsNullOrEmpty(Cache.Instance.FactionName))
            {
                Cache.Instance.MissionXmlPath = System.IO.Path.Combine(Settings.Instance.MissionsPath, FilterPath(missionName) + "-" + Cache.Instance.FactionName + ".xml");
                if (!File.Exists(Cache.Instance.MissionXmlPath))
                {
                    Logging.Log("Cache.SetmissionXmlPath",
                                "Unable to find faction specific [" + Cache.Instance.MissionXmlPath +
                                "] trying generic version", Logging.White);
                    Cache.Instance.MissionXmlPath = System.IO.Path.Combine(Settings.Instance.MissionsPath, FilterPath(missionName) + ".xml");
                }
            }
            else
            {
                Cache.Instance.MissionXmlPath = System.IO.Path.Combine(Settings.Instance.MissionsPath, FilterPath(missionName) + ".xml");
            }

        }
        /// <summary>
        ///   Refresh the mission items
        /// </summary>
        public void RefreshMissionItems(long agentId)
        {
            // Clear out old items
            MissionItems.Clear();
            BringMissionItem = string.Empty;
            BringOptionalMissionItem = string.Empty;

            DirectAgentMission missionDetailsForMissionItems = GetAgentMission(agentId);
            if (missionDetailsForMissionItems == null)
                return;
            if (string.IsNullOrEmpty(FactionName))
                FactionName = "Default";

            if (Settings.Instance.UseFittingManager)
            {
                //Set fitting to default
                DefaultFitting = Settings.Instance.DefaultFitting.Fitting;
                Fitting = DefaultFitting;
                MissionShip = "";
                ChangeMissionShipFittings = false;
                if (Settings.Instance.MissionFitting.Any(m => m.Mission.ToLower() == missionDetailsForMissionItems.Name.ToLower())) //priority goes to mission-specific fittings
                {
                    MissionFitting missionFitting;

                    // if we have got multiple copies of the same mission, find the one with the matching faction
                    if (Settings.Instance.MissionFitting.Any(m => m.Faction.ToLower() == FactionName.ToLower() && (m.Mission.ToLower() == missionDetailsForMissionItems.Name.ToLower())))
                        missionFitting = Settings.Instance.MissionFitting.FirstOrDefault(m => m.Faction.ToLower() == FactionName.ToLower() && (m.Mission.ToLower() == missionDetailsForMissionItems.Name.ToLower()));
                    else //otherwise just use the first copy of that mission
                        missionFitting = Settings.Instance.MissionFitting.FirstOrDefault(m => m.Mission.ToLower() == missionDetailsForMissionItems.Name.ToLower());

                    if (missionFitting != null)
                    {
                        var missionFit = missionFitting.Fitting;
                        var missionShip = missionFitting.Ship;
                        if (!(missionFit == "" && missionShip != "")) // if we've both specified a mission specific ship and a fitting, then apply that fitting to the ship
                        {
                            ChangeMissionShipFittings = true;
                            Fitting = missionFit;
                        }
                        else if (!string.IsNullOrEmpty(FactionFit))
                            Fitting = FactionFit;
                        Logging.Log("Cache", "Mission: " + missionFitting.Mission + " - Faction: " + FactionName + " - Fitting: " + missionFit + " - Ship: " + missionShip + " - ChangeMissionShipFittings: " + ChangeMissionShipFittings, Logging.White);
                        MissionShip = missionShip;
                    }
                }
                else if (!string.IsNullOrEmpty(FactionFit)) // if no mission fittings defined, try to match by faction
                    Fitting = FactionFit;

                if (Fitting == "") // otherwise use the default
                    Fitting = DefaultFitting;
            }

            string missionName = FilterPath(missionDetailsForMissionItems.Name);
            Cache.Instance.MissionXmlPath = System.IO.Path.Combine(Settings.Instance.MissionsPath, missionName + ".xml");
            if (!File.Exists(Cache.Instance.MissionXmlPath))
                return;

            try
            {
                XDocument xdoc = XDocument.Load(Cache.Instance.MissionXmlPath);
                IEnumerable<string> items = ((IEnumerable)xdoc.XPathEvaluate("//action[(translate(@name, 'LOT', 'lot')='loot') or (translate(@name, 'LOTIEM', 'lotiem')='lootitem')]/parameter[translate(@name, 'TIEM', 'tiem')='item']/@value")).Cast<XAttribute>().Select(a => ((string)a ?? string.Empty).ToLower());
                MissionItems.AddRange(items);

                if (xdoc.Root != null) BringMissionItem = (string)xdoc.Root.Element("bring") ?? string.Empty;
                BringMissionItem = BringMissionItem.ToLower();

                if (xdoc.Root != null) BringMissionItemQuantity = (int?)xdoc.Root.Element("bringquantity") ?? 1;
                BringMissionItemQuantity = BringMissionItemQuantity;

                if (xdoc.Root != null) BringOptionalMissionItem = (string)xdoc.Root.Element("trytobring") ?? string.Empty;
                BringOptionalMissionItem = BringOptionalMissionItem.ToLower();

                if (xdoc.Root != null) BringOptionalMissionItemQuantity = (int?)xdoc.Root.Element("trytobringquantity") ?? 1;
                BringOptionalMissionItemQuantity = BringOptionalMissionItemQuantity;

                //load fitting setting from the mission file
                //Fitting = (string)xdoc.Root.Element("fitting") ?? "default";
            }
            catch (Exception ex)
            {
                Logging.Log("Cache", "Error loading mission XML file [" + ex.Message + "]", Logging.Orange);
            }
        }

        /// <summary>
        ///   Remove targets from priority list
        /// </summary>
        /// <param name = "targets"></param>
        public bool RemovePriorityTargets(IEnumerable<EntityCache> targets)
        {
            return _priorityTargets.RemoveAll(pt => targets.Any(t => t.Id == pt.EntityID)) > 0;
        }

        /// <summary>
        ///   Add priority targets
        /// </summary>
        /// <param name = "targets"></param>
        /// <param name = "priority"></param>
        public void AddPriorityTargets(IEnumerable<EntityCache> targets, Priority priority)
        {
            foreach (EntityCache target in targets)
            {
                if (_priorityTargets.Any(pt => pt.EntityID == target.Id))
                    continue;

                _priorityTargets.Add(new PriorityTarget { EntityID = target.Id, Priority = priority });
            }
        }

        /// <summary>
        ///   Calculate distance from me
        /// </summary>
        /// <param name = "x"></param>
        /// <param name = "y"></param>
        /// <param name = "z"></param>
        /// <returns></returns>
        public double DistanceFromMe(double x, double y, double z)
        {
            if (DirectEve.ActiveShip.Entity == null)
                return double.MaxValue;

            double curX = DirectEve.ActiveShip.Entity.X;
            double curY = DirectEve.ActiveShip.Entity.Y;
            double curZ = DirectEve.ActiveShip.Entity.Z;

            return Math.Sqrt((curX - x) * (curX - x) + (curY - y) * (curY - y) + (curZ - z) * (curZ - z));
        }

        /// <summary>
        ///   Calculate distance from entity
        /// </summary>
        /// <param name = "x"></param>
        /// <param name = "y"></param>
        /// <param name = "z"></param>
        /// <param name="entity"> </param>
        /// <returns></returns>
        public double DistanceFromEntity(double x, double y, double z, DirectEntity entity)
        {
            if (entity == null)
                return double.MaxValue;

            double curX = entity.X;
            double curY = entity.Y;
            double curZ = entity.Z;

            return Math.Sqrt((curX - x) * (curX - x) + (curY - y) * (curY - y) + (curZ - z) * (curZ - z));
        }

        /// <summary>
        ///   Create a bookmark
        /// </summary>
        /// <param name = "label"></param>
        public void CreateBookmark(string label)
        {
            if (Cache.Instance.AfterMissionSalvageBookmarks.Count() < 100)
            {
                if (Settings.Instance.CreateSalvageBookmarksIn.ToLower() == "corp".ToLower())
                    DirectEve.CorpBookmarkCurrentLocation(label, "", null);
                else
                    DirectEve.BookmarkCurrentLocation(label, "", null);
            }
            else
            {
                Logging.Log("CreateBookmark", "We already have over 100 AfterMissionSalvage bookmarks: their must be a issue processing or deleting bookmarks. No additional bookmarks will be created until the number of salvage bookmarks drops below 100.", Logging.Orange);
            }
        }

        /// <summary>
        ///   Create a bookmark of the closest wreck
        /// </summary>
        //public void CreateBookmarkofWreck(IEnumerable<EntityCache> containers, string label)
        //{
        //    DirectEve.BookmarkEntity(Cache.Instance.Containers.FirstOrDefault, "a", "a", null);
        //}

        private Func<EntityCache, int> OrderByLowestHealth()
        {
            return t => (int)(t.ShieldPct + t.ArmorPct + t.StructurePct);
        }

        //public List <long> BookMarkToDestination(DirectBookmark bookmark)
        //{
        //    Directdestination = new MissionBookmarkDestination(Cache.Instance.GetMissionBookmark(Cache.Instance.AgentId, "Encounter"));
        //    return List<long> destination;
        //}

        public DirectItem CheckCargoForItem(int typeIdToFind, int quantityToFind)
        {
            DirectContainer cargo = Cache.Instance.DirectEve.GetShipsCargo();
            DirectItem item = cargo.Items.FirstOrDefault(i => i.TypeId == typeIdToFind && i.Quantity >= quantityToFind);
            return item;
        }

        public bool CheckifRouteIsAllHighSec()
        {
            Cache.Instance.RouteIsAllHighSecBool = false;
            // Find the first waypoint
            List<long> currentPath = DirectEve.Navigation.GetDestinationPath();
            if (currentPath == null || !currentPath.Any()) return false;
            if (currentPath[0] == 0) return false; //No destination set - prevents exception if somehow we have got an invalid destination

            for (int i = currentPath.Count - 1; i >= 0; i--)
            {
                DirectSolarSystem solarSystemInRoute = Cache.Instance.DirectEve.SolarSystems[currentPath[i]];
                if (solarSystemInRoute.Security < 0.45)
                {
                    //Bad bad bad
                    Cache.Instance.RouteIsAllHighSecBool = false;
                    return true;
                }
            }
            Cache.Instance.RouteIsAllHighSecBool = true;
            return true;
        }

        /// <summary>
        ///   Return the best possible target (based on current target, distance and low value first)
        /// </summary>
        /// <param name="currentTarget"></param>
        /// <param name="distance"></param>
        /// <param name="lowValueFirst"></param>
        /// <param name="callingroutine"> </param>
        /// <returns></returns>
        public EntityCache GetBestTarget(EntityCache currentTarget, double distance, bool lowValueFirst, string callingroutine)
        {
            // Do we have a 'current target' and if so, is it an actual target?
            // If not, clear current target
            if (currentTarget != null && !currentTarget.IsTarget)
                currentTarget = null;

            // Is our current target a warp scrambling priority target?
            if (currentTarget != null && PriorityTargets.Any(pt => pt.Id == currentTarget.Id && pt.IsWarpScramblingMe && pt.IsTarget))
                return currentTarget;

            // Get the closest warp scrambling priority target
            EntityCache warpscramblingtarget = PriorityTargets.OrderBy(OrderByLowestHealth()).ThenBy(t => t.Distance).FirstOrDefault(pt => pt.Distance < distance && pt.IsWarpScramblingMe && pt.IsTarget);
            if (warpscramblingtarget != null)
                return warpscramblingtarget;

            if (Settings.Instance.SpeedTank) //all webbers have to be relatively close so processing them all is ok
            {
                // Is our current target a webbing priority target?
                if (currentTarget != null && !Cache.Instance.IgnoreTargets.Contains(currentTarget.Name.Trim()) && PriorityTargets.Any(pt => pt.Id == currentTarget.Id && pt.IsWebbingMe && pt.IsTarget))
                    return currentTarget;

                // Get the closest webbing priority target frigate
                EntityCache webbingtarget = PriorityTargets.OrderBy(OrderByLowestHealth()).ThenBy(t => t.Distance).FirstOrDefault(pt => pt.Distance < distance && pt.IsWebbingMe && pt.IsNPCFrigate && pt.IsTarget); //frigates
                if (webbingtarget != null && !Cache.Instance.IgnoreTargets.Contains(webbingtarget.Name.Trim()))
                    return webbingtarget;

                // Get the closest webbing priority target cruiser
                webbingtarget = PriorityTargets.OrderBy(OrderByLowestHealth()).ThenBy(t => t.Distance).FirstOrDefault(pt => pt.Distance < distance && pt.IsWebbingMe && pt.IsNPCCruiser && pt.IsTarget); //cruisers
                if (webbingtarget != null && !Cache.Instance.IgnoreTargets.Contains(webbingtarget.Name.Trim()))
                    return webbingtarget;

                // Get the closest webbing priority target (anything else)
                webbingtarget = PriorityTargets.OrderBy(OrderByLowestHealth()).ThenBy(t => t.Distance).FirstOrDefault(pt => pt.Distance < distance && pt.IsWebbingMe && pt.IsTarget); //everything else
                if (webbingtarget != null && !Cache.Instance.IgnoreTargets.Contains(webbingtarget.Name.Trim()))
                    return webbingtarget;
            }

            // Is our current target any other priority target?
            if (currentTarget != null && !Cache.Instance.IgnoreTargets.Contains(currentTarget.Name.Trim()) && PriorityTargets.Any(pt => pt.Id == currentTarget.Id))
                return currentTarget;

            bool currentTargetHealthLogNow = true;
            if (Settings.Instance.DetailedCurrentTargetHealthLogging)
            {
                if (currentTarget != null && (int)currentTarget.Id != (int)TargetingCache.CurrentTargetID)
                    if ((int)currentTarget.ArmorPct == 0 && (int)currentTarget.ShieldPct == 0 && (int)currentTarget.StructurePct == 0)
                    {
                        //assume that any NPC with no shields, armor or hull is dead or does not yet have valid data associated with it
                    }
                    else
                    {
                        //
                        // assign shields and armor to targetingcache variables - compare them to each other
                        // to see if we need to send another log message to the console, if the values have not changed no need to log it.
                        //
                        if ((int)currentTarget.ShieldPct >= TargetingCache.CurrentTargetShieldPct ||
                            (int)currentTarget.ArmorPct >= TargetingCache.CurrentTargetArmorPct ||
                            (int)currentTarget.StructurePct >= TargetingCache.CurrentTargetStructurePct)
                        {
                            currentTargetHealthLogNow = false;
                        }
                        //
                        // now that we are done comparing - assign new values for this tick
                        //
                        TargetingCache.CurrentTargetShieldPct = (int)currentTarget.ShieldPct;
                        TargetingCache.CurrentTargetArmorPct = (int)currentTarget.ArmorPct;
                        TargetingCache.CurrentTargetStructurePct = (int)currentTarget.StructurePct;
                        if (currentTargetHealthLogNow)
                        {
                            Logging.Log(callingroutine, ".GetBestTarget: CurrentTarget is [" + currentTarget.Name +                              //name
                                        "][" + (Math.Round(currentTarget.Distance / 1000, 0)).ToString(CultureInfo.InvariantCulture) +           //distance
                                        "k][Shield%:[" + Math.Round(currentTarget.ShieldPct * 100, 0).ToString(CultureInfo.InvariantCulture) +   //shields
                                        "][Armor%:[" + Math.Round(currentTarget.ArmorPct * 100, 0).ToString(CultureInfo.InvariantCulture) + "]" //armor
                                        , Logging.White);
                        }
                    }
            }
            // Is our current target already in armor? keep shooting the same target if so...
            if (currentTarget != null && currentTarget.ArmorPct * 100 < 60 && !Cache.Instance.IgnoreTargets.Contains(currentTarget.Name.Trim()))
            {
                //Logging.Log(callingroutine + ".GetBestTarget: CurrentTarget has less than 60% armor, keep killing this target");
                return currentTarget;
            }

            // Get the closest priority target
            EntityCache prioritytarget = PriorityTargets.OrderBy(OrderByLowestHealth()).ThenBy(t => t.Distance).FirstOrDefault(pt => pt.Distance < distance && pt.IsTarget);
            if (prioritytarget != null && !Cache.Instance.IgnoreTargets.Contains(prioritytarget.Name.Trim()))
                return prioritytarget;

            // Do we have a target?
            if (currentTarget != null)
                return currentTarget;

            // Get all entity targets
            IEnumerable<EntityCache> targets = Targets.Where(e => e.CategoryId == (int)CategoryID.Entity && e.IsNpc && !e.IsContainer && e.GroupId != (int)Group.LargeCollidableStructure).ToList();

            EWarEffectsOnMe(); //updates data that is displayed in the Questor GUI (and possibly used elsewhere later)

            // Get the closest high value target
            EntityCache highValueTarget = targets.Where(t => t.TargetValue.HasValue && t.Distance < distance).OrderByDescending(t => t.TargetValue != null ? t.TargetValue.Value : 0).ThenBy(OrderByLowestHealth()).ThenBy(t => t.Distance).FirstOrDefault();
            // Get the closest low value target
            EntityCache lowValueTarget = targets.Where(t => !t.TargetValue.HasValue && t.Distance < distance).OrderBy(OrderByLowestHealth()).ThenBy(t => t.Distance).FirstOrDefault();

            if (lowValueFirst && lowValueTarget != null)
                return lowValueTarget;
            if (!lowValueFirst && highValueTarget != null)
                return highValueTarget;

            // Return either one or the other
            return lowValueTarget ?? highValueTarget;
        }

        private void EWarEffectsOnMe()
        {
            // Get all entity targets
            IEnumerable<EntityCache> targets = Targets.Where(e => e.CategoryId == (int)CategoryID.Entity && e.IsNpc && !e.IsContainer && e.GroupId != (int)Group.LargeCollidableStructure).ToList();

            //
            //Start of Current EWar Effects On Me (below)
            //
            //Dampening
            TargetingCache.EntitiesDampeningMe = targets.Where(e => e.IsSensorDampeningMe).ToList();
            TargetingCache.EntitiesDampeningMeText = String.Empty;
            foreach (EntityCache entityDampeningMe in TargetingCache.EntitiesDampeningMe)
            {
                TargetingCache.EntitiesDampeningMeText = TargetingCache.EntitiesDampeningMeText + " [" +
                                                          entityDampeningMe.Name + "][" +
                                                          Math.Round(entityDampeningMe.Distance / 1000, 0) +
                                                          "k] , ";
            }

            //Neutralizing
            TargetingCache.EntitiesNeutralizingMe = targets.Where(e => e.IsNeutralizingMe).ToList();
            TargetingCache.EntitiesNeutralizingMeText = String.Empty;
            foreach (EntityCache entityNeutralizingMe in TargetingCache.EntitiesNeutralizingMe)
            {
                TargetingCache.EntitiesNeutralizingMeText = TargetingCache.EntitiesNeutralizingMeText + " [" +
                                                             entityNeutralizingMe.Name + "][" +
                                                             Math.Round(entityNeutralizingMe.Distance / 1000, 0) +
                                                             "k] , ";
            }

            //TargetPainting
            TargetingCache.EntitiesTargetPatingingMe = targets.Where(e => e.IsTargetPaintingMe).ToList();
            TargetingCache.EntitiesTargetPaintingMeText = String.Empty;
            foreach (EntityCache entityTargetpaintingMe in TargetingCache.EntitiesTargetPatingingMe)
            {
                TargetingCache.EntitiesTargetPaintingMeText = TargetingCache.EntitiesTargetPaintingMeText + " [" +
                                                               entityTargetpaintingMe.Name + "][" +
                                                               Math.Round(entityTargetpaintingMe.Distance / 1000, 0) +
                                                               "k] , ";
            }

            //TrackingDisrupting
            TargetingCache.EntitiesTrackingDisruptingMe = targets.Where(e => e.IsTrackingDisruptingMe).ToList();
            TargetingCache.EntitiesTrackingDisruptingMeText = String.Empty;
            foreach (EntityCache entityTrackingDisruptingMe in TargetingCache.EntitiesTrackingDisruptingMe)
            {
                TargetingCache.EntitiesTrackingDisruptingMeText = TargetingCache.EntitiesTrackingDisruptingMeText +
                                                                   " [" + entityTrackingDisruptingMe.Name + "][" +
                                                                   Math.Round(entityTrackingDisruptingMe.Distance / 1000, 0) +
                                                                   "k] , ";
            }

            //Jamming (ECM)
            TargetingCache.EntitiesJammingMe = targets.Where(e => e.IsJammingMe).ToList();
            TargetingCache.EntitiesJammingMeText = String.Empty;
            foreach (EntityCache entityJammingMe in TargetingCache.EntitiesJammingMe)
            {
                TargetingCache.EntitiesJammingMeText = TargetingCache.EntitiesJammingMeText + " [" +
                                                        entityJammingMe.Name + "][" +
                                                        Math.Round(entityJammingMe.Distance / 1000, 0) +
                                                        "k] , ";
            }

            //Warp Disrupting (and warp scrambling)
            TargetingCache.EntitiesWarpDisruptingMe = targets.Where(e => e.IsWarpScramblingMe).ToList();
            TargetingCache.EntitiesWarpDisruptingMeText = String.Empty;
            foreach (EntityCache entityWarpDisruptingMe in TargetingCache.EntitiesWarpDisruptingMe)
            {
                TargetingCache.EntitiesWarpDisruptingMeText = TargetingCache.EntitiesWarpDisruptingMeText + " [" +
                                                               entityWarpDisruptingMe.Name + "][" +
                                                               Math.Round(entityWarpDisruptingMe.Distance / 1000, 0) +
                                                               "k] , ";
            }

            //Webbing
            TargetingCache.EntitiesWebbingMe = targets.Where(e => e.IsWebbingMe).ToList();
            TargetingCache.EntitiesWebbingMeText = String.Empty;
            foreach (EntityCache entityWebbingMe in TargetingCache.EntitiesWebbingMe)
            {
                TargetingCache.EntitiesWebbingMeText = TargetingCache.EntitiesWebbingMeText + " [" +
                                                        entityWebbingMe.Name + "][" +
                                                        Math.Round(entityWebbingMe.Distance / 1000, 0) +
                                                        "k] , ";
            }
            //
            //End of Current EWar Effects On Me (above)
            //
        }

        public int RandomNumber(int min, int max)
        {
            var random = new Random();
            return random.Next(min, max);
        }

        public bool DebugInventoryWindows(string module)
        {
            List<DirectWindow> windows = Cache.Instance.Windows;

            Logging.Log(module, "DebugInventoryWindows: *** Start Listing Inventory Windows ***", Logging.White);
            int windownumber = 0;
            foreach (DirectWindow window in windows)
            {
                if (window.Type.ToLower().Contains("inventory"))
                {
                    windownumber++;
                    Logging.Log(module, "----------------------------  #[" + windownumber + "]", Logging.White);
                    Logging.Log(module, "DebugInventoryWindows.Name:    [" + window.Name + "]", Logging.White);
                    Logging.Log(module, "DebugInventoryWindows.Type:    [" + window.Type + "]", Logging.White);
                    Logging.Log(module, "DebugInventoryWindows.Caption: [" + window.Caption + "]", Logging.White);
                }
            }
            Logging.Log(module, "DebugInventoryWindows: ***  End Listing Inventory Windows  ***", Logging.White);
            return true;
        }


        public DirectContainer ItemHangar { get; set; }

        public bool OpenItemsHangarSingleInstance(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;
            if (Cache.Instance.InStation)
            {
                DirectContainerWindow lootHangarWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.Inventory" && w.Caption.Contains("Item hangar"));
                // Is the items hangar open?
                if (lootHangarWindow == null)
                {
                    // No, command it to open
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                    Cache.Instance.NextOpenHangarAction =
                        DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                    Logging.Log(module, "Opening Item Hangar: waiting [" +
                                        Math.Round(
                                            Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                        "sec]", Logging.White);
                    return false;
                }
                
                Cache.Instance.ItemHangar = Cache.Instance.DirectEve.GetContainer(lootHangarWindow.currInvIdItem);
                return true;
            }
            return false;
        }

        public bool OpenItemsHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;
            if (Cache.Instance.InStation)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "We are in Station", Logging.Teal);
                Cache.Instance.ItemHangar = Cache.Instance.DirectEve.GetItemHangar();

                if (Cache.Instance.ItemHangar == null)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar was null", Logging.Teal);
                    return false;
                }
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar exists", Logging.Teal);

                // Is the items hangar open?
                if (Cache.Instance.ItemHangar.Window == null)
                {
                    // No, command it to open
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(1, 2));
                    return false;
                }

                if (!Cache.Instance.ItemHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar.window is not yet ready", Logging.Teal);
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(2, 3));
                    Logging.Log(module, "Opening Item Hangar: waiting [" +
                                Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                "sec]", Logging.White);
                    return false;
                }

                if (Cache.Instance.ItemHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar.window ready", Logging.Teal);
                    if (Cache.Instance.ItemHangar.Window.IsPrimary())
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar.window is primary, opening as secondary", Logging.Teal);
                        Cache.Instance.ItemHangar.Window.OpenAsSecondary();
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public bool CloseItemsHangar (String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "We are in Station", Logging.Teal);
                Cache.Instance.ItemHangar = Cache.Instance.DirectEve.GetItemHangar();

                if (Cache.Instance.ItemHangar == null)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar was null", Logging.Teal);
                    return false;
                }
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar exists", Logging.Teal);

                // Is the items hangar open?
                if (Cache.Instance.ItemHangar.Window == null)
                {
                    Logging.Log(module, "Item Hangar: is closed", Logging.White);
                    return true;
                }

                if (!Cache.Instance.ItemHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangar", "ItemsHangar.window is not yet ready", Logging.Teal);
                    return false;
                }
                if (Cache.Instance.ItemHangar.Window.IsReady)
                {
                    Cache.Instance.ItemHangar.Window.Close();
                    return false;
                }
            }
            return false;
        }

        public bool OpenItemsHangarAsLootHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;
            if (Cache.Instance.InStation)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsLootHangar", "We are in Station", Logging.Teal);
                Cache.Instance.LootHangar = Cache.Instance.DirectEve.GetItemHangar();

                if (Cache.Instance.LootHangar == null)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsLootHangar", "LootHangar was null", Logging.Teal);
                    return false;
                }
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsLootHangar", "LootHangar exists", Logging.Teal);

                // Is the items hangar open?
                if (Cache.Instance.LootHangar.Window == null)
                {
                    // No, command it to open
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 4));
                    Logging.Log(module, "Opening Item Hangar: waiting [" +
                            Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                            "sec]", Logging.White);
                    return false;
                }
                if (!Cache.Instance.LootHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsLootHangar", "LootHangar.window is not yet ready", Logging.Teal);
                    return false;
                }
                if (Cache.Instance.LootHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsLootHangar", "LootHangar.window ready", Logging.Teal);
                    if (Cache.Instance.LootHangar.Window.IsPrimary())
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsLootHangar", "LootHangar.window is primary, opening as secondary", Logging.Teal);
                        Cache.Instance.LootHangar.Window.OpenAsSecondary();
                        return false;
                    }
                    if (Cache.Instance.LootHangar.Window.Type.Contains("form.InventorySecondary"))
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log("Questor", "LootHangar.Window is a secondary inventory window", Logging.Teal);
                        return true;
                    }

                    if (Settings.Instance.DebugHangars)
                    {
                        Logging.Log("Questor", "-----LootHangar.Window-----", Logging.Orange);
                        Logging.Log("Questor", "Cache.Instance.LootHangar.Window.Name: [" + Cache.Instance.LootHangar.Window.Name + "]", Logging.White);
                        Logging.Log("Questor", "Cache.Instance.LootHangar.Window.Caption: [" + Cache.Instance.LootHangar.Window.Caption + "]", Logging.White);
                        Logging.Log("Questor", "Cache.Instance.LootHangar.Window.Type: [" + Cache.Instance.LootHangar.Window.Type + "]", Logging.White);
                        Logging.Log("Questor", "Cache.Instance.LootHangar.Window.IsModal: [" + Cache.Instance.LootHangar.Window.IsModal + "]", Logging.White);
                        Logging.Log("Questor", "Cache.Instance.LootHangar.Window.IsDialog: [" + Cache.Instance.LootHangar.Window.IsDialog + "]", Logging.White);
                        Logging.Log("Questor", "Cache.Instance.LootHangar.Window.Id: [" + Cache.Instance.LootHangar.Window.Id + "]", Logging.White);
                        Logging.Log("Questor", "Cache.Instance.LootHangar.Window.IsKillable: [" + Cache.Instance.LootHangar.Window.IsKillable + "]", Logging.White);
                    }
                    return false;
                }
            }
            return false;
        }

        public bool OpenItemsHangarAsAmmoHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsAmmoHangar", "We are in Station", Logging.Teal);
                Cache.Instance.AmmoHangar = Cache.Instance.DirectEve.GetItemHangar();

                if (Cache.Instance.AmmoHangar == null)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsAmmoHangar", "AmmoHangar was null", Logging.Teal);
                    return false;
                }
                if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsAmmoHangar", "AmmoHangar exists", Logging.Teal);

                // Is the items hangar open?
                if (Cache.Instance.AmmoHangar.Window == null)
                {
                    // No, command it to open
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 4));
                    Logging.Log(module, "Opening Item Hangar: waiting [" +
                                Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                "sec]", Logging.White);
                    return false;
                }
                if (!Cache.Instance.AmmoHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsAmmoHangar", "AmmoHangar.window is not yet ready", Logging.Teal);
                    return false;
                }
                if (Cache.Instance.AmmoHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsAmmoHangar", "AmmoHangar.window ready", Logging.Teal);
                    if (Cache.Instance.AmmoHangar.Window.IsPrimary())
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log("OpenItemsHangarAsAmmoHangar", "AmmoHangar.window is primary, opening as secondary", Logging.Teal);
                        Cache.Instance.AmmoHangar.Window.OpenAsSecondary();
                        return false;
                    }
                    return true;
                }
                return false;
            }
            return false;
        }

        public bool StackItemsHangarAsLootHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;
            if (Cache.Instance.InStation)
            {
                if (!Cache.Instance.OpenItemsHangarAsLootHangar("Cache.StackItemsHangar")) return false;
                if (Settings.Instance.DebugHangars) Logging.Log("StackItemsHangarAsLootHangar", "OpenItemsHangarAsLootHangar returned true", Logging.Teal);
                if (Cache.Instance.LootHangar != null && Cache.Instance.LootHangar.Window.IsReady)
                {
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                    Logging.Log(module, "Stacking Item Hangar: waiting [" +
                                Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                "sec]", Logging.White);
                    Cache.Instance.LootHangar.StackAll();
                    return true;
                }
                return false;
            }
            return false;
        }

        public bool StackItemsHangarAsAmmoHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;
            if (Cache.Instance.InStation)
            {
                if (!Cache.Instance.OpenItemsHangarAsAmmoHangar("Cache.StackItemsHangar")) return false;
                if (Cache.Instance.AmmoHangar != null && Cache.Instance.AmmoHangar.IsValid)
                {
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                    Logging.Log(module, "Stacking Item Hangar: waiting [" +
                            Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                            "sec]", Logging.White);
                    Cache.Instance.AmmoHangar.StackAll();
                    return true;
                }
                return false;
            }
            return false;
        }

        public DirectContainer CargoHold { get; set; }

        public bool OpenCargoHold(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenCargoAction)
            {
                if (DateTime.Now.Subtract(Cache.Instance.NextOpenCargoAction).TotalSeconds > 0)
                {
                    Logging.Log(module, "Opening CargoHold: waiting [" +
                                Math.Round(Cache.Instance.NextOpenCargoAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                "sec]", Logging.White);
                }
                return false;
            }

            Cache.Instance.CargoHold = Cache.Instance.DirectEve.GetShipsCargo();
            if (Cache.Instance.InStation || Cache.Instance.InSpace) //do we need to special case pods here?
            {
                if (Cache.Instance.CargoHold.Window == null)
                {
                    // No, command it to open
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenCargoHoldOfActiveShip);
                    Cache.Instance.NextOpenCargoAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
                    Logging.Log(module, "Opening Cargohold of active ship: waiting [" +
                                Math.Round(Cache.Instance.NextOpenCargoAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                "sec]", Logging.White);
                    return false;
                }

                if (!Cache.Instance.CargoHold.Window.IsReady)
                {
                    //Logging.Log(module, "cargo window is not ready", Logging.White);
                    return false;
                }

                if (!Cache.Instance.CargoHold.Window.IsPrimary())
                {
                    if (Settings.Instance.DebugHangars) Logging.Log(module, "DebugHangars: cargo window is ready and is a secondary inventory window", Logging.DebugHangars);
                    return true;
                }

                if (Cache.Instance.CargoHold.Window.IsPrimary())
                {
                    if (Settings.Instance.DebugHangars) Logging.Log(module, "DebugHangars:Opening cargo window as secondary", Logging.DebugHangars);
                    Cache.Instance.CargoHold.Window.OpenAsSecondary();
                    //Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenCargoHoldOfActiveShip);
                    Cache.Instance.NextOpenCargoAction = DateTime.Now.AddMilliseconds(1000 + Cache.Instance.RandomNumber(0, 2000));
                    return false;
                }
                return true;
            }
            return false;
        }

        public bool StackCargoHold(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenCargoAction)
                return false;

            Logging.Log(module, "Stacking CargoHold: waiting [" +
                        Math.Round(Cache.Instance.NextOpenCargoAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                        "sec]", Logging.White);
            if (Cache.Instance.CargoHold != null && Cache.Instance.CargoHold.IsReady)
            {
                Cache.Instance.CargoHold.StackAll();
                return true;
            }
            return false;
        }

        public bool CloseCargoHold(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenCargoAction)
            {
                if (DateTime.Now.Subtract(Cache.Instance.NextOpenCargoAction).TotalSeconds > 0)
                {
                    Logging.Log(module, "Opening CargoHold: waiting [" +
                                Math.Round(Cache.Instance.NextOpenCargoAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                "sec]", Logging.White);
                }
                return false;
            }

            Cache.Instance.CargoHold = Cache.Instance.DirectEve.GetShipsCargo();
            if (Cache.Instance.InStation || Cache.Instance.InSpace) //do we need to special case pods here?
            {
                if (Cache.Instance.CargoHold.Window == null)
                {
                    Logging.Log(module, "Cargohold is closed", Logging.White);
                    return false;
                }

                if (!Cache.Instance.CargoHold.Window.IsReady)
                {
                    //Logging.Log(module, "cargo window is not ready", Logging.White);
                    return false;
                }

                if (Cache.Instance.CargoHold.Window.IsReady)
                {
                    Cache.Instance.CargoHold.Window.Close();
                    Cache.Instance.NextOpenCargoAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(1, 2));
                    return true;
                }
                return true;
            }
            return false;
        }

        public DirectContainer ShipHangar { get; set; }

        public bool ReadyShipsHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                return false;
            }

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
            {
                return false;
            }

            if (Cache.Instance.InStation)
            {
                Cache.Instance.ShipHangar = Cache.Instance.DirectEve.GetShipHangar();
                if (Cache.Instance.ShipHangar == null)
                {
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddMilliseconds(500);
                    return false;
                }

                //if (Cache.Instance.PrimaryInventoryWindow == null)
                //{
                //    Cache.Instance.OpenInventoryWindow("ReadyShipsHangar");
                //}

                // Is the ShipHangar ready to be used?
                if (Cache.Instance.ShipHangar != null && Cache.Instance.ShipHangar.IsValid)
                {
                    //Logging.Log("ReadyShipHangar","Ship Hangar is ready to be used (no window needed)",Logging.White);
                    return true;
                }
            }
            return false;
        }

        public bool StackShipsHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (Cache.Instance.ShipHangar != null && Cache.Instance.ShipHangar.IsValid)
                {
                    Logging.Log(module, "Stacking Ship Hangar: waiting [" + Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]", Logging.White);
                    Cache.Instance.ShipHangar.StackAll();
                    Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                    return true;
                }
                Logging.Log(module, "Stacking Ship Hangar: not yet ready: waiting [" + Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]", Logging.White);
                return false;
            }
            return false;
        }

        public bool CloseShipsHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("OpenShipsHangar", "We are in Station", Logging.Teal);
                Cache.Instance.ShipHangar = Cache.Instance.DirectEve.GetShipHangar();

                if (Cache.Instance.ShipHangar == null)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenShipsHangar", "ShipsHangar was null", Logging.Teal);
                    return false;
                }
                if (Settings.Instance.DebugHangars) Logging.Log("OpenShipsHangar", "ShipsHangar exists", Logging.Teal);

                // Is the items hangar open?
                if (Cache.Instance.ShipHangar.Window == null)
                {
                    Logging.Log(module, "Ship Hangar: is closed", Logging.White);
                    return true;
                }

                if (!Cache.Instance.ShipHangar.Window.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenShipsHangar", "ShipsHangar.window is not yet ready", Logging.Teal);
                    return false;
                }

                if (Cache.Instance.ShipHangar.Window.IsReady)
                {
                    Cache.Instance.ShipHangar.Window.Close();
                    return false;
                }
            }
            return false;
        }

        //public DirectContainer CorpAmmoHangar { get; set; }

        public bool GetCorpAmmoHangarID()
        {
            if (Cache.Instance.InStation && DateTime.Now > LastSessionChange.AddSeconds(10))
            {
                string CorpHangarName;
                if (Settings.Instance.AmmoHangar != null)
                {
                    CorpHangarName = Settings.Instance.AmmoHangar;
                    if (Settings.Instance.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "CorpHangarName we are looking for is [" + CorpHangarName + "][ AmmoHangarID was: " + Cache.Instance.AmmoHangarID + "]", Logging.White);
                }
                else
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar not configured: Questor will default to item hangar", Logging.White);
                    return true;
                }

                if (CorpHangarName != string.Empty) //&& Cache.Instance.AmmoHangarID == -99)
                {
                    Cache.Instance.AmmoHangar = null;
                    Cache.Instance.AmmoHangar = Cache.Instance.DirectEve.GetCorporationHangar(CorpHangarName);
                    if (Cache.Instance.AmmoHangar.IsValid)
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar contains [" + Cache.Instance.AmmoHangar.Items.Count() + "] Items", Logging.White);
                        //if (Settings.Instance.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar Description [" + Cache.Instance.AmmoHangar.Description + "]", Logging.White);
                        //if (Settings.Instance.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar UsedCapacity [" + Cache.Instance.AmmoHangar.UsedCapacity + "]", Logging.White);
                        //if (Settings.Instance.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangar Volume [" + Cache.Instance.AmmoHangar.Volume + "]", Logging.White);
                    }

                    Cache.Instance.AmmoHangarID = -99;
                    Cache.Instance.AmmoHangarID = Cache.Instance.DirectEve.GetCorpHangarId(Settings.Instance.AmmoHangar) - 1;
                    if (Settings.Instance.DebugHangars) Logging.Log("GetCorpAmmoHangarID", "AmmoHangarID is [" + Cache.Instance.AmmoHangarID + "]", Logging.Teal);
                    return true;
                }
                return true;
            }
            return false;
        }

        public bool GetCorpLootHangarID()
        {
            if (Cache.Instance.InStation && DateTime.Now > LastSessionChange.AddSeconds(10))
            {
                string CorpHangarName;
                if (Settings.Instance.LootHangar != null)
                {
                    CorpHangarName = Settings.Instance.LootHangar;
                    if (Settings.Instance.DebugHangars) Logging.Log("GetCorpLootHangarID", "CorpHangarName we are looking for is [" + CorpHangarName + "][ LootHangarID was: " + Cache.Instance.LootHangarID + "]", Logging.White);
                }
                else
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar not configured: Questor will default to item hangar", Logging.White);
                    return true;
                }

                if (CorpHangarName != string.Empty) //&& Cache.Instance.LootHangarID == -99)
                {
                    Cache.Instance.LootHangar = null;
                    Cache.Instance.LootHangar = Cache.Instance.DirectEve.GetCorporationHangar(CorpHangarName);
                    if (Cache.Instance.LootHangar.IsValid)
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar contains [" + Cache.Instance.LootHangar.Items.Count() + "] Items", Logging.White);
                        //if (Settings.Instance.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar Description [" + Cache.Instance.LootHangar.Description + "]", Logging.White);
                        //if (Settings.Instance.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar UsedCapacity [" + Cache.Instance.LootHangar.UsedCapacity + "]", Logging.White);
                        //if (Settings.Instance.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangar Volume [" + Cache.Instance.LootHangar.Volume + "]", Logging.White);
                    }

                    Cache.Instance.LootHangarID = -99;
                    Cache.Instance.LootHangarID = Cache.Instance.DirectEve.GetCorpHangarId(Settings.Instance.LootHangar) - 1;
                    if (Settings.Instance.DebugHangars) Logging.Log("GetCorpLootHangarID", "LootHangarID is [" + Cache.Instance.LootHangarID + "]", Logging.Teal);
                    return true;
                }
                return true;
            }
            return false;
        }
        
        public bool ReadyCorpAmmoHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar)) //do we have the corp hangar setting setup?
                {
                    if (!Cache.Instance.CloseLootHangar("OpenCorpAmmoHangar")) return false;
                    if (!Cache.Instance.GetCorpAmmoHangarID()) return false;
                    
                    if (Cache.Instance.AmmoHangar != null && Cache.Instance.AmmoHangar.IsValid) //do we have a corp hangar tab setup with that name?
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log(module,"AmmoHangar is defined (no window needed)",Logging.DebugHangars);
                        return true;
                    }

                    if (Cache.Instance.AmmoHangar == null)
                    {
                        if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar))
                            Logging.Log(module, "Opening Corporate Ammo Hangar: failed! No Corporate Hangar in this station! lag?", Logging.Orange);
                        return false;
                    }
                    if (Settings.Instance.DebugHangars) Logging.Log(module, "LootHangar is not yet ready. waiting...", Logging.DebugHangars);
                    return false;
                }
                
                Cache.Instance.AmmoHangar = null;
                return true;
            }
            return false;
        }

        public bool StackCorpAmmoHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar))
                {
                    if (!Cache.Instance.ReadyCorpAmmoHangar("Cache.StackCorpAmmoHangar")) return false;

                    if (AmmoHangar != null && AmmoHangar.IsValid)
                    {
                        Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                        Logging.Log(module, "Stacking Corporate Ammo Hangar: waiting [" +
                                Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds,
                                           0) + "sec]", Logging.White);
                        Cache.Instance.AmmoHangar.StackAll();
                        return true;
                    }
                    return false;
                }
                
                Cache.Instance.AmmoHangar = null;
                return true;
            }
            return false;
        }

        //public DirectContainer CorpLootHangar { get; set; }
        public DirectContainerWindow PrimaryInventoryWindow { get; set; }
        
        public DirectContainerWindow corpAmmoHangarSecondaryWindow { get; set; }
        
        public DirectContainerWindow corpLootHangarSecondaryWindow { get; set; }                

        public bool OpenInventoryWindow(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            Cache.Instance.PrimaryInventoryWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.Inventory" && w.Name == "('Inventory', None)");

            if (Cache.Instance.PrimaryInventoryWindow == null)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("debug", "Cache.Instance.InventoryWindow is null, opening InventoryWindow", Logging.Teal);
                // No, command it to open
                Cache.Instance.DirectEve.OpenInventory();
                Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(2, 3));
                Logging.Log(module, "Opening Inventory Window: waiting [" + Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]", Logging.White);
                return false;
            }

            if (Cache.Instance.PrimaryInventoryWindow != null)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("debug", "Cache.Instance.InventoryWindow exists", Logging.Teal);
                if (Cache.Instance.PrimaryInventoryWindow.IsReady)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("debug", "Cache.Instance.InventoryWindow exists and is ready", Logging.Teal);
                    return true;
                }
                //
                // if the InventoryWindow "hangs" and is never ready we will hang... it would be better if we set a timer
                // and closed the inventorywindow that is not ready after 10-20seconds. (can we close a window that is in a state if !window.isready?)
                //
                return false;
            }
            return false;
        }

        public bool ReadyCorpLootHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.LootHangar)) //do we have the corp hangar setting setup?
                {
                    if (!Cache.Instance.CloseAmmoHangar("OpenCorpLootHangar")) return false;
                    if (!Cache.Instance.GetCorpLootHangarID()) return false;

                    if (Cache.Instance.LootHangar != null && Cache.Instance.LootHangar.IsValid) //do we have a corp hangar tab setup with that name?
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log(module, "AmmoHangar is defined (no window needed)", Logging.DebugHangars);
                        return true;
                    }

                    if (Cache.Instance.LootHangar == null)
                    {
                        if (!string.IsNullOrEmpty(Settings.Instance.LootHangar))
                            Logging.Log(module, "Opening Corporate Loot Hangar: failed! No Corporate Hangar in this station! lag?", Logging.Orange);
                        return false;
                    }
                    if (Settings.Instance.DebugHangars) Logging.Log(module, "AmmoHangar is not yet ready. waiting...", Logging.DebugHangars);
                    return false;
                }
                
                Cache.Instance.LootHangar = null;
                return true;
            }
            return false;
        }

        public bool StackCorpLootHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.LootHangar))
                {
                    if (!Cache.Instance.ReadyCorpLootHangar("Cache.StackCorpLootHangar")) return false;

                    if (LootHangar != null && LootHangar.IsValid)
                    {
                        Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                        Logging.Log(module, "Stacking Corporate Loot Hangar: waiting [" +
                                    Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds,
                                               0) + "sec]", Logging.White);
                        Cache.Instance.LootHangar.StackAll();
                        return true;
                    }
                    return false;
                }
                
                Cache.Instance.LootHangar = null;
                return true;
            }
            return false;
        }

        public DirectContainer CorpBookmarkHangar { get; set; }

        public bool OpenCorpBookmarkHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenCorpBookmarkHangarAction)
                return false;
            if (Cache.Instance.InStation)
            {
                Cache.Instance.CorpBookmarkHangar = !string.IsNullOrEmpty(Settings.Instance.BookmarkHangar)
                                      ? Cache.Instance.DirectEve.GetCorporationHangar(Settings.Instance.BookmarkHangar)
                                      : null;
                // Is the corpHangar open?
                if (Cache.Instance.CorpBookmarkHangar != null)
                {
                    if (Cache.Instance.CorpBookmarkHangar.Window == null)
                    {
                        // No, command it to open
                        //Cache.Instance.DirectEve.OpenCorporationHangar();
                        Cache.Instance.NextOpenCorpBookmarkHangarAction =
                            DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
                        Logging.Log(module, "Opening Corporate Bookmark Hangar: waiting [" +
                                    Math.Round(
                                        Cache.Instance.NextOpenCorpBookmarkHangarAction.Subtract(DateTime.Now).TotalSeconds,
                                        0) + "sec]", Logging.White);
                        return false;
                    }
                    if (!Cache.Instance.CorpBookmarkHangar.Window.IsReady)
                        return false;
                    if (Cache.Instance.CorpBookmarkHangar.Window.IsReady)
                    {
                        if (Cache.Instance.CorpBookmarkHangar.Window.IsPrimary())
                        {
                            Cache.Instance.CorpBookmarkHangar.Window.OpenAsSecondary();
                            return false;
                        }
                        return true;
                    }
                }
                if (Cache.Instance.CorpBookmarkHangar == null)
                {
                    if (!string.IsNullOrEmpty(Settings.Instance.BookmarkHangar))
                        Logging.Log(module, "Opening Corporate Bookmark Hangar: failed! No Corporate Hangar in this station! lag?", Logging.Orange);
                    return false;
                }
            }
            return false;
        }

        public bool CloseCorpHangar(String module, String window)
        {
            if (Cache.Instance.InStation && !String.IsNullOrEmpty(window))
            {
                DirectContainerWindow corpHangarWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.InventorySecondary" && w.Caption == window);

                if (corpHangarWindow != null)
                {
                    Logging.Log(module, "Closing Corp Window: " + window, Logging.Teal);
                    corpHangarWindow.Close();
                    return false;
                }
                return true;
            }
            return true;
        }

        public bool ClosePrimaryInventoryWindow(String module)
        {
            if (DateTime.Now < NextOpenHangarAction)
                return false;

            //
            // go through *every* window
            //
            foreach (DirectWindow window in Cache.Instance.Windows)
            {
                if (window.Type.Equals("form.Inventory"))
                {
                    if (Settings.Instance.DebugHangars) Logging.Log(module, "ClosePrimaryInventoryWindow: Closing Primary Inventory Window Named [" + window.Name + "]", Logging.White);
                    window.Close();
                    NextOpenHangarAction = DateTime.Now.AddMilliseconds(500);
                    return false;
                }     
            }
            return true;
        }

        //public DirectContainer LootContainer { get; set; }

        public bool ReadyLootContainer(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenLootContainerAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.LootContainer))
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenLootContainer", "Debug: if (!string.IsNullOrEmpty(Settings.Instance.LootContainer))", Logging.Teal);
                    if (!Cache.Instance.OpenItemsHangar(module)) return false;

                    DirectItem firstLootContainer = Cache.Instance.ItemHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && i.GroupId == (int)Group.FreightContainer && i.GivenName.ToLower() == Settings.Instance.LootContainer.ToLower());
                    if (firstLootContainer != null)
                    {
                        long lootContainerID = firstLootContainer.ItemId;
                        Cache.Instance.LootHangar = Cache.Instance.DirectEve.GetContainer(lootContainerID);

                        if (Cache.Instance.LootHangar != null && Cache.Instance.LootHangar.IsValid)
                        {
                            if (Settings.Instance.DebugHangars) Logging.Log(module, "LootHangar is defined (no window needed)", Logging.DebugHangars);
                            return true;
                        }

                        if (Cache.Instance.LootHangar == null)
                        {
                            if (!string.IsNullOrEmpty(Settings.Instance.LootHangar))
                                Logging.Log(module, "Opening Corporate Loot Hangar: failed! No Corporate Hangar in this station! lag?", Logging.Orange);
                            return false;
                        }

                        if (Settings.Instance.DebugHangars) Logging.Log(module, "AmmoHangar is not yet ready. waiting...", Logging.DebugHangars);
                        return false;
                    }
                    
                    Logging.Log(module, "unable to find LootContainer named [ " + Settings.Instance.LootContainer.ToLower() + " ]", Logging.Orange);
                    var firstOtherContainer = Cache.Instance.ItemHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && i.GroupId == (int)Group.FreightContainer);
                        
                    if (firstOtherContainer != null)
                    {
                        Logging.Log(module, "we did however find a container named [ " + firstOtherContainer.GivenName + " ]", Logging.Orange);
                        return false;
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        public bool OpenAndSelectInvItem(string module, long id)
        {
            if (DateTime.Now < Cache.Instance.LastSessionChange.AddSeconds(10))
            {
                if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace)", Logging.Teal);
                return false;
            }

            if (DateTime.Now < NextOpenHangarAction)
            {
                if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (DateTime.Now < NextOpenHangarAction)", Logging.Teal);
                return false;
            }

            if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: about to: if (!Cache.Instance.OpenInventoryWindow", Logging.Teal);

            if (!Cache.Instance.OpenInventoryWindow(module)) return false;

            Cache.Instance.PrimaryInventoryWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.Inventory" && w.Name == "('Inventory', None)");

            if (Cache.Instance.PrimaryInventoryWindow != null && Cache.Instance.PrimaryInventoryWindow.IsReady)
            {
                if (id < 0)
                {
                    //
                    // this also kicks in if we have no corp hangar at all in station... can we detect that some other way?
                    //
                    Logging.Log("OpenAndSelectInvItem", "Inventory item ID from tree cannot be less than 0, retrying", Logging.White);
                    return false;
                }

                List<long> idsInInvTreeView = Cache.Instance.PrimaryInventoryWindow.GetIdsFromTree(false);
                if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: IDs Found in the Inv Tree [" + idsInInvTreeView.Count() + "]", Logging.Teal);

                foreach (Int64 itemInTree in idsInInvTreeView)
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: itemInTree [" + itemInTree + "][looking for: " + id, Logging.Teal);
                    if (itemInTree == id)
                    {
                        if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: Found a match! itemInTree [" + itemInTree + "] = id [" + id + "]", Logging.Teal);
                        if (Cache.Instance.PrimaryInventoryWindow.currInvIdItem != id)
                        {
                            if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: We do not have the right ID selected yet, select it now.", Logging.Teal);
                            Cache.Instance.PrimaryInventoryWindow.SelectTreeEntryByID(id);
                            Cache.Instance.NextOpenCargoAction = DateTime.Now.AddMilliseconds(Cache.Instance.RandomNumber(2000, 4400));
                            return false;
                        }
                        if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: We already have the right ID selected.", Logging.Teal);
                        return true;
                    }
                    continue;
                }

                if (!idsInInvTreeView.Contains(id))
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("OpenAndSelectInvItem", "Debug: if (!Cache.Instance.InventoryWindow.GetIdsFromTree(false).Contains(ID))", Logging.Teal);

                    if (id >= 0 && id <= 6 && Cache.Instance.PrimaryInventoryWindow.ExpandCorpHangarView())
                    {
                        Logging.Log(module, "ExpandCorpHangar executed", Logging.Teal);
                        Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(4);
                        return false;
                    }

                    foreach (Int64 itemInTree in idsInInvTreeView)
                    {
                        Logging.Log(module, "ID: " + itemInTree, Logging.Red);
                    }
                    Logging.Log(module, "Was looking for: " + id, Logging.Red);
                    return false;
                }
                return false;
            }
            return false;
            
        }

        public bool StackLootContainer(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenLootContainerAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!Cache.Instance.ReadyLootContainer("Cache.StackLootContainer")) return false;
                Cache.Instance.NextOpenLootContainerAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(3, 5));
                if (LootHangar.Window == null)
                {
                    var firstLootContainer = Cache.Instance.ItemHangar.Items.FirstOrDefault(i => i.GivenName != null && i.IsSingleton && i.GroupId == (int)Group.FreightContainer && i.GivenName.ToLower() == Settings.Instance.LootContainer.ToLower());
                    if (firstLootContainer != null)
                    {
                        long lootContainerID = firstLootContainer.ItemId;
                        if (!OpenAndSelectInvItem(module, lootContainerID))
                            return false;
                    }
                    else return false;
                }
                if (LootHangar.Window == null || !LootHangar.Window.IsReady) return false;

                Logging.Log(module, "Loot Container window named: [ " + LootHangar.Window.Name + " ] was found and its contents are being stacked", Logging.White);
                LootHangar.StackAll();
                Cache.Instance.NextOpenLootContainerAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
                return true;
            }
            return false;
        }

        public bool CloseLootContainer(String module)
        {
            if (!string.IsNullOrEmpty(Settings.Instance.LootContainer))
            {
                if (Settings.Instance.DebugHangars) Logging.Log("CloseCorpLootHangar", "Debug: else if (!string.IsNullOrEmpty(Settings.Instance.LootContainer))", Logging.Teal);
                DirectContainerWindow lootHangarWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.InventorySecondary" && w.Caption == Settings.Instance.LootContainer);
                    
                if (lootHangarWindow != null)
                {
                    lootHangarWindow.Close();
                    return false;
                }
                return true;
            }
            return true;
        }

        public DirectContainer LootHangar { get; set; }

        public bool CloseLootHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.LootHangar))
                {
                    Cache.Instance.LootHangar = Cache.Instance.DirectEve.GetCorporationHangar(Settings.Instance.LootHangar);

                    // Is the corp loot Hangar open?
                    if (Cache.Instance.LootHangar != null)
                    {
                        Cache.Instance.corpLootHangarSecondaryWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.InventorySecondary" && w.Caption == Settings.Instance.LootHangar);
                        if (Settings.Instance.DebugHangars) Logging.Log("CloseCorpLootHangar", "Debug: if (Cache.Instance.LootHangar != null)", Logging.Teal);

                        if (Cache.Instance.corpLootHangarSecondaryWindow != null)
                        {
                            // if open command it to close
                            Cache.Instance.corpLootHangarSecondaryWindow.Close();
                            Cache.Instance.NextOpenHangarAction =
                                DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
                            Logging.Log(module, "Closing Corporate Loot Hangar: waiting [" +
                                        Math.Round(
                                            Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).
                                                TotalSeconds,
                                            0) + "sec]", Logging.White);
                            return false;
                        }
                        return true;
                    }

                    if (Cache.Instance.LootHangar == null)
                    {
                        if (!string.IsNullOrEmpty(Settings.Instance.LootHangar))
                            Logging.Log(module, "Closing Corporate Hangar: failed! No Corporate Hangar in this station! lag or setting misconfiguration?", Logging.Orange);
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(Settings.Instance.LootContainer))
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("CloseCorpLootHangar", "Debug: else if (!string.IsNullOrEmpty(Settings.Instance.LootContainer))", Logging.Teal);
                    DirectContainerWindow lootHangarWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.InventorySecondary" && w.Caption == Settings.Instance.LootContainer);
                    
                    if (lootHangarWindow != null)
                    {
                        lootHangarWindow.Close();
                        return false;
                    }
                    return true;
                }
                else //use local items hangar
                {
                    Cache.Instance.LootHangar = Cache.Instance.DirectEve.GetItemHangar();
                    if (Cache.Instance.LootHangar == null)
                        return false;

                    // Is the items hangar open?
                    if (Cache.Instance.LootHangar.Window != null)
                    {
                        // if open command it to close
                        Cache.Instance.LootHangar.Window.Close();
                        Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 4));
                        Logging.Log(module, "Closing Item Hangar: waiting [" +
                                    Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                    "sec]", Logging.White);
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public bool OpenLootHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.LootHangar)) // Corporate hangar = LootHangar
                {
                    if (!Cache.Instance.ReadyCorpLootHangar(module)) return false;
                    return true;
                }

                if (!string.IsNullOrEmpty(Settings.Instance.LootContainer)) // Freight Container in my local items hangar = LootHangar
                {
                    if (!Cache.Instance.OpenItemsHangarAsLootHangar(module)) return false;
                    if (!Cache.Instance.ReadyLootContainer(module)) return false;
                    return true;
                }

                if (!Cache.Instance.OpenItemsHangarAsLootHangar(module)) return false;
                return true;
            }
            return false;
        }

        public bool StackLootHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.LootHangar))
                {
                    if (!Cache.Instance.StackCorpLootHangar("Cache.StackLootHangar")) return false;
                    return true;
                }

                if (!string.IsNullOrEmpty(Settings.Instance.LootContainer))
                {
                    if (!Cache.Instance.StackLootContainer("Cache.StackLootHangar")) return false;
                    return true;
                }

                if (!Cache.Instance.StackItemsHangarAsLootHangar("Cache.StackLootHangar")) return false;
                return true;
            }
            return false;
        }

        public DirectContainer AmmoHangar { get; set; }

        public bool ReadyAmmoHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar))
                {
                    if (Settings.Instance.DebugHangars) Logging.Log(module, "using Corporate hangar as Ammo hangar", Logging.White);
                    if (!Cache.Instance.ReadyCorpAmmoHangar(module)) return false;
                }
                else
                {
                    if (Settings.Instance.DebugHangars) Logging.Log(module, "using Local items hangar as Ammo hangar", Logging.White);
                    if (!Cache.Instance.OpenItemsHangarAsAmmoHangar(module)) return false;
                }
                return true;
            }
            return false;
        }

        public bool StackAmmoHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar))
                {
                    if (!Cache.Instance.StackCorpAmmoHangar("Cache.StackCorpAmmoHangar")) return false;
                    return true;
                }

                if (!Cache.Instance.StackItemsHangarAsAmmoHangar("Cache.StackAmmoHangar")) return false;
                return true;
            }
            return false;
        }

        public bool CloseAmmoHangar(String module)
        {
            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
                return false;

            if (Cache.Instance.InStation)
            {
                if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar))
                {
                    if (Settings.Instance.DebugHangars) Logging.Log("CloseCorpAmmoHangar", "Debug: if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar))", Logging.Teal);

                    Cache.Instance.AmmoHangar = Cache.Instance.DirectEve.GetCorporationHangar(Settings.Instance.AmmoHangar);

                    // Is the corp Ammo Hangar open?
                    if (Cache.Instance.AmmoHangar != null)
                    {
                        Cache.Instance.corpAmmoHangarSecondaryWindow = (DirectContainerWindow)Cache.Instance.DirectEve.Windows.FirstOrDefault(w => w.Type == "form.InventorySecondary" && w.Caption == Settings.Instance.AmmoHangar);
                        if (Settings.Instance.DebugHangars) Logging.Log("CloseCorpAmmoHangar", "Debug: if (Cache.Instance.AmmoHangar != null)", Logging.Teal);

                        if (Cache.Instance.corpAmmoHangarSecondaryWindow != null)
                        {
                            if (Settings.Instance.DebugHangars) Logging.Log("CloseCorpAmmoHangar", "Debug: if (ammoHangarWindow != null)", Logging.Teal);

                            // if open command it to close
                            Cache.Instance.corpAmmoHangarSecondaryWindow.Close();
                            Cache.Instance.NextOpenHangarAction =
                                DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
                            Logging.Log(module, "Closing Corporate Ammo Hangar: waiting [" +
                                        Math.Round(
                                            Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).
                                                TotalSeconds,
                                            0) + "sec]", Logging.White);
                            return false;
                        }
                        return true;
                    }

                    if (Cache.Instance.AmmoHangar == null)
                    {
                        if (!string.IsNullOrEmpty(Settings.Instance.AmmoHangar))
                            Logging.Log(module, "Closing Corporate Hangar: failed! No Corporate Hangar in this station! lag or setting misconfiguration?", Logging.Orange);
                        return false;
                    }
                }
                else //use local items hangar
                {
                    Cache.Instance.AmmoHangar = Cache.Instance.DirectEve.GetItemHangar();
                    if (Cache.Instance.AmmoHangar == null)
                        return false;

                    // Is the items hangar open?
                    if (Cache.Instance.AmmoHangar.Window != null)
                    {
                        // if open command it to close
                        Cache.Instance.AmmoHangar.Window.Close();
                        Cache.Instance.NextOpenHangarAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 4));
                        Logging.Log(module, "Closing Item Hangar: waiting [" +
                                    Math.Round(Cache.Instance.NextOpenHangarAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                    "sec]", Logging.White);
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public DirectContainer DroneBay { get; set; }

        //{
        //    get { return _dronebay ?? (_dronebay = Cache.Instance.DirectEve.GetShipsDroneBay()); }
        //}

        public bool ReadyDroneBay(String module)
        {
            if (DateTime.Now < Cache.Instance.NextDroneBayAction)
            {
                //Logging.Log(module + ": Opening Drone Bay: waiting [" + Math.Round(Cache.Instance.NextOpenDroneBayAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]",Logging.White);
                return false;
            }

            if ((!Cache.Instance.InSpace && !Cache.Instance.InStation))
            {
                Logging.Log(module, "Opening Drone Bay: We are not in station or space?!", Logging.Orange);
                return false;
            }

            //if(Cache.Instance.DirectEve.ActiveShip.Entity == null || Cache.Instance.DirectEve.ActiveShip.GroupId == 31)
            //{
            //    Logging.Log(module + ": Opening Drone Bay: we are in a shuttle or not in a ship at all!");
            //    return false;
            //}

            if (Cache.Instance.InStation || Cache.Instance.InSpace)
            {
                Cache.Instance.DroneBay = Cache.Instance.DirectEve.GetShipsDroneBay();
            }
            else return false;

            if (GetShipsDroneBayAttempts > 10) //we her havent located a dronebay in over 10 attempts, we are not going to 
            {
                if (Settings.Instance.DebugHangars) Logging.Log(module, "unable to find a dronebay after 11 attempts: continuing without defining one", Logging.DebugHangars);
                return true;
            }

            if (Cache.Instance.DroneBay == null)
            {
                Cache.Instance.NextDroneBayAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
                Logging.Log(module, "Opening Drone Bay: --- waiting [" +
                                Math.Round(Cache.Instance.NextDroneBayAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                                "sec]", Logging.White);
                GetShipsDroneBayAttempts++;
                return false;
            }

            if (Cache.Instance.DroneBay != null && Cache.Instance.DroneBay.IsValid)
            {
                Cache.Instance.NextDroneBayAction = DateTime.Now.AddSeconds(1 + Cache.Instance.RandomNumber(1, 2));
                if (Settings.Instance.DebugHangars) Logging.Log(module, "DroneBay is ready. waiting [" + Math.Round(Cache.Instance.NextDroneBayAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]", Logging.White);
                GetShipsDroneBayAttempts = 0;
                return true;
            }
            if (Settings.Instance.DebugHangars) Logging.Log(module, "DroneBay is not ready...", Logging.White);
            return false;
        }

        public bool CloseDroneBay(String module)
        {
            if (DateTime.Now < Cache.Instance.NextDroneBayAction)
            {
                //Logging.Log(module + ": Closing Drone Bay: waiting [" + Math.Round(Cache.Instance.NextOpenDroneBayAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]",Logging.White);
                return false;
            }
            if ((!Cache.Instance.InSpace && !Cache.Instance.InStation))
            {
                Logging.Log(module, "Closing Drone Bay: We are not in station or space?!", Logging.Orange);
                return false;
            }
            if (Cache.Instance.InStation || Cache.Instance.InSpace)
            {
                Cache.Instance.DroneBay = Cache.Instance.DirectEve.GetShipsDroneBay();
            }
            else return false;

            // Is the drone bay open? if so, close it
            if (Cache.Instance.DroneBay.Window != null)
            {
                Cache.Instance.NextDroneBayAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
                Logging.Log(module, "Closing Drone Bay: waiting [" +
                            Math.Round(Cache.Instance.NextDroneBayAction.Subtract(DateTime.Now).TotalSeconds, 0) +
                            "sec]", Logging.White);
                Cache.Instance.DroneBay.Window.Close();
                return true;
            }
            return true;
        }

        public DirectLoyaltyPointStoreWindow LPStore { get; set; }

        public bool OpenLPStore(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
            {
                //Logging.Log(module + ": Opening Drone Bay: waiting [" + Math.Round(Cache.Instance.NextOpenDroneBayAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]",Logging.White);
                return false;
            }
            if (!Cache.Instance.InStation)
            {
                Logging.Log(module, "Opening LP Store: We are not in station?! There is no LP Store in space, waiting...", Logging.Orange);
                return false;
            }
            if (Cache.Instance.InStation)
            {
                Cache.Instance.LPStore = Cache.Instance.DirectEve.Windows.OfType<DirectLoyaltyPointStoreWindow>().FirstOrDefault();
                if (Cache.Instance.LPStore == null)
                {
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenLpstore);
                    Logging.Log(module, "Opening loyalty point store", Logging.White);
                    return false;
                }
                return true;
            }
            return false;
        }

        public bool CloseLPStore(String module)
        {
            if (DateTime.Now < Cache.Instance.NextOpenHangarAction)
            {
                return false;
            }
            if (!Cache.Instance.InStation)
            {
                Logging.Log(module, "Closing LP Store: We are not in station?!", Logging.Orange);
                return false;
            }
            if (Cache.Instance.InStation)
            {
                Cache.Instance.LPStore = Cache.Instance.DirectEve.Windows.OfType<DirectLoyaltyPointStoreWindow>().FirstOrDefault();
                if (Cache.Instance.LPStore != null)
                {
                    Logging.Log(module, "Closing loyalty point store", Logging.White);
                    Cache.Instance.LPStore.Close();
                    return false;
                }
                return true;
            }
            return true; //if we are not in station then the LP Store should have auto closed already.
        }

        public DirectWindow JournalWindow { get; set; }

        public bool OpenJournalWindow(String module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(20) && !Cache.Instance.InSpace) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
                return false;

            if (DateTime.Now < Cache.Instance.NextOpenJournalWindowAction)
                return false;

            if (Cache.Instance.InStation)
            {
                Cache.Instance.JournalWindow = Cache.Instance.GetWindowByName("journal");

                // Is the journal window open?
                if (Cache.Instance.JournalWindow == null)
                {
                    // No, command it to open
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenJournal);
                    Cache.Instance.NextOpenJournalWindowAction = DateTime.Now.AddSeconds(2 + Cache.Instance.RandomNumber(10, 20));
                    Logging.Log(module, "Opening Journal Window: waiting [" +
                                Math.Round(Cache.Instance.NextOpenJournalWindowAction.Subtract(DateTime.Now).TotalSeconds,
                                           0) + "sec]", Logging.White);
                    return false;
                }
                return true; //if JournalWindow is not null then the window must be open.
            }
            return false;
        }

        public DirectContainer ContainerInSpace { get; set; }

        public bool OpenContainerInSpace(String module, EntityCache containerToOpen)
        {
            if (DateTime.Now < Cache.Instance.NextLootAction)
                return false;

            if (Cache.Instance.InSpace && containerToOpen.Distance <= (int)Distance.ScoopRange)
            {
                Cache.Instance.ContainerInSpace = Cache.Instance.DirectEve.GetContainer(containerToOpen.Id);

                if (Cache.Instance.ContainerInSpace != null)
                {
                    if (Cache.Instance.ContainerInSpace.Window == null)
                    {
                        containerToOpen.OpenCargo();
                        Cache.Instance.NextLootAction = DateTime.Now.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);
                        Logging.Log(module, "Opening Container: waiting [" + Math.Round(Cache.Instance.NextLootAction.Subtract(DateTime.Now).TotalSeconds, 0) + " sec]", Logging.White);
                        return false;
                    }

                    if (!Cache.Instance.ContainerInSpace.Window.IsReady)
                    {
                        Logging.Log(module, "Container window is not ready", Logging.White);
                        return false;
                    }

                    if (Cache.Instance.ContainerInSpace.Window.IsPrimary())
                    {
                        Logging.Log(module, "Opening Container window as secondary", Logging.White);
                        Cache.Instance.ContainerInSpace.Window.OpenAsSecondary();
                        Cache.Instance.NextLootAction = DateTime.Now.AddMilliseconds(Time.Instance.LootingDelay_milliseconds);
                        return true;
                    }
                }
                return true;
            }
            Logging.Log(module, "Not in space or not in scoop range", Logging.Orange);
            return true;
        }

        public List<DirectBookmark> AfterMissionSalvageBookmarks
        {
            get
            {
                if (_States.CurrentQuestorState == QuestorState.DedicatedBookmarkSalvagerBehavior)
                {
                    return Cache.Instance.BookmarksByLabel(Settings.Instance.BookmarkPrefix + " ").Where(e => e.CreatedOn != null && e.CreatedOn.Value.CompareTo(AgedDate) < 0).ToList();
                }
                
                return Cache.Instance.BookmarksByLabel(Settings.Instance.BookmarkPrefix + " ").ToList();
            }
        }

        //Represents date when bookmarks are eligible for salvage. This should not be confused with when the bookmarks are too old to salvage.
        public DateTime AgedDate
        {
            get
            {
                return DateTime.UtcNow.AddMinutes(-Settings.Instance.AgeofBookmarksForSalvageBehavior);
            }
        }

        public DirectBookmark GetSalvagingBookmark
        {
            get
            {
                //Delete bookmarks older than 2 hours.
                DateTime bmExpirationDate = DateTime.UtcNow.AddMinutes(-Settings.Instance.AgeofSalvageBookmarksToExpire);
                List<DirectBookmark> listOldBktoDelete = Cache.Instance.BookmarksByLabel(Settings.Instance.BookmarkPrefix + " ").Where(e => e.CreatedOn != null && e.CreatedOn.Value.CompareTo(bmExpirationDate) < 0).ToList();
                foreach (DirectBookmark oldBktoDelete in listOldBktoDelete)
                {
                    Logging.Log("CombatMissionsBehavior.BeginAftermissionSalvaging", "Remove old Bookmark: " + oldBktoDelete.Title + " BookmarExpirationDate: " + bmExpirationDate, Logging.Teal);
                    oldBktoDelete.Delete();
                }

                if (Settings.Instance.FirstSalvageBookmarksInSystem)
                {
                    Logging.Log("CombatMissionsBehavior.BeginAftermissionSalvaging", "Salvaging at first bookmark from system", Logging.White);
                    return Cache.Instance.BookmarksByLabel(Settings.Instance.BookmarkPrefix + " ").OrderBy(b => b.CreatedOn).FirstOrDefault(c => c.LocationId == Cache.Instance.DirectEve.Session.SolarSystemId);
                }
                
                Logging.Log("CombatMissionsBehavior.BeginAftermissionSalvaging", "Salvaging at first oldest bookmarks", Logging.White);
                return Cache.Instance.BookmarksByLabel(Settings.Instance.BookmarkPrefix + " ").OrderBy(b => b.CreatedOn).FirstOrDefault();
            }
        }

        public bool GateInGrid()
        {
            if (Cache.Instance.AccelerationGates.FirstOrDefault() == null || !Cache.Instance.AccelerationGates.Any())
                return false;
            return true;
        }

        private int _bookmarkDeletionAttempt;
        public DateTime NextBookmarkDeletionAttempt = DateTime.Now;

        public bool DeleteBookmarksOnGrid(string module)
        {
            if (DateTime.Now < NextBookmarkDeletionAttempt)
            {
                return false;
            }
            NextBookmarkDeletionAttempt = DateTime.Now.AddSeconds(5 + Settings.Instance.RandomNumber(1, 5));

            //
            // remove all salvage bookmarks over 48hrs old - they have long since been rendered useless
            //
            try
            {
                var uselessSalvageBookmarks = new List<DirectBookmark>(AfterMissionSalvageBookmarks.Where(b => b.CreatedOn < DateTime.Now.AddDays(-2)).OrderByDescending(b => b.CreatedOn));

                DirectBookmark uselessSalvageBookmark = uselessSalvageBookmarks.FirstOrDefault();
                if (uselessSalvageBookmark != null)
                {
                    _bookmarkDeletionAttempt++;
                    if (_bookmarkDeletionAttempt <= 5)
                    {
                        Logging.Log(module, "removing salvage bookmark that aged more than 48 hours (is their a dedicated or aftermissions salvager cleaning these up?):" + uselessSalvageBookmark.Title, Logging.White);
                        uselessSalvageBookmark.Delete();
                    }
                    if (_bookmarkDeletionAttempt > 5)
                    {
                        Logging.Log(module, "error removing bookmark!" + uselessSalvageBookmark.Title, Logging.White);
                        _States.CurrentQuestorState = QuestorState.Error;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logging.Log("Cache.DeleteBookmarksOnGrid", "Delete old unprocessed salvage bookmarks: exception generated:" + ex.Message, Logging.White);
            }

            var bookmarksInLocal = new List<DirectBookmark>(AfterMissionSalvageBookmarks.Where(b => b.LocationId == Cache.Instance.DirectEve.Session.SolarSystemId).
                                                                   OrderBy(b => b.CreatedOn));
            DirectBookmark onGridBookmark = bookmarksInLocal.FirstOrDefault(b => Cache.Instance.DistanceFromMe(b.X ?? 0, b.Y ?? 0, b.Z ?? 0) < (int)Distance.OnGridWithMe);
            if (onGridBookmark != null)
            {
                _bookmarkDeletionAttempt++;
                if (_bookmarkDeletionAttempt <= 5)
                {
                    Logging.Log(module, "removing salvage bookmark:" + onGridBookmark.Title, Logging.White);
                    onGridBookmark.Delete();
                }
                if (_bookmarkDeletionAttempt > 5)
                {
                    Logging.Log(module, "error removing bookmark!" + onGridBookmark.Title, Logging.White);
                    _States.CurrentQuestorState = QuestorState.Error;
                }
                return false;
            }
            
            _bookmarkDeletionAttempt = 0;
            Cache.Instance.NextSalvageTrip = DateTime.Now;
            Statistics.Instance.FinishedSalvaging = DateTime.Now;
            _States.CurrentDedicatedBookmarkSalvagerBehaviorState = DedicatedBookmarkSalvagerBehaviorState.CheckBookmarkAge;
            return true;
        }

        public bool RepairItems(string module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(5) && !Cache.Instance.InSpace || DateTime.Now < NextRepairItemsAction) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                //Logging.Log(module, "Waiting...", Logging.Orange);
                return false;
            }

            NextRepairItemsAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber(2, 4));

            if (Cache.Instance.InStation && !Cache.Instance.DirectEve.hasRepairFacility())
            {
                Logging.Log(module, "This station does not have repair facilities to use! aborting attempt to use non-existant repair facility.", Logging.Orange);
                return true;
            }

            if (Cache.Instance.InStation)
            {
                DirectRepairShopWindow repairWindow = Cache.Instance.Windows.OfType<DirectRepairShopWindow>().FirstOrDefault();

                DirectWindow repairQuote = Cache.Instance.GetWindowByName("Set Quantity");

                if (doneUsingRepairWindow)
                {
                    doneUsingRepairWindow = false;
                    if (repairWindow != null) repairWindow.Close();
                    return true;
                }

                foreach (DirectWindow window in Cache.Instance.Windows)
                {
                    if (window.Name == "modal")
                    {
                        if (!string.IsNullOrEmpty(window.Html))
                        {
                            if (window.Html.Contains("Repairing these items will cost"))
                            {
                                Logging.Log(module, "Closing Quote for Repairing All with OK", Logging.White);
                                window.AnswerModal("Yes");
                                doneUsingRepairWindow = true;
                                return false;
                            }
                        }
                    }
                }

                if (repairQuote != null && repairQuote.IsModal && repairQuote.IsKillable)
                {
                    if (repairQuote.Html != null) Logging.Log("RepairItems", "Content of modal window (HTML): [" + (repairQuote.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.White);
                    Logging.Log(module, "Closing Quote for Repairing All with OK", Logging.White);
                    repairQuote.AnswerModal("OK");
                    doneUsingRepairWindow = true;
                    return false;
                }

                if (repairWindow == null)
                {
                    Logging.Log(module, "Opening repairshop window", Logging.White);
                    Cache.Instance.DirectEve.OpenRepairShop();
                    NextRepairItemsAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber(1, 3));
                    return false;
                }
                
                if (!Cache.Instance.ReadyShipsHangar(module)) return false;
                if (!Cache.Instance.OpenItemsHangar(module)) return false;
                if (!Cache.Instance.ReadyDroneBay(module)) return false;

                //repair ships in ships hangar
                List<DirectItem> repairAllItems = Cache.Instance.ShipHangar.Items;
                //repair items in items hangar and drone bay of active ship also
                repairAllItems.AddRange(Cache.Instance.ItemHangar.Items);
                repairAllItems.AddRange(Cache.Instance.DroneBay.Items);

                if (repairAllItems.Any())
                {
                    if (String.IsNullOrEmpty(repairWindow.AvgDamage()))
                    {
                        Logging.Log(module, "Add items to repair list", Logging.White);
                        repairWindow.RepairItems(repairAllItems);
                        return false;
                    }
                    
                    Logging.Log(module, "Repairing Items", Logging.White);
                    repairWindow.RepairAll();
                    Cache.Instance.RepairAll = false;
                    NextRepairItemsAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber(1, 2));
                    return false;
                }
                
                Logging.Log(module, "No items available, nothing to repair.", Logging.Orange);
                return true;
            }
            Logging.Log(module, "Not in station.", Logging.Orange);
            return false;
        }

        public bool RepairDrones(string module)
        {
            if (DateTime.Now < Cache.Instance.LastInSpace.AddSeconds(5) && !Cache.Instance.InSpace || DateTime.Now < NextRepairDronesAction) // we wait 20 seconds after we last thought we were in space before trying to do anything in station
            {
                //Logging.Log(module, "Waiting...", Logging.Orange);
                return false;
            }

            NextRepairDronesAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber(2, 4));

            if (Cache.Instance.InStation && !Cache.Instance.DirectEve.hasRepairFacility())
            {
                Logging.Log(module, "This station does not have repair facilities to use! aborting attempt to use non-existant repair facility.", Logging.Orange);
                return true;
            }

            if (Cache.Instance.InStation)
            {
                DirectRepairShopWindow repairWindow = Cache.Instance.Windows.OfType<DirectRepairShopWindow>().FirstOrDefault();

                DirectWindow repairQuote = Cache.Instance.GetWindowByName("Set Quantity");

                if (GetShipsDroneBayAttempts > 10 && Cache.Instance.DroneBay == null)
                {
                    Logging.Log(module,"Your current ship does not have a drone bay, aborting repair of drones",Logging.Teal);
                    return true;
                }

                if (doneUsingRepairWindow)
                {
                    Logging.Log(module, "Done with RepairShop: closing", Logging.White);
                    doneUsingRepairWindow = false;
                    if (repairWindow != null) repairWindow.Close();
                    return true;
                }

                if (repairQuote != null && repairQuote.IsModal && repairQuote.IsKillable)
                {
                    if (repairQuote.Html != null) Logging.Log("RepairDrones", "Content of modal window (HTML): [" + (repairQuote.Html).Replace("\n", "").Replace("\r", "") + "]", Logging.White);
                    Logging.Log(module, "Closing Quote for Repairing Drones with OK", Logging.White);
                    repairQuote.AnswerModal("OK");
                    doneUsingRepairWindow = true;
                    return false;
                }

                if (repairWindow == null)
                {
                    Logging.Log(module, "Opening repairshop window", Logging.White);
                    Cache.Instance.DirectEve.OpenRepairShop();
                    NextRepairDronesAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber(1, 3));
                    return false;
                }

                if (Cache.Instance.DroneBay == null)
                {
                    if (!Cache.Instance.ReadyDroneBay(module)) return false;
                }

                List<DirectItem> dronesToRepair = Cache.Instance.DroneBay.Items;
                
                if (dronesToRepair.Any())
                {
                    if (String.IsNullOrEmpty(repairWindow.AvgDamage()))
                    {
                        Logging.Log(module, "Get Quote for Repairing [" + dronesToRepair.Count() + "] Drones", Logging.White);
                        repairWindow.RepairItems(dronesToRepair);
                        return false;
                    }
                    Logging.Log(module, "Repairing Drones", Logging.White);
                    repairWindow.RepairAll();
                    NextRepairDronesAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber(1, 2));
                    return false;
                }
                
                Logging.Log(module, "No drones available, nothing to repair.", Logging.Orange);
                return true;
            }
            Logging.Log(module, "Not in station.", Logging.Orange);
            return false;
        }
    }
}