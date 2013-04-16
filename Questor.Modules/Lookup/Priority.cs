// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------
namespace Questor.Modules.Lookup
{
    public enum PrimaryWeaponPriority
    {
        Jamming = 1,
        PriorityKillTarget = 2, 
        TrackingDisrupting = 3,
        Neutralizing = 4,
        WarpScrambler = 5,
        TargetPainting = 6,
        Dampening = 7,
        Webbing = 8,
    }

    public enum DronePriority
    {
        WarpScrambler = 0,
        Webbing = 1,
        PriorityKillTarget = 2,
        LowPriorityTarget = 3
    }
}