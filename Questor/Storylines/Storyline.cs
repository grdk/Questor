
namespace Questor.Storylines
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules.Actions;
    using global::Questor.Modules.Activities;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.Combat;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.States;

    public class Storyline
    {
        private IStoryline _storyline;
        private readonly Dictionary<string, IStoryline> _storylines;
        //public List<long> AgentBlacklist;

        private readonly Combat _combat;
        private readonly Traveler _traveler;
        private readonly AgentInteraction _agentInteraction;

        private DateTime _nextAction = DateTime.Now;
        private DateTime _nextStoryLineAttempt = DateTime.Now;
        private int _highSecCounter;
        private bool _highSecChecked;
        private bool _setDestinationStation;

        public Storyline()
        {
            _combat = new Combat();
            _traveler = new Traveler();
            _agentInteraction = new AgentInteraction();

            Cache.Instance.AgentBlacklist = new List<long>();

            _storylines = new Dictionary<string, IStoryline>
                            {
                               // Examples
                               //{"StorylineCombatNameHere", new GenericCombatStoryline()},
                               //{"StorylineCourierNameHere", new GenericCourier()},

                               /* COURIER/DELIVERY - ALL FACTIONS */
                               {"Materials For War Preparation", new MaterialsForWarPreparation()},
                               {"Transaction Data Delivery", new TransactionDataDelivery()},
                               //{"A Special Delivery", new GenericCourier()}, // Needs 40k m3 cargo capacity (i.e. Iteron Mark V, T2 CHO rigs)
                               /* COURIER/DELIVERY - AMARR */
                               {"Opiate of the Masses", new GenericCourier()},
                               {"Send the Marines!", new GenericCourier()},
                               {"The Governors Ball", new GenericCourier()},
                               {"The State of the Empire", new GenericCourier()},
                               {"Unmasking the Traitor", new GenericCourier()},
                               /* COURIER/DELIVERY - CALDARI */
                               {"A Desperate Rescue", new GenericCourier()},
                               {"Black Ops Crisis", new GenericCourier()},
                               {"Fire and Ice", new GenericCourier()},
                               {"Hunting Black Dog", new GenericCourier()},
                               {"Operation Doorstop", new GenericCourier()},
                               /* COURIER/DELIVERY - GALLENTE */
                               ////{"A Fathers Love", new GenericCourier()},
                               ////{"A Fine Wine", new GenericCourier()}
                               ////{"A Greener World", new GenericCourier()},
                               ////{"Eradication", new GenericCourier()},
                               ////{"Evacuation", new GenericCourier()},
                               ////{"On the Run", new GenericCourier()},
                               ////{"The Natural Way", new GenericCourier()},
                               /* COURIER/DELIVERY - MINMATAR */
                               {"A Cargo With Attitude", new GenericCourier()},
                               {"A Load of Scrap", new GenericCourier()},
                               {"Brand New Harvesters", new GenericCourier()},
                               {"Heart of the Rogue Drone", new GenericCourier()},
                               {"Their Secret Defense", new GenericCourier()},
                               /* COURIER/DELIVERY - MORE THAN ONE RACE */
                               
                               /* COMBAT - ALL FACTIONS */
                               {"Covering Your Tracks", new GenericCombatStoryline()},
                               {"Evolution", new GenericCombatStoryline()},
                               {"Patient Zero", new GenericCombatStoryline()},
                               {"Record Cleaning", new GenericCombatStoryline()},
                               {"Shipyard Theft", new GenericCombatStoryline()},
                               {"Soothe the Salvage Beast", new GenericCombatStoryline()},
                               /* COMBAT - AMARR */
                               {"Blood Farm", new GenericCombatStoryline()},
                               {"Dissidents", new GenericCombatStoryline()},
                               {"Extract the Renegade", new GenericCombatStoryline()},
                               {"Gate to Nowhere", new GenericCombatStoryline()},
                               {"Racetrack Ruckus", new GenericCombatStoryline()},
                               {"Jealous Rivals", new GenericCombatStoryline()},
                               {"The Mouthy Merc", new GenericCombatStoryline()},
                               /* COMBAT - CALDARI */
                               {"Crowd Control", new GenericCombatStoryline()},
                               {"Forgotten Outpost", new GenericCombatStoryline()},
                               //{"Illegal Mining", new GenericCombatStoryline()}, // Extremely high DPS after shooting structures!
                               {"Innocents in the Crossfire", new GenericCombatStoryline()},
                               {"Stem the Flow", new GenericCombatStoryline()},
                               /* COMBAT - GALLENTE */
                               ////{"Kidnappers Strike - Ambush In The Dark (1 of 10)", new GenericCombatStoryline()},
                               ////{"Kidnappers Strike - The Kidnapping (3 of 10)", new GenericCombatStoryline()},
                               ////{"Kidnappers Strike - Incriminating Evidence (5 of 10)", new GenericCombatStoryline()},
                               ////{"Kidnappers Strike - The Secret Meeting (7 of 10)", new GenericCombatStoryline()},
                               ////{"Kidnappers Strike - Defend the Civilian Convoy (8 of 10)", new GenericCombatStoryline()},
                               ////{"Kidnappers Strike - Retrieve the Prisoners (9 of 10)", new GenericCombatStoryline()},
                               ////{"Kidnappers Strike - The Final Battle (10 of 10)", new GenericCombatStoryline()},
                               ////{"Whispers in the Dark - First Contact (1 of 4)", new GenericCombatStoryline()},
                               ////{"Whispers in the Dark - Lay and Pray (2 of 4)", new GenericCombatStoryline()},
                               ////{"Whispers in the Dark - The Outpost (4 of 4)", new GenericCombatStoryline()},
                               /* COMBAT - MINMATAR */
                               {"Amarrian Excavators", new GenericCombatStoryline()},
                               ////{"Diplomatic Incident", new GenericCombatStoryline()},
                               {"Matriarch", new GenericCombatStoryline()},  
                               {"Nine Tenths of the Wormhole", new GenericCombatStoryline()},
                               {"Postmodern Primitives", new GenericCombatStoryline()},
                               {"Quota Season", new GenericCombatStoryline()},
                               {"The Blood of Angry Men", new GenericCombatStoryline()},
                               /* COMBAT - MORE THAN ONE RACE */                                                         
                            };
        }

        public void Reset()
        {
            //Logging.Log("Storyline", "Storyline.Reset", Logging.White);
            _States.CurrentStorylineState = StorylineState.Idle;
            Cache.Instance.CurrentStorylineAgentId = 0;
            _storyline = null;
            _States.CurrentAgentInteractionState = AgentInteractionState.Idle;
            _States.CurrentTravelerState = TravelerState.Idle;
            _traveler.Destination = null;
        }

        private DirectAgentMission StorylineMission
        {
            get
            {
                IEnumerable<DirectAgentMission> missionsInJournal = Cache.Instance.DirectEve.AgentMissions.ToList();
                if (Cache.Instance.CurrentStorylineAgentId != 0)
                    return missionsInJournal.FirstOrDefault(m => m.AgentId == Cache.Instance.CurrentStorylineAgentId);

                missionsInJournal = missionsInJournal.Where(m => !Cache.Instance.AgentBlacklist.Contains(m.AgentId)).ToList();
                missionsInJournal = missionsInJournal.Where(m => m.Important).ToList();
                Logging.Log("Storyline", "Currently have  [" + missionsInJournal.Count() + "] availible storyline missions", Logging.Yellow);
                missionsInJournal = missionsInJournal.Where(m => _storylines.ContainsKey(Cache.Instance.FilterPath(m.Name)));
                Logging.Log("Storyline", "Currently have  [" + missionsInJournal.Count() + "] storyline missions questor knows how to do", Logging.Yellow);
                missionsInJournal = missionsInJournal.Where(m => Settings.Instance.MissionBlacklist.All(b => b.ToLower() != Cache.Instance.FilterPath(m.Name).ToLower())).ToList();
                Logging.Log("Storyline", "Currently have  [" + missionsInJournal.Count() + "] storyline missions questor knows how to do and are not blacklisted", Logging.Yellow);
                //missions = missions.Where(m => !Settings.Instance.MissionGreylist.Any(b => b.ToLower() == Cache.Instance.FilterPath(m.Name).ToLower()));
                return missionsInJournal.FirstOrDefault();
            }
        }

        private void IdleState()
        {
            DirectAgentMission currentStorylineMission = StorylineMission;
            if (currentStorylineMission == null)
            {
                _nextStoryLineAttempt = DateTime.Now.AddMinutes(15);
                _States.CurrentStorylineState = StorylineState.Done;
                Cache.Instance.MissionName = String.Empty;
                return;
            }

            Cache.Instance.CurrentStorylineAgentId = currentStorylineMission.AgentId;
            DirectAgent storylineagent = Cache.Instance.DirectEve.GetAgentById(Cache.Instance.CurrentStorylineAgentId);
            if (storylineagent == null)
            {
                Logging.Log("Storyline", "Unknown agent [" + Cache.Instance.CurrentStorylineAgentId + "]", Logging.Yellow);

                _States.CurrentStorylineState = StorylineState.Done;
                return;
            }

            Logging.Log("Storyline", "Going to do [" + currentStorylineMission.Name + "] for agent [" + storylineagent.Name + "] AgentID[" + Cache.Instance.CurrentStorylineAgentId + "]", Logging.Yellow);
            Cache.Instance.MissionName = currentStorylineMission.Name;

            _highSecChecked = false;
            _States.CurrentStorylineState = StorylineState.Arm;
            _storyline = _storylines[Cache.Instance.FilterPath(currentStorylineMission.Name)];
        }

        private void GotoAgent(StorylineState nextState)
        {
            if (_nextAction > DateTime.Now)
                return;

            DirectAgent storylineagent = Cache.Instance.DirectEve.GetAgentById(Cache.Instance.CurrentStorylineAgentId);
            if (storylineagent == null)
            {
                _States.CurrentStorylineState = StorylineState.Done;
                return;
            }

            var baseDestination = _traveler.Destination as StationDestination;
            if (baseDestination == null || baseDestination.StationId != storylineagent.StationId)
            {
                _traveler.Destination = new StationDestination(storylineagent.SolarSystemId, storylineagent.StationId, Cache.Instance.DirectEve.GetLocationName(storylineagent.StationId));
                return;
            }

            if (!_highSecChecked && storylineagent.SolarSystemId != Cache.Instance.DirectEve.Session.SolarSystemId)
            {
                // if we haven't already done so, set Eve's autopilot
                if (!_setDestinationStation)
                {
                    if (!_traveler.SetStationDestination(storylineagent.StationId))
                    {
                        Logging.Log("Storyline", "GotoAgent: Unable to find route to storyline agent. Skipping.", Logging.Yellow);
                        _States.CurrentStorylineState = StorylineState.Done;
                        return;
                    }
                    _setDestinationStation = true;
                    _nextAction = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(2, 4));
                    return;
                }

                // Make sure we have got a clear path to the agent
                if (!Settings.Instance.LowSecMissionsInShuttles && !Cache.Instance.CheckifRouteIsAllHighSec())
                {
                    if (_highSecCounter < 5)
                    {
                        _highSecCounter++;
                        return;
                    }
                    Logging.Log("Storyline", "GotoAgent: Unable to determine whether route is all highsec or not. Skipping.", Logging.Yellow);
                    _States.CurrentStorylineState = StorylineState.Done;
                    _highSecCounter = 0;
                    return;
                }

                if (!Cache.Instance.RouteIsAllHighSecBool)
                {
                    Logging.Log("Storyline", "GotoAgent: Route to agent is through low-sec systems. Skipping.", Logging.Yellow);
                    _States.CurrentStorylineState = StorylineState.Done;
                    return;
                }
                _highSecChecked = true;
            }

            if (Cache.Instance.PriorityTargets.Any(pt => pt != null && pt.IsValid))
            {
                Logging.Log("Storyline", "GotoAgent: Priority targets found, engaging!", Logging.Yellow);
                _combat.ProcessState();
            }

            _traveler.ProcessState();
            if (_States.CurrentTravelerState == TravelerState.AtDestination)
            {
                _States.CurrentStorylineState = nextState;
                _traveler.Destination = null;
                _setDestinationStation = false;
            }

            if (Settings.Instance.DebugStates)
                Logging.Log("Traveler.State is", _States.CurrentTravelerState.ToString(), Logging.White);
        }

        private void BringSpoilsOfWar()
        {
            if (_nextAction > DateTime.Now)
                return;

            // Open the item hangar (should still be open)
            if (!Cache.Instance.OpenItemsHangar("Storyline")) return;

            // Do we have any implants?
            if (!Cache.Instance.ItemHangar.Items.Any(i => i.GroupId >= 738 && i.GroupId <= 750))
            {
                _States.CurrentStorylineState = StorylineState.Done;
                return;
            }

            // Yes, open the ships cargo
            if (!Cache.Instance.OpenCargoHold("Storyline")) return;

            // If we are not moving items
            if (Cache.Instance.DirectEve.GetLockedItems().Count == 0)
            {
                // Move all the implants to the cargo bay
                foreach (DirectItem item in Cache.Instance.ItemHangar.Items.Where(i => i.GroupId >= 738 && i.GroupId <= 750))
                {
                    if (Cache.Instance.CargoHold.Capacity - Cache.Instance.CargoHold.UsedCapacity - (item.Volume * item.Quantity) < 0)
                    {
                        Logging.Log("Storyline", "We are full, not moving anything else", Logging.Yellow);
                        _States.CurrentStorylineState = StorylineState.Done;
                        return;
                    }

                    Logging.Log("Storyline", "Moving [" + item.TypeName + "][" + item.ItemId + "] to cargo", Logging.Yellow);
                    Cache.Instance.CargoHold.Add(item, item.Quantity);
                }
                _nextAction = DateTime.Now.AddSeconds(10);
            }
            return;
        }

        public void ProcessState()
        {
            switch (_States.CurrentStorylineState)
            {
                case StorylineState.Idle:
                    IdleState();
                    break;

                case StorylineState.Arm:
                    //Logging.Log("Storyline: Arm");
                    _States.CurrentStorylineState = _storyline.Arm(this);
                    break;

                case StorylineState.GotoAgent:
                    //Logging.Log("Storyline: GotoAgent");
                    GotoAgent(StorylineState.PreAcceptMission);
                    break;

                case StorylineState.PreAcceptMission:
                    //Logging.Log("Storyline: PreAcceptMission-!!");
                    _States.CurrentAgentInteractionState = AgentInteractionState.Idle;
                    _States.CurrentStorylineState = _storyline.PreAcceptMission(this);
                    break;

                case StorylineState.DeclineMission:
                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Idle)
                    {
                        Logging.Log("Storyline.AgentInteraction", "Start conversation [Decline Mission]", Logging.Yellow);

                        _States.CurrentAgentInteractionState = AgentInteractionState.StartConversation;
                        AgentInteraction.Purpose = AgentInteractionPurpose.DeclineMission;
                        _agentInteraction.AgentId = Cache.Instance.CurrentStorylineAgentId;

                    }

                    _agentInteraction.ProcessState();

                    if (Settings.Instance.DebugStates)
                        Logging.Log("AgentInteraction.State is ", _States.CurrentAgentInteractionState.ToString(), Logging.White);

                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Done)
                    {
                        _States.CurrentAgentInteractionState = AgentInteractionState.Idle;
                        // If there is no mission anymore then we're done (we declined it)
                        
                    }
                    break;

                case StorylineState.AcceptMission:
                    //Logging.Log("Storyline: AcceptMission!!-");
                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Idle)
                    {
                        Logging.Log("Storyline.AgentInteraction", "Start conversation [Start Mission]", Logging.Yellow);

                        _States.CurrentAgentInteractionState = AgentInteractionState.StartConversation;
                        AgentInteraction.Purpose = AgentInteractionPurpose.StartMission;
                        _agentInteraction.AgentId = Cache.Instance.CurrentStorylineAgentId;
                        _agentInteraction.ForceAccept = true;
                    }

                    _agentInteraction.ProcessState();

                    if (Settings.Instance.DebugStates)
                        Logging.Log("AgentInteraction.State is ", _States.CurrentAgentInteractionState.ToString(), Logging.White);

                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Done)
                    {
                        _States.CurrentAgentInteractionState = AgentInteractionState.Idle;
                        // If there is no mission anymore then we're done (we declined it)
                        _States.CurrentStorylineState = StorylineMission == null ? StorylineState.Done : StorylineState.ExecuteMission;
                    }
                    break;

                case StorylineState.ExecuteMission:
                    _States.CurrentStorylineState = _storyline.ExecuteMission(this);
                    break;

                case StorylineState.ReturnToAgent:
                    GotoAgent(StorylineState.CompleteMission);
                    break;

                case StorylineState.CompleteMission:
                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Idle)
                    {
                        Logging.Log("AgentInteraction", "Start Conversation [Complete Mission]", Logging.Yellow);

                        _States.CurrentAgentInteractionState = AgentInteractionState.StartConversation;
                        AgentInteraction.Purpose = AgentInteractionPurpose.CompleteMission;
                    }

                    _agentInteraction.ProcessState();

                    if (Settings.Instance.DebugStates)
                        Logging.Log("AgentInteraction.State is", _States.CurrentAgentInteractionState.ToString(), Logging.White);

                    if (_States.CurrentAgentInteractionState == AgentInteractionState.Done)
                    {
                        _States.CurrentAgentInteractionState = AgentInteractionState.Idle;
                        _States.CurrentStorylineState = StorylineState.BringSpoilsOfWar;
                    }
                    break;

                case StorylineState.BringSpoilsOfWar:
                    BringSpoilsOfWar();
                    break;

                case StorylineState.BlacklistAgent:
                    Cache.Instance.AgentBlacklist.Add(Cache.Instance.CurrentStorylineAgentId);
                    Logging.Log("Storyline", "BlacklistAgent: The agent that provided us with this storyline mission has been added to the session blacklist", Logging.Orange);
                    Reset();
                    _States.CurrentCombatMissionBehaviorState = CombatMissionsBehaviorState.GotoBase;
                    break;

                case StorylineState.Done:
                    if (DateTime.Now > _nextStoryLineAttempt)
                    {
                        _States.CurrentStorylineState = StorylineState.Idle;
                    }
                    break;
            }
        }

        public bool HasStoryline()
        {
            // Do we have a registered storyline?
            return StorylineMission != null;
        }

        public IStoryline StorylineHandler
        {
            get { return _storyline; }
        }
    }
}