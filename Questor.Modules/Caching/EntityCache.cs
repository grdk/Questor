﻿// ------------------------------------------------------------------------------
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
    using System.Linq;
    using DirectEve;
    //using System.Collections.Generic;
    using global::Questor.Modules.Lookup;
    using global::Questor.Modules.Logging;

    public class EntityCache
    {
        private readonly DirectEntity _directEntity;

        public EntityCache(DirectEntity entity)
        {
            _directEntity = entity;
        }

        public int GroupId
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.GroupId;

                return 0;
            }
        }

        public int CategoryId
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.CategoryId;

                return 0;
            }
        }

        public long Id
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.Id;

                return 0;
            }
        }

        public int TypeId
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.TypeId;

                return 0;
            }
        }

        public long FollowId
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.FollowId;

                return 0;
            }
        }

        public string Name
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.Name ?? string.Empty;

                return string.Empty;
            }
        }

        public double Distance
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.Distance != 0)
                    {
                        //
                        // if we use the formatted value everywhere we need to change ALL distances to kilometers instead of meters (which would take some time and effort)
                        //
                        //Double DistanceFormatted = Math.Round(_directEntity.Distance / 1000, 2);
                        //return DistanceFormatted;
                        return _directEntity.Distance;
                    }

                    return 0;
                }

                return 0;
            }
        }

        public double Nearest5kDistance
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.Distance > 0 && _directEntity.Distance < 900000000)
                    {
                        double _nearest5kDistance = Math.Round((_directEntity.Distance / 1000) * 2, MidpointRounding.AwayFromZero) / 2;
                        return _nearest5kDistance;
                    }

                    return 0;
                }

                return 0;
            }
        }

        public double ShieldPct
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.ShieldPct;

                return 0;
            }
        }

        public double ArmorPct
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.ArmorPct;

                return 0;
            }
        }

        public double StructurePct
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.StructurePct;

                return 0;
            }
        }

        public bool IsNpc
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.IsNpc;

                return false;
            }
        }

        public double Velocity
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.Velocity;

                return 0;
            }
        }

        public bool IsTarget
        {
            get
            {
                if (_directEntity != null)
                {
                    if (!_directEntity.HasExploded && Cache.Instance.Entities.Any(t => t.Id == _directEntity.Id))
                    {
                        return _directEntity.IsTarget;
                    }

                    return false;
                }                    

                return false;
            }
        }

        public bool IsPrimaryWeaponKillPriority
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance._primaryWeaponPriorityTargets.Any(e => e.Entity.Id == _directEntity.Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsDroneKillPriority
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance._dronePriorityTargets.Any(e => e.Entity.Id == _directEntity.Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsTooCloseTooFastTooSmallToHit
        {
            get
            {
                if (_directEntity != null)
                {
                    if (IsNPCFrigate || IsFrigate)
                    {
                        if (Cache.Instance.DoWeCurrentlyHaveTurretsMounted() && Cache.Instance.UseDrones)
                        {
                            if (_directEntity.Distance < Settings.Instance.DistanceNPCFrigatesShouldBeIgnoredByPrimaryWeapons
                             && _directEntity.Velocity > Settings.Instance.SpeedNPCFrigatesShouldBeIgnoredByPrimaryWeapons)
                            {
                                return true;
                            }

                            return false;
                        }

                        return false;
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsReadyToShoot
        {
            get
            {
                if (_directEntity != null)
                {
                    if (!HasExploded && IsTarget)
                    {
                        if (_directEntity.Distance < Cache.Instance.MaxRange)
                        {
                            if (Cache.Instance.Entities.Any(t => t.Id == _directEntity.Id))
                            {
                                return true;
                            }

                            return false; 
                        }

                        return false;
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsEntityWeShouldKeepShooting
        {
            get
            {
                if (_directEntity != null)
                {
                    if ((IsTarget)
                      && _directEntity.ArmorPct * 100 < Settings.Instance.DoNotSwitchTargetsIfTargetHasMoreThanThisArmorDamagePercentage
                      && !Cache.Instance.IgnoreTargets.Contains(_directEntity.Name.Trim()))
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsReadyToNavigateToward
        {
            get
            {
                if (_directEntity != null)
                {
                    if (!HasExploded && IsTarget)
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsReadyToTarget
        {
            get
            {
                if (_directEntity != null)
                {
                    if (!HasExploded && !IsTarget && !IsTargeting)
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsHigherPriorityPresent
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance.PrimaryWeaponPriorityTargets.Any() || Cache.Instance.DronePriorityTargets.Any())
                    {

                        if (Cache.Instance.PrimaryWeaponPriorityTargets.Any()) 
                        {
                            if (Cache.Instance.PrimaryWeaponPriorityTargets.Any(pt => pt.Id == _directEntity.Id))
                            {
                                PrimaryWeaponPriority _currentPrimaryWeaponPriority = Cache.Instance.PrimaryWeaponPriorityTargets.Where(t => t.Id == _directEntity.Id).Select(pt => pt.PrimaryWeaponPriorityLevel).FirstOrDefault();

                                if (!Cache.Instance.PrimaryWeaponPriorityTargets.All(pt => pt.PrimaryWeaponPriorityLevel < _currentPrimaryWeaponPriority && pt.Distance < Cache.Instance.MaxRange))
                                {
                                    return true;
                                }

                                return false;
                            }

                            if (Cache.Instance.PrimaryWeaponPriorityTargets.Any(e => e.Distance < Cache.Instance.MaxRange))
                            {
                                return true;
                            }

                            return false;
                        }

                        if (Cache.Instance.DronePriorityTargets.Any())
                        {
                            if (Cache.Instance.DronePriorityTargets.Any(pt => pt.Id == _directEntity.Id))
                            {
                                DronePriority _currentEntityDronePriority = Cache.Instance.DronePriorityTargets.Where(t => t.Id == _directEntity.Id).Select(pt => pt.DronePriorityLevel).FirstOrDefault();

                                if (!Cache.Instance.DronePriorityTargets.All(pt => pt.DronePriorityLevel < _currentEntityDronePriority && pt.Distance < Settings.Instance.DroneControlRange))
                                {
                                    return true;
                                }

                                return false;
                            }

                            if (Cache.Instance.DronePriorityTargets.Any(e => e.Distance < Settings.Instance.DroneControlRange))
                            {
                                return true;
                            }

                            return false;
                        }

                        return false;
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsLowerPriorityPresent
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance._primaryWeaponPriorityTargets.Any() || Cache.Instance._dronePriorityTargets.Any())
                    {

                        if (Cache.Instance._primaryWeaponPriorityTargets.Any())
                        {
                            if (Cache.Instance._primaryWeaponPriorityTargets.Any(pt => pt.EntityID == _directEntity.Id))
                            {
                                PrimaryWeaponPriority _currentPrimaryWeaponPriority = Cache.Instance._primaryWeaponPriorityTargets.Where(t => t.EntityID == _directEntity.Id).Select(pt => pt.PrimaryWeaponPriority).FirstOrDefault();

                                if (!Cache.Instance._primaryWeaponPriorityTargets.All(pt => pt.PrimaryWeaponPriority > _currentPrimaryWeaponPriority && pt.Entity.Distance < Cache.Instance.MaxRange))
                                {
                                    return true;
                                }

                                return false;
                            }

                            if (Cache.Instance._primaryWeaponPriorityTargets.Any(e => e.Entity.Distance < Cache.Instance.MaxRange))
                            {
                                return true;
                            }

                            return false;
                        }

                        if (Cache.Instance._dronePriorityTargets.Any())
                        {
                            if (Cache.Instance._dronePriorityTargets.Any(pt => pt.EntityID == _directEntity.Id))
                            {
                                DronePriority _currentEntityDronePriority = Cache.Instance._dronePriorityTargets.Where(t => t.EntityID == _directEntity.Id).Select(pt => pt.DronePriority).FirstOrDefault();

                                if (!Cache.Instance._dronePriorityTargets.All(pt => pt.DronePriority > _currentEntityDronePriority && pt.Entity.Distance < Settings.Instance.DroneControlRange))
                                {
                                    return true;
                                }

                                return false;
                            }

                            if (Cache.Instance._dronePriorityTargets.Any(e => e.Entity.Distance < Settings.Instance.DroneControlRange))
                            {
                                return true;
                            }

                            return false;
                        }

                        return false;
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsActiveTarget
        {
            get
            {
                if (_directEntity != null)
                {
                    if (IsTarget)
                    {
                        return _directEntity.IsActiveTarget;        
                    }
                }

                return false;
            }
        }

        public bool IsInOptimalRange
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Settings.Instance.SpeedTank && Settings.Instance.OrbitDistance != 0 )
                    {
                        if (Settings.Instance.OptimalRange == 0)
                        {
                            Cache.Instance.OptimalRange = Settings.Instance.OrbitDistance + 5000;
                        }
                    }

                    if (Cache.Instance.OptimalRange != 0 || Settings.Instance.OptimalRange != 0)
                    {
                        double optimal = 0;
                        
                        if (Cache.Instance.OptimalRange != 0)
                        {
                            optimal = Cache.Instance.OptimalRange;
                        }
                        else if (Settings.Instance.OptimalRange != 0) //do we really need this condition? we cant even get in here if one of them isnt != 0, that is the idea, if its 0 we sure as hell dont want to use it as the optimal
                        {
                            optimal = Settings.Instance.OptimalRange;
                        }

                        if (Cache.Instance.DoWeCurrentlyHaveTurretsMounted()) //Lasers, Projectile, and Hybrids
                        {
                            if (Distance > Settings.Instance.InsideThisRangeIsHardToTrack)
                            {
                                if (Distance < (optimal * 1.5))
                                {
                                    return true;
                                }
                            }
                        }
                        else //missile boats - use max range
                        {
                            optimal = Cache.Instance.MaxRange;
                            if (Distance < optimal)
                            {
                                return true;
                            }
                        }
                        
                        return false;
                    }

                    // If you have no optimal you have to assume the entity is within Optimal... (like missiles)
                    return true;
                }

                return false;
            }
        }

        public bool IsDronePriorityTarget
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance.DronePriorityTargets.All(i => i.Id != Id))
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }
        }

        public bool IsPriorityWarpScrambler
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance.PrimaryWeaponPriorityTargets.Any(pt => pt.Id == Id))
                    {
                        EntityCache __entity = new EntityCache(_directEntity);
                        if (__entity.PrimaryWeaponPriorityLevel == PrimaryWeaponPriority.WarpScrambler)
                        {
                            return true;
                        }
                    }

                    if (Cache.Instance.DronePriorityTargets.Any(pt => pt.Id == Id))
                    {
                        EntityCache __entity = new EntityCache(_directEntity);
                        if (__entity.DronePriorityLevel == DronePriority.WarpScrambler)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                return false;
            }
        }

        public bool IsPrimaryWeaponPriorityTarget
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance.PrimaryWeaponPriorityTargets.All(i => i.Id != Id))
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }
        }

        public PrimaryWeaponPriority PrimaryWeaponPriorityLevel
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance.PrimaryWeaponPriorityTargets.Any(pt => pt.Id == Id))
                    {
                        PrimaryWeaponPriority currentTargetPriority = Cache.Instance._primaryWeaponPriorityTargets.Where(t => t.Entity.IsTarget
                                                                                                                           && t.EntityID == Id)
                                                                                                                  .Select(pt => pt.PrimaryWeaponPriority)
                                                                                                                  .FirstOrDefault();
                        return currentTargetPriority;
                    }

                    return PrimaryWeaponPriority.NotUsed;
                }

                return PrimaryWeaponPriority.NotUsed;
            }
        }

        public DronePriority DronePriorityLevel
        {
            get
            {
                if (_directEntity != null)
                {
                    if (Cache.Instance._dronePriorityTargets.Any(pt => pt.EntityID == _directEntity.Id))
                    {
                        DronePriority currentTargetPriority = Cache.Instance._dronePriorityTargets.Where(t => t.Entity.IsTarget
                                                                                                                  && t.EntityID == Id)
                                                                                                                  .Select(pt => pt.DronePriority)
                                                                                                                  .FirstOrDefault();

                        return currentTargetPriority;
                    }

                    return DronePriority.NotUsed;
                }

                return DronePriority.NotUsed;
            }
        }

        public bool IsTargeting
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.IsTargeting;

                return false;
            }
        }

        public bool IsTargetedBy
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.IsTargetedBy;

                return false;
            }
        }

        public bool IsAttacking
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.IsAttacking;

                return false;
            }
        }

        public bool IsWreckEmpty
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.IsEmpty;

                return false;
            }
        }

        public bool HasReleased
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.HasReleased;

                return false;
            }
        }

        public bool HasExploded
        {
            get
            {
                if (_directEntity != null)
                    return _directEntity.HasExploded;

                return false;
            }
        }

        public bool IsEwarTarget()
        {
            bool result = false;
            result |= IsWarpScramblingMe;
            result |= IsWebbingMe;
            result |= IsNeutralizingMe;
            result |= IsJammingMe;
            result |= IsSensorDampeningMe;
            result |= IsTargetPaintingMe;
            result |= IsTrackingDisruptingMe;
            return result;
        }

        public DronePriority IsActiveDroneEwarType()
        {
            if (IsWarpScramblingMe)
            {
                return DronePriority.WarpScrambler;
            }

            if (IsWebbingMe)
            {
                return DronePriority.Webbing;
            }

            if (IsNeutralizingMe)
            {
                return DronePriority.PriorityKillTarget;
            }

            if (IsJammingMe)
            {
                return DronePriority.PriorityKillTarget;
            }

            if (IsSensorDampeningMe)
            {
                return DronePriority.PriorityKillTarget;
            }

            if (IsTargetPaintingMe)
            {
                return DronePriority.PriorityKillTarget;
            }

            if (IsTrackingDisruptingMe)
            {
                return DronePriority.PriorityKillTarget;
            }
            
            return DronePriority.NotUsed;
        }

        public PrimaryWeaponPriority IsActivePrimaryWeaponEwarType()
        {
            if (IsWarpScramblingMe)
            {
                return PrimaryWeaponPriority.WarpScrambler;
            }

            if (IsWebbingMe)
            {
                return PrimaryWeaponPriority.Webbing;
            }

            if (IsNeutralizingMe)
            {
                return PrimaryWeaponPriority.Neutralizing;
            }

            if (IsJammingMe)
            {
                return PrimaryWeaponPriority.Jamming;
            }

            if (IsSensorDampeningMe)
            {
                return PrimaryWeaponPriority.Dampening;
            }

            if (IsTargetPaintingMe)
            {
                return PrimaryWeaponPriority.TargetPainting;
            }

            if (IsTrackingDisruptingMe)
            {
                return PrimaryWeaponPriority.TrackingDisrupting;
            }

            return PrimaryWeaponPriority.NotUsed;
        }

        public bool IsWarpScramblingMe
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.Attacks.Contains("effects.WarpScramble"))
                    {
                        if (!Cache.Instance.WarpScrambler.Contains(_directEntity.Id)) Cache.Instance.WarpScrambler.Add(_directEntity.Id);
                        return true;
                    }
                    if (Cache.Instance.WarpScrambler.Contains(_directEntity.Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsWebbingMe
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.Attacks.Contains("effects.ModifyTargetSpeed"))
                    {
                        if (!Cache.Instance.Webbing.Contains(Id)) Cache.Instance.Webbing.Add(Id);
                        return true;
                    }
                    
                    if (Cache.Instance.Webbing.Contains(Id))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool IsNeutralizingMe
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.ElectronicWarfare.Contains("ewEnergyNeut"))
                    {
                        if (!Cache.Instance.Neuting.Contains(Id)) Cache.Instance.Neuting.Add(Id);
                        return true;
                    }
                    
                    if (Cache.Instance.Neuting.Contains(Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsJammingMe
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.ElectronicWarfare.Contains("electronic"))
                    {
                        if (!Cache.Instance.Jammer.Contains(Id)) Cache.Instance.Jammer.Add(Id);
                        return true;
                    }
                    
                    if (Cache.Instance.Jammer.Contains(Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsSensorDampeningMe
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.ElectronicWarfare.Contains("ewRemoteSensorDamp"))
                    {
                        if (!Cache.Instance.Dampening.Contains(Id)) Cache.Instance.Dampening.Add(Id);
                        return true;
                    }
                    
                    if (Cache.Instance.Dampening.Contains(Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsTargetPaintingMe
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.ElectronicWarfare.Contains("ewTargetPaint"))
                    {
                        if (!Cache.Instance.TargetPainting.Contains(Id)) Cache.Instance.TargetPainting.Add(Id);
                        return true;
                    }
                    
                    if (Cache.Instance.TargetPainting.Contains(Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsTrackingDisruptingMe
        {
            get
            {
                if (_directEntity != null)
                {
                    if (_directEntity.ElectronicWarfare.Contains("ewTrackingDisrupt"))
                    {
                        if (!Cache.Instance.TrackingDisrupter.Contains(Id)) Cache.Instance.TrackingDisrupter.Add(Id);
                        return true;
                    }
                    
                    if (Cache.Instance.TrackingDisrupter.Contains(Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int Health
        {
            get
            {
                if (_directEntity != null)
                {
                    return (int)((ShieldPct + ArmorPct + StructurePct) * 100);
                }

                return 0;
            }
        }

        public bool IsSentry
        {
            get
            {
                if (GroupId == (int)Group.SentryGun) return true;
                if (GroupId == (int)Group.ProtectiveSentryGun) return true;
                if (GroupId == (int)Group.MobileSentryGun) return true;
                if (GroupId == (int)Group.DestructibleSentryGun) return true;
                if (GroupId == (int)Group.MobileMissileSentry) return true;
                if (GroupId == (int)Group.MobileProjectileSentry) return true;
                if (GroupId == (int)Group.MobileLaserSentry) return true;
                if (GroupId == (int)Group.MobileHybridSentry) return true;
                if (GroupId == (int)Group.DeadspaceOverseersSentry) return true;
                if (GroupId == (int)Group.StasisWebificationBattery) return true;
                if (GroupId == (int)Group.EnergyNeutralizingBattery) return true;
                return false;
            }
        }

        public bool HaveLootRights
        {
            get
            {
                if (GroupId == (int)Group.SpawnContainer)
                    return true;

                if (_directEntity != null)
                {
                    bool haveLootRights = false;
                    if (Cache.Instance.DirectEve.ActiveShip.Entity != null)
                    {
                        haveLootRights |= _directEntity.CorpId == Cache.Instance.DirectEve.ActiveShip.Entity.CorpId;
                        haveLootRights |= _directEntity.OwnerId == Cache.Instance.DirectEve.ActiveShip.Entity.CharId;
                        //
                        // It would be nice if this were eventually extended to detect and include 'abandoned' wrecks (blue ones). 
                        // I do not yet know what attributed actually change when that happens. We should collect some data. 
                        //
                    }

                    return haveLootRights;
                }

                return false;
            }
        }

        public int? TargetValue
        {
            get
            {
                ShipTargetValue value = null;

                try
                {
                    value = Cache.Instance.ShipTargetValues.FirstOrDefault(v => v.GroupId == GroupId);
                }
                catch (Exception exception)
                {
                    if (Settings.Instance.DebugShipTargetValues) Logging.Log("TargetValue", "exception [" + exception + "]", Logging.Debug);
                }

                if (value == null)
                {

                    if (IsNPCBattleship) return 4;
                    if (IsNPCBattlecruiser) return 3;
                    if (IsNPCCruiser) return 2;
                    if (IsNPCFrigate) return 0;
                    return 2;
                }

                return value.TargetValue;
            }
        }

        public DirectContainerWindow CargoWindow
        {
            get { return Cache.Instance.Windows.OfType<DirectContainerWindow>().FirstOrDefault(w => w.ItemId == Id); }
        }

        public bool IsValid
        {
            get
            {
                if (_directEntity == null)
                {
                    return false;
                }

                if (!HasExploded)
                {
                    return _directEntity.IsValid;
                }

                return false;
            }
        }

        public bool IsContainer
        {
            get
            {
                if (GroupId == (int)Group.Wreck) return true;
                if (GroupId == (int)Group.CargoContainer) return true;
                if (GroupId == (int)Group.SpawnContainer) return true;
                if (GroupId == (int)Group.MissionContainer) return true;
                return false;
            }
        }

        public bool IsPlayer
        {
            get { return _directEntity.IsPc; }
        }

        public bool IsTargetingMeAndNotYetTargeted
        {
            get
            {
                bool result = false;
                result |= (((IsNpc || IsNpcByGroupID) || IsAttacking)
                           && (!IsSentry || (IsSentry && Settings.Instance.KillSentries))
                           && (!IsTargeting && !IsTarget && IsTargetedBy)
                           && !IsContainer
                           && CategoryId == (int)CategoryID.Entity
                           && Distance < Cache.Instance.DirectEve.ActiveShip.MaxTargetRange
                           && !Cache.Instance.IgnoreTargets.Contains(Name.Trim())
                           && (!IsBadIdea || IsAttacking)
                           && !IsEntityIShouldLeaveAlone
                           && !IsFactionWarfareNPC
                           && !IsLargeCollidable
                           && !IsStation);
                return result;
            }
        }

        public bool IsNotYetTargetingMeAndNotYetTargeted
        {
            get
            {
                bool result = false;
                result |= (((IsNpc || IsNpcByGroupID) || IsAttacking || Cache.Instance.InMission)
                           && (!IsSentry || (IsSentry && Settings.Instance.KillSentries))
                           && (!IsTargeting && !IsTarget)
                           && !IsContainer
                           && CategoryId == (int) CategoryID.Entity
                           && Distance < Cache.Instance.DirectEve.ActiveShip.MaxTargetRange
                           && !Cache.Instance.IgnoreTargets.Contains(Name.Trim())
                           && (!IsBadIdea || IsAttacking)
                           && !IsEntityIShouldLeaveAlone
                           && !IsFactionWarfareNPC
                           && !IsLargeCollidable
                           && !IsStation);
                return result;
            }
        }

        public bool IsTargetWeCanShootButHaveNotYetTargeted
        {
            get
            {
                bool result = false;
                result |= (((IsNpc || IsNpcByGroupID) || IsAttacking)
                           && !IsTarget
                           && !IsTargeting
                           && (!IsSentry || (IsSentry && Settings.Instance.KillSentries))
                           && !IsContainer
                           && CategoryId == (int) CategoryID.Entity
                           && Distance < Cache.Instance.DirectEve.ActiveShip.MaxTargetRange
                           && !Cache.Instance.IgnoreTargets.Contains(Name.Trim())
                           && (!IsBadIdea || IsAttacking)
                           && !IsEntityIShouldLeaveAlone
                           && !IsFactionWarfareNPC
                           && !IsStation);
                return result;
            }
        }

        /// <summary>
        /// Frigate includes all elite-variants - this does NOT need to be limited to players, as we check for players specifically everywhere this is used
        /// </summary>
        public bool IsFrigate
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Frigate;
                result |= GroupId == (int)Group.AssaultShip;
                result |= GroupId == (int)Group.StealthBomber;
                result |= GroupId == (int)Group.ElectronicAttackShip;
                result |= GroupId == (int)Group.PrototypeExplorationShip;

                // Technically not frigs, but for our purposes they are
                result |= GroupId == (int)Group.Destroyer;
                result |= GroupId == (int)Group.Interdictor;
                result |= GroupId == (int)Group.Interceptor;
                return result;
            }
        }

        /// <summary>
        /// Frigate includes all elite-variants - this does NOT need to be limited to players, as we check for players specifically everywhere this is used
        /// </summary>
        public bool IsNPCFrigate
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Guristas_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Guristas_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Serpentis_Destroyer;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Destroyer;
                result |= GroupId == (int)Group.Mission_Caldari_State_Destroyer;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Destroyer;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Destroyer;
                result |= GroupId == (int)Group.Mission_Khanid_Destroyer;
                result |= GroupId == (int)Group.Mission_CONCORD_Destroyer;
                result |= GroupId == (int)Group.Mission_Mordu_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Destroyer;
                result |= GroupId == (int)Group.Mission_Thukker_Destroyer;
                result |= GroupId == (int)Group.Mission_Generic_Destroyers;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Destroyer;
                result |= GroupId == (int)Group.asteroid_angel_cartel_frigate;
                result |= GroupId == (int)Group.asteroid_blood_raiders_frigate;
                result |= GroupId == (int)Group.asteroid_guristas_frigate;
                result |= GroupId == (int)Group.asteroid_sanshas_nation_frigate;
                result |= GroupId == (int)Group.asteroid_serpentis_frigate;
                result |= GroupId == (int)Group.deadspace_angel_cartel_frigate;
                result |= GroupId == (int)Group.deadspace_blood_raiders_frigate;
                result |= GroupId == (int)Group.deadspace_guristas_frigate;
                result |= GroupId == (int)Group.deadspace_sanshas_nation_frigate;
                result |= GroupId == (int)Group.deadspace_serpentis_frigate;
                result |= GroupId == (int)Group.mission_amarr_empire_frigate;
                result |= GroupId == (int)Group.mission_caldari_state_frigate;
                result |= GroupId == (int)Group.mission_gallente_federation_frigate;
                result |= GroupId == (int)Group.mission_minmatar_republic_frigate;
                result |= GroupId == (int)Group.mission_khanid_frigate;
                result |= GroupId == (int)Group.mission_concord_frigate;
                result |= GroupId == (int)Group.mission_mordu_frigate;
                result |= GroupId == (int)Group.asteroid_rouge_drone_frigate;
                result |= GroupId == (int)Group.asteroid_rouge_drone_frigate2;
                result |= GroupId == (int)Group.asteroid_angel_cartel_commander_frigate;
                result |= GroupId == (int)Group.asteroid_blood_raiders_commander_frigate;
                result |= GroupId == (int)Group.asteroid_guristas_commander_frigate;
                result |= GroupId == (int)Group.asteroid_sanshas_nation_commander_frigate;
                result |= GroupId == (int)Group.asteroid_serpentis_commander_frigate;
                result |= GroupId == (int)Group.mission_generic_frigates;
                result |= GroupId == (int)Group.mission_thukker_frigate;
                result |= GroupId == (int)Group.asteroid_rouge_drone_commander_frigate;
                result |= Name.Contains("Spider Drone"); //we *really* need to find out the GroupID of this one. 
                return result;
            }
        }

        /// <summary>
        /// Cruiser includes all elite-variants
        /// </summary>
        public bool IsCruiser
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Cruiser;
                result |= GroupId == (int)Group.HeavyAssaultShip;
                result |= GroupId == (int)Group.Logistics;
                result |= GroupId == (int)Group.ForceReconShip;
                result |= GroupId == (int)Group.CombatReconShip;
                result |= GroupId == (int)Group.HeavyInterdictor;
                return result;
            }
        }

        /// <summary>
        /// Cruiser includes all elite-variants
        /// </summary>
        public bool IsNPCCruiser
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Storyline_Cruiser;
                result |= GroupId == (int)Group.Storyline_Mission_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Guristas_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Serpentis_Cruiser;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Cruiser;
                result |= GroupId == (int)Group.Mission_Caldari_State_Cruiser;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Cruiser;
                result |= GroupId == (int)Group.Mission_Khanid_Cruiser;
                result |= GroupId == (int)Group.Mission_CONCORD_Cruiser;
                result |= GroupId == (int)Group.Mission_Mordu_Cruiser;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Cruiser;
                result |= GroupId == (int)Group.Mission_Generic_Cruisers;
                result |= GroupId == (int)Group.Deadspace_Overseer_Cruiser;
                result |= GroupId == (int)Group.Mission_Thukker_Cruiser;
                result |= GroupId == (int)Group.Mission_Generic_Battle_Cruisers;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Cruiser;
                result |= GroupId == (int)Group.Mission_Faction_Cruiser;
                return result;
            }
        }

        /// <summary>
        /// Battlecruiser includes all elite-variants
        /// </summary>
        public bool IsBattlecruiser
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Battlecruiser;
                result |= GroupId == (int)Group.CommandShip;
                result |= GroupId == (int)Group.StrategicCruiser; // Technically a cruiser, but hits hard enough to be a BC :)
                return result;
            }
        }

        /// <summary>
        /// Battlecruiser includes all elite-variants
        /// </summary>
        public bool IsNPCBattlecruiser
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Guristas_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Serpentis_BattleCruiser;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Caldari_State_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Khanid_Battlecruiser;
                result |= GroupId == (int)Group.Mission_CONCORD_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Mordu_Battlecruiser;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Mission_Thukker_Battlecruiser;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_BattleCruiser;
                return result;
            }
        }

        /// <summary>
        /// Battleship includes all elite-variants
        /// </summary>
        public bool IsBattleship
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Battleship;
                result |= GroupId == (int)Group.EliteBattleship;
                result |= GroupId == (int)Group.BlackOps;
                result |= GroupId == (int)Group.Marauder;
                return result;
            }
        }

        /// <summary>
        /// Battleship includes all elite-variants
        /// </summary>
        public bool IsNPCBattleship
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Storyline_Battleship;
                result |= GroupId == (int)Group.Storyline_Mission_Battleship;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Battleship;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Battleship;
                result |= GroupId == (int)Group.Asteroid_Guristas_Battleship;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Battleship;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Battleship;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Battleship;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Battleship;
                result |= GroupId == (int)Group.Deadspace_Guristas_Battleship;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Battleship;
                result |= GroupId == (int)Group.Deadspace_Serpentis_Battleship;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Battleship;
                result |= GroupId == (int)Group.Mission_Caldari_State_Battleship;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Battleship;
                result |= GroupId == (int)Group.Mission_Khanid_Battleship;
                result |= GroupId == (int)Group.Mission_CONCORD_Battleship;
                result |= GroupId == (int)Group.Mission_Mordu_Battleship;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battleship;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Battleship;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Battleship;
                result |= GroupId == (int)Group.Mission_Generic_Battleships;
                result |= GroupId == (int)Group.Deadspace_Overseer_Battleship;
                result |= GroupId == (int)Group.Mission_Thukker_Battleship;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Battleship;
                result |= GroupId == (int)Group.Mission_Faction_Battleship;
                return result;
            }
        }

        /// <summary>
        /// A bad idea to attack these targets
        /// </summary>
        public bool IsLargeCollidable
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.LargeColidableObject;
                result |= GroupId == (int)Group.LargeColidableShip;
                result |= GroupId == (int)Group.LargeColidableStructure;
                return result;
            }
        }

        /// <summary>
        /// A bad idea to attack these targets
        /// </summary>
        public bool IsBadIdea
        {
            get
            {
                if (Cache.Instance.Entities.Any(t => t.Id != Id))
                {
                    return false;
                }

                bool result = false;
                result |= GroupId == (int)Group.ConcordDrone;
                result |= GroupId == (int)Group.PoliceDrone;
                result |= GroupId == (int)Group.CustomsOfficial;
                result |= GroupId == (int)Group.Billboard;
                result |= GroupId == (int)Group.Stargate;
                result |= GroupId == (int)Group.Station;
                result |= GroupId == (int)Group.SentryGun;
                result |= GroupId == (int)Group.Capsule;
                result |= GroupId == (int)Group.MissionContainer;
                result |= GroupId == (int)Group.CustomsOffice;
                result |= GroupId == (int)Group.GasCloud;
                result |= IsFrigate;
                result |= IsCruiser;
                result |= IsBattlecruiser;
                result |= IsBattleship;
                result |= IsPlayer;
                return result;
            }
        }

        public bool IsFactionWarfareNPC
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.FactionWarfareNPC;
                return result;
            }
        }

        public bool IsNpcByGroupID
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Storyline_Battleship;
                result |= GroupId == (int)Group.Storyline_Mission_Battleship;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Battleship;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Battleship;
                result |= GroupId == (int)Group.Asteroid_Guristas_Battleship;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Battleship;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Battleship;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Battleship;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Battleship;
                result |= GroupId == (int)Group.Deadspace_Guristas_Battleship;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Battleship;
                result |= GroupId == (int)Group.Deadspace_Serpentis_Battleship;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Battleship;
                result |= GroupId == (int)Group.Mission_Caldari_State_Battleship;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Battleship;
                result |= GroupId == (int)Group.Mission_Khanid_Battleship;
                result |= GroupId == (int)Group.Mission_CONCORD_Battleship;
                result |= GroupId == (int)Group.Mission_Mordu_Battleship;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battleship;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Battleship;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Battleship;
                result |= GroupId == (int)Group.Mission_Generic_Battleships;
                result |= GroupId == (int)Group.Deadspace_Overseer_Battleship;
                result |= GroupId == (int)Group.Mission_Thukker_Battleship;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Battleship;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Battleship;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Battleship;
                result |= GroupId == (int)Group.Mission_Faction_Battleship;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Guristas_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Serpentis_BattleCruiser;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Caldari_State_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Khanid_Battlecruiser;
                result |= GroupId == (int)Group.Mission_CONCORD_Battlecruiser;
                result |= GroupId == (int)Group.Mission_Mordu_Battlecruiser;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Mission_Thukker_Battlecruiser;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_BattleCruiser;
                result |= GroupId == (int)Group.Storyline_Cruiser;
                result |= GroupId == (int)Group.Storyline_Mission_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Guristas_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Serpentis_Cruiser;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Cruiser;
                result |= GroupId == (int)Group.Mission_Caldari_State_Cruiser;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Cruiser;
                result |= GroupId == (int)Group.Mission_Khanid_Cruiser;
                result |= GroupId == (int)Group.Mission_CONCORD_Cruiser;
                result |= GroupId == (int)Group.Mission_Mordu_Cruiser;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Cruiser;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Cruiser;
                result |= GroupId == (int)Group.Mission_Generic_Cruisers;
                result |= GroupId == (int)Group.Deadspace_Overseer_Cruiser;
                result |= GroupId == (int)Group.Mission_Thukker_Cruiser;
                result |= GroupId == (int)Group.Mission_Generic_Battle_Cruisers;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Cruiser;
                result |= GroupId == (int)Group.Mission_Faction_Cruiser;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Guristas_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Angel_Cartel_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Blood_Raiders_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Guristas_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Sanshas_Nation_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Serpentis_Destroyer;
                result |= GroupId == (int)Group.Mission_Amarr_Empire_Destroyer;
                result |= GroupId == (int)Group.Mission_Caldari_State_Destroyer;
                result |= GroupId == (int)Group.Mission_Gallente_Federation_Destroyer;
                result |= GroupId == (int)Group.Mission_Minmatar_Republic_Destroyer;
                result |= GroupId == (int)Group.Mission_Khanid_Destroyer;
                result |= GroupId == (int)Group.Mission_CONCORD_Destroyer;
                result |= GroupId == (int)Group.Mission_Mordu_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Angel_Cartel_Commander_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Blood_Raiders_Commander_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Guristas_Commander_Destroyer;
                result |= GroupId == (int)Group.Deadspace_Rogue_Drone_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Sanshas_Nation_Commander_Destroyer;
                result |= GroupId == (int)Group.Asteroid_Serpentis_Commander_Destroyer;
                result |= GroupId == (int)Group.Mission_Thukker_Destroyer;
                result |= GroupId == (int)Group.Mission_Generic_Destroyers;
                result |= GroupId == (int)Group.Asteroid_Rogue_Drone_Commander_Destroyer;
                result |= GroupId == (int)Group.asteroid_angel_cartel_frigate;
                result |= GroupId == (int)Group.asteroid_blood_raiders_frigate;
                result |= GroupId == (int)Group.asteroid_guristas_frigate;
                result |= GroupId == (int)Group.asteroid_sanshas_nation_frigate;
                result |= GroupId == (int)Group.asteroid_serpentis_frigate;
                result |= GroupId == (int)Group.deadspace_angel_cartel_frigate;
                result |= GroupId == (int)Group.deadspace_blood_raiders_frigate;
                result |= GroupId == (int)Group.deadspace_guristas_frigate;
                result |= GroupId == (int)Group.deadspace_sanshas_nation_frigate;
                result |= GroupId == (int)Group.deadspace_serpentis_frigate;
                result |= GroupId == (int)Group.mission_amarr_empire_frigate;
                result |= GroupId == (int)Group.mission_caldari_state_frigate;
                result |= GroupId == (int)Group.mission_gallente_federation_frigate;
                result |= GroupId == (int)Group.mission_minmatar_republic_frigate;
                result |= GroupId == (int)Group.mission_khanid_frigate;
                result |= GroupId == (int)Group.mission_concord_frigate;
                result |= GroupId == (int)Group.mission_mordu_frigate;
                result |= GroupId == (int)Group.asteroid_rouge_drone_frigate;
                result |= GroupId == (int)Group.asteroid_rouge_drone_frigate2;
                result |= GroupId == (int)Group.asteroid_angel_cartel_commander_frigate;
                result |= GroupId == (int)Group.asteroid_blood_raiders_commander_frigate;
                result |= GroupId == (int)Group.asteroid_guristas_commander_frigate;
                result |= GroupId == (int)Group.asteroid_sanshas_nation_commander_frigate;
                result |= GroupId == (int)Group.asteroid_serpentis_commander_frigate;
                result |= GroupId == (int)Group.mission_generic_frigates;
                result |= GroupId == (int)Group.mission_thukker_frigate;
                result |= GroupId == (int)Group.asteroid_rouge_drone_commander_frigate;
                return result;
            }
        }

        public bool IsEntityIShouldLeaveAlone
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Merchant;            // Merchant, Convoy?
                result |= GroupId == (int)Group.Mission_Merchant;    // Merchant, Convoy? - Dread Pirate Scarlet
                result |= IsOreOrIce;
                return result;
            }
        }
        
        public bool IsOnGridWithMe
        {
            get
            {
                bool result = false;
                result |= Distance < (double) Distances.OnGridWithMe;
                return result;
            }
        }

        public bool IsStation
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Station;
                return result;
            }
        }

        public bool IsCustomsOffice
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.CustomsOffice;
                return result;
            }
        }

        public bool IsCelestial
        {
            get
            {
                bool result = false;
                result |= CategoryId == (int) CategoryID.Celestial;
                result |= CategoryId == (int) CategoryID.Station;
                result |= GroupId == (int) Group.Moon;
                result |= GroupId == (int) Group.AsteroidBelt;
                return result;
            }
        }

        public bool IsAsteroidBelt
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.AsteroidBelt;
                return result;
            }
        }

        public bool IsPlanet
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Planet;
                return result;
            }
        }

        public bool IsMoon
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Moon;
                return result;
            }
        }

        public bool IsAsteroid
        {
            get
            {
                bool result = false;
                result |= CategoryId == (int)CategoryID.Asteroid;
                return result;
            }
        }

        public bool IsShipWithNoDroneBay
        {
            get
            {
                bool result = false;
                result |= TypeId == (int)TypeID.Tengu;
                result |= GroupId == (int)Group.Shuttle;
                return result;
            }
        }

        public bool IsOreOrIce
        {
            get
            {
                bool result = false;
                result |= GroupId == (int)Group.Plagioclase;
                result |= GroupId == (int)Group.Spodumain;
                result |= GroupId == (int)Group.Kernite;
                result |= GroupId == (int)Group.Hedbergite;
                result |= GroupId == (int)Group.Arkonor;
                result |= GroupId == (int)Group.Bistot;
                result |= GroupId == (int)Group.Pyroxeres;
                result |= GroupId == (int)Group.Crokite;
                result |= GroupId == (int)Group.Jaspet;
                result |= GroupId == (int)Group.Omber;
                result |= GroupId == (int)Group.Scordite;
                result |= GroupId == (int)Group.Gneiss;
                result |= GroupId == (int)Group.Veldspar;
                result |= GroupId == (int)Group.Hemorphite;
                result |= GroupId == (int)Group.DarkOchre;
                result |= GroupId == (int)Group.Ice;
                return result;
            }
        }
        

        public bool LockTarget(string module)
        {
            // If the bad idea is attacking, attack back
            if (IsBadIdea && !IsAttacking)
            {
                Logging.Log("EntityCache.LockTarget", "[" + module + "] Attempted to target a player or concord entity! [" + Name + "] - aborting", Logging.White);
                return false;
            }

            if (Distance >= 250001 || Distance > Cache.Instance.DirectEve.ActiveShip.MaxTargetRange) //250k is the MAX targeting range in eve. 
            {
                Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "] which is [" + Math.Round(Distance / 1000, 2) + "k] away. Do not try to lock things that you cant possibly target", Logging.Debug);
                return false;
            }

            // Remove the target info (its been targeted)
            foreach (EntityCache target in Cache.Instance.Entities.Where(e => e.IsTarget && Cache.Instance.TargetingIDs.ContainsKey(e.Id)))
            {
                Cache.Instance.TargetingIDs.Remove(target.Id);
            }

            if (Cache.Instance.TargetingIDs.ContainsKey(Id))
            {
                DateTime lastTargeted = Cache.Instance.TargetingIDs[Id];

                // Ignore targeting request
                double seconds = DateTime.UtcNow.Subtract(lastTargeted).TotalSeconds;
                if (seconds < 20)
                {
                    Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + Cache.Instance.MaskedID(Id) + "][" + Cache.Instance.Targets.Count() + "] targets already, can retarget in [" + Math.Round(20 - seconds, 0) + "]", Logging.White);
                    return false;
                }
            }

            // Only add targeting id's when its actually being targeted
            if (_directEntity != null)
            {
                if (!IsTarget)
                {
                    if (!HasExploded)
                    {
                        if (Distance < Cache.Instance.DirectEve.ActiveShip.MaxTargetRange)
                        {
                            if (Cache.Instance.Targets.Count() < Cache.Instance.DirectEve.ActiveShip.MaxLockedTargets)
                            {
                                if (!IsTargeting)
                                {
                                    if (Cache.Instance.Entities.Any(i => i.Id == Id))
                                    {
                                        if (_directEntity.LockTarget())
                                        {
                                            Cache.Instance.TargetingIDs[Id] = DateTime.UtcNow;
                                            return true;
                                        }

                                        Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + Cache.Instance.MaskedID(Id) + "][" + Cache.Instance.Targets.Count() + "] targets already, LockTarget failed (unknown reason)", Logging.White);
                                    }

                                    Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + Cache.Instance.MaskedID(Id) + "][" + Cache.Instance.Targets.Count() + "] targets already, LockTarget failed: target was not in Entities List", Logging.White);
                                }

                                Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + Cache.Instance.MaskedID(Id) + "][" + Cache.Instance.Targets.Count() + "] targets already, LockTarget aborted: target is already being targeted", Logging.White);
                            }
                            else
                            {
                                Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + Cache.Instance.MaskedID(Id) + "][" + Cache.Instance.Targets.Count() + "] targets already, we are out of targeting slots!", Logging.White);
                            }
                        }
                        else
                        {
                            Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + Cache.Instance.MaskedID(Id) + "][" + Cache.Instance.Targets.Count() + "] targets already, target is out of range!", Logging.White);
                        }
                    }
                    else
                    {
                        Logging.Log("EntityCache.LockTarget", "[" + module + "] tried to lock [" + Name + "][" + Cache.Instance.Targets.Count() + "] targets already, target is alread dead!", Logging.White);
                    }
                }
                else
                {
                    Logging.Log("EntityCache.LockTarget", "[" + module + "] LockTarget req has been ignored for [" + Name + "][" + Math.Round(Distance / 1000, 2) + "k][" + Cache.Instance.MaskedID(Id) + "][" + Cache.Instance.Targets.Count() + "] targets already, target is already locked!", Logging.White);
                }

                return false;
            }

            return false;
        }

        public bool UnlockTarget(string module)
        {
            if (_directEntity != null)
            {
                //if (Distance > 250001)
                //{
                //    return false;
                //}

                Cache.Instance.TargetingIDs.Remove(Id);

                if (IsTarget)
                {
                    _directEntity.UnlockTarget();
                    return true;
                }
                
                return false;
            }

            return false;
        }

        public void Jump()
        {
            if (_directEntity != null)

                //Cache.Instance._lastDockedorJumping = DateTime.UtcNow;
                _directEntity.Jump();
        }

        public void Activate()
        {
            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextActivateAction)
            {
                _directEntity.Activate();
                Cache.Instance.LastInWarp = DateTime.UtcNow;
                Cache.Instance.NextActivateAction = DateTime.UtcNow.AddSeconds(15);
            }
        }

        public void Approach()
        {
            Cache.Instance.Approaching = this;

            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextApproachAction)
            {
                Cache.Instance.NextApproachAction = DateTime.UtcNow.AddSeconds(Time.Instance.ApproachDelay_seconds);
                _directEntity.Approach();
            }
        }

        public void Approach(int range)
        {
            Cache.Instance.Approaching = this;

            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextApproachAction)
            {
                Cache.Instance.NextApproachAction = DateTime.UtcNow.AddSeconds(Time.Instance.ApproachDelay_seconds);
                _directEntity.KeepAtRange(range);
            }
        }

        public void Orbit(int range)
        {
            Cache.Instance.Approaching = this;

            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextOrbit)
            {
                Cache.Instance.NextOrbit = DateTime.UtcNow.AddSeconds(Time.Instance.OrbitDelay_seconds);
                _directEntity.Orbit(range);
            }
        }

        public void WarpTo()
        {
            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextWarpTo)
            {
                Cache.Instance.LastInWarp = DateTime.UtcNow;
                Cache.Instance.NextWarpTo = DateTime.UtcNow.AddSeconds(Time.Instance.WarptoDelay_seconds);
                _directEntity.WarpTo();
            }
        }

        public void AlignTo()
        {
            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextAlign)
            {
                Cache.Instance.NextAlign = DateTime.UtcNow.AddMinutes(Time.Instance.AlignDelay_minutes);
                _directEntity.AlignTo();
            }
        }

        public void WarpToAndDock()
        {
            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextWarpTo && DateTime.UtcNow > Cache.Instance.NextDockAction)
            {
                Cache.Instance.LastInWarp = DateTime.UtcNow;
                Cache.Instance.NextWarpTo = DateTime.UtcNow.AddSeconds(Time.Instance.WarptoDelay_seconds);
                Cache.Instance.NextDockAction = DateTime.UtcNow.AddSeconds(Time.Instance.DockingDelay_seconds);
                _directEntity.WarpToAndDock();
            }
        }

        public void Dock()
        {
            if (_directEntity != null && DateTime.UtcNow > Cache.Instance.NextDockAction)
            {
                _directEntity.Dock();
                Cache.Instance.NextDockAction = DateTime.UtcNow.AddSeconds(Time.Instance.DockingDelay_seconds);
            }
        }

        public void OpenCargo()
        {
            if (_directEntity != null)
            {
                _directEntity.OpenCargo();
                Cache.Instance.NextOpenCargoAction = DateTime.UtcNow.AddSeconds(2 + Cache.Instance.RandomNumber(1, 3));
            }
        }

        public void MakeActiveTarget()
        {
            if (_directEntity != null)
            {
                if (IsTarget)
                {
                    _directEntity.MakeActiveTarget();    
                }

                return;
            }

            return;
        }
    }
}