// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

namespace Questor.Modules.Activities
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using DirectEve;
    using global::Questor.Modules.Actions;
    using global::Questor.Modules.Caching;
    using global::Questor.Modules.Combat;
    using global::Questor.Modules.Logging;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.States;

    public class Traveler
    {
        private TravelerDestination _destination;
        private DateTime _nextTravelerAction;
        private DateTime _lastPulse;
        private DateTime _nextGetLocation;
        
        private List<long> _destinationRoute;
        private DirectLocation _location;
        private string _locationName;
        private int _locationErrors;

        public DirectBookmark UndockBookmark { get; set; }

        public TravelerDestination Destination
        {
            get { return _destination; }
            set
            {
                _destination = value;
                _States.CurrentTravelerState = _destination == null ? TravelerState.AtDestination : TravelerState.Idle;
            }
        }

        /// <summary>
        ///   Set destination to a solar system
        /// </summary>
        public bool SetStationDestination(long stationId)
        {
            _location = Cache.Instance.DirectEve.Navigation.GetLocation(stationId);
            Logging.Log("Traveler", "Location = [" + Logging.Yellow + Cache.Instance.DirectEve.Navigation.GetLocationName(stationId) + Logging.Green + "]", Logging.Green);
            if (_location.IsValid)
            {
                _locationErrors = 0;
                Logging.Log("Traveler", "Setting destination to [" + Logging.Yellow + _location.Name + Logging.Green + "]", Logging.Green);
                if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "Setting destination to [" + Logging.Yellow + _location.Name + Logging.Green + "]", Logging.Teal);
                _location.SetDestination();
                return true;
            }
            
            Logging.Log("Traveler", "Error setting solar system destination [" + Logging.Yellow + stationId + Logging.Green + "]", Logging.Green);
            _locationErrors++;
            if (_locationErrors > 100)
            {
                return false;
            }
            return false;
        }

        /// <summary>
        ///   Navigate to a solar system
        /// </summary>
        /// <param name = "solarSystemId"></param>
        private void NagivateToBookmarkSystem(long solarSystemId)
        {
            if (_nextTravelerAction > DateTime.Now)
            {
                //Logging.Log("Traveler: will continue in [ " + Math.Round(_nextTravelerAction.Subtract(DateTime.Now).TotalSeconds, 0) + " ]sec");
                return;
            }

            Cache.Instance.NextTravelerAction = DateTime.Now.AddSeconds(1);
            if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem - Iterating- next iteration should be in no less than [1] second ", Logging.Teal);

            _destinationRoute = Cache.Instance.DirectEve.Navigation.GetDestinationPath();

            if (_destinationRoute.Count == 0 || _destinationRoute.All(d => d != solarSystemId))
            {
                if (Settings.Instance.DebugTraveler) if (_destinationRoute.Count == 0) Logging.Log("Traveler", "We have no destination", Logging.Teal);
                if (Settings.Instance.DebugTraveler) if (_destinationRoute.All(d => d != solarSystemId)) Logging.Log("Traveler", "the destination is not currently set to solarsystemId [" + solarSystemId + "]", Logging.Teal);
                
                // We do not have the destination set
                if (DateTime.Now > _nextGetLocation || _location == null)
                {
                    if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: getting Location of solarSystemId [" + solarSystemId + "]", Logging.Teal);
                    _nextGetLocation = DateTime.Now.AddSeconds(10);
                    _location = Cache.Instance.DirectEve.Navigation.GetLocation(solarSystemId);
                    Cache.Instance.NextTravelerAction = DateTime.Now.AddSeconds(2);
                    return;
                }
                
                if (_location.IsValid)
                {
                    _locationErrors = 0;
                    Logging.Log("Traveler", "Setting destination to [" + Logging.Yellow + _location.Name + Logging.Green + "]", Logging.Green);
                    if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "Setting destination to [" + Logging.Yellow + _location.Name + Logging.Green + "]", Logging.Teal);
                    _location.SetDestination();
                    Cache.Instance.NextTravelerAction = DateTime.Now.AddSeconds(3);
                    return;
                }
                
                Logging.Log("Traveler", "Error setting solar system destination [" + Logging.Yellow + solarSystemId + Logging.Green + "]", Logging.Green);
                _locationErrors++;
                if (_locationErrors > 100)
                {
                    _States.CurrentTravelerState = TravelerState.Error;
                    return;
                }
                return;
            }
            
            _locationErrors = 0;
            if (!Cache.Instance.InSpace)
            {
                if (Cache.Instance.InStation)
                {
                    Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.CmdExitStation);
                    _nextTravelerAction = DateTime.Now.AddSeconds(Time.Instance.TravelerExitStationAmIInSpaceYet_seconds);
                }
                Cache.Instance.NextActivateSupportModules = DateTime.Now.AddSeconds(Cache.Instance.RandomNumber(1, 2));
                // We are not yet in space, wait for it
                return;
            }

            // We are apparently not really in space yet...
            if (Cache.Instance.DirectEve.ActiveShip.Entity == null)
                return;

            if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "Destination is set: processing...", Logging.Teal);
                
            // Find the first waypoint
            long waypoint = _destinationRoute.First();
            if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: getting next waypoints locationname", Logging.Teal);
            _locationName = Cache.Instance.DirectEve.Navigation.GetLocationName(waypoint);
            // Find the stargate associated with it
            IEnumerable<EntityCache> stargates = Cache.Instance.EntitiesByName(_locationName).Where(e => e.GroupId == (int)Group.Stargate).ToList();
            if (!stargates.Any())
            {
                // not found, that cant be true?!?!?!?!
                Logging.Log("Traveler", "Error [" + Logging.Yellow + _locationName + Logging.Green + "] not found, most likely lag waiting [" + Time.Instance.TravelerNoStargatesFoundRetryDelay_seconds + "] seconds.", Logging.Red);
                _nextTravelerAction = DateTime.Now.AddSeconds(Time.Instance.TravelerNoStargatesFoundRetryDelay_seconds);
                return;
            }

            // Warp to, approach or jump the stargate
            EntityCache stargate = stargates.First();
            if (stargate.Distance < (int)Distance.DecloakRange && !Cache.Instance.DirectEve.ActiveShip.Entity.IsCloaked)
            {
                Logging.Log("Traveler", "Jumping to [" + Logging.Yellow + _locationName + Logging.Green + "]", Logging.Green);
                stargate.Jump();
                Cache.Instance.NextInSpaceorInStation = DateTime.Now;
                _nextTravelerAction = DateTime.Now.AddSeconds(Time.Instance.TravelerJumpedGateNextCommandDelay_seconds);
                Cache.Instance.NextActivateSupportModules = DateTime.Now.AddSeconds(Time.Instance.TravelerJumpedGateNextCommandDelay_seconds);
                return;
            }
            
            if (stargate.Distance < (int)Distance.WarptoDistance)
            {
                if (DateTime.Now > Cache.Instance.NextApproachAction && !Cache.Instance.IsApproaching)
                {
                    if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: approaching stargate", Logging.Teal);
                    stargate.Approach(); //you could use a negative approach distance here but ultimately that is a bad idea.. Id like to go toward the entity without approaching it so we would end up inside the docking ring (eventually)
                    return;
                }
                if (Settings.Instance.DebugTraveler) Logging.Log("Traveler", "NavigateToBookmarkSystem: we are already approaching the stargate", Logging.Teal);
                return;
            }
            
            if (DateTime.Now > Cache.Instance.NextWarpTo)
            {
                if (Cache.Instance.InSpace && !Cache.Instance.TargetedBy.Any(t => t.IsWarpScramblingMe))
                {
                    Logging.Log("Traveler",
                                "Warping to [" + Logging.Yellow + _locationName + Logging.Green + "][" + Logging.Yellow + 
                                Math.Round((stargate.Distance / 1000) / 149598000, 2) + Logging.Green + " AU away]", Logging.Green);
                    stargate.WarpTo();
                    return;
                }
                return;
            }
            if (!Combat.ReloadAll(Cache.Instance.EntitiesNotSelf.OrderBy(t => t.Distance).FirstOrDefault(t => t.Distance < (double)Distance.OnGridWithMe))) return;
            return;
        }

        public void ProcessState()
        {
            // Only pulse state changes every 1.5s
            if (DateTime.Now.Subtract(_lastPulse).TotalMilliseconds < Time.Instance.QuestorPulse_milliseconds) //default: 1500ms
                return;

            _lastPulse = DateTime.Now;

            switch (_States.CurrentTravelerState)
            {
                case TravelerState.Idle:
                    _States.CurrentTravelerState = TravelerState.Traveling;
                    break;

                case TravelerState.Traveling:
                    if (Cache.Instance.InWarp || (!Cache.Instance.InSpace && !Cache.Instance.InStation)) //if we are in warp, do nothing, as nothing can actually be done until we are out of warp anyway.
                        return;

                    if (Destination == null)
                    {
                        _States.CurrentTravelerState = TravelerState.Error;
                        break;
                    }

                    if (Destination.SolarSystemId != Cache.Instance.DirectEve.Session.SolarSystemId)
                    {
                        //Logging.Log("traveler: NagivateToBookmarkSystem(Destination.SolarSystemId);");
                        NagivateToBookmarkSystem(Destination.SolarSystemId);
                    }
                    else if (Destination.PerformFinalDestinationTask())
                    {
                        _destinationRoute = null;
                        _location = null;
                        _locationName = string.Empty;
                        _locationErrors = 0;
                        //Logging.Log("traveler: _States.CurrentTravelerState = TravelerState.AtDestination;");
                        _States.CurrentTravelerState = TravelerState.AtDestination;
                    }
                    break;

                case TravelerState.AtDestination:
                    //do nothing when at destination
                    //Traveler sits in AtDestination when it has nothing to do, NOT in idle.
                    break;

                default:
                    break;
            }
        }
    }
}