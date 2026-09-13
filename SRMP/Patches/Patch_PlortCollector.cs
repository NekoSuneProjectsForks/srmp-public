using HarmonyLib;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using System;

namespace SRMultiplayer.Patches
{
    /// <summary>
    /// Centralizes collector ownership checks. During region initialization the
    /// NetworkLandplot/Region link can briefly be missing; in that case the host
    /// is the only safe authority instead of either crashing or letting every
    /// peer run collection independently.
    /// </summary>
    internal static class PlortCollectorAuthority
    {
        public static bool IsAuthoritative(PlortCollector collector)
        {
            if (collector == null || collector.model == null || collector.model.gameObj == null)
                return Globals.IsServer;

            NetworkLandplot netLandplot = collector.model.gameObj.GetComponent<NetworkLandplot>();
            if (netLandplot == null || netLandplot.Region == null)
                return Globals.IsServer;

            return netLandplot.IsLocal;
        }
    }

    [HarmonyPatch(typeof(PlortCollector))]
    [HarmonyPatch("DoCollection")]
    class PlortCollector_DoCollection
    {
        static bool Prefix(PlortCollector __instance)
        {
            if (!Globals.IsMultiplayer)
                return true;

            return PlortCollectorAuthority.IsAuthoritative(__instance);
        }

        static void Postfix(PlortCollector __instance)
        {
            if (!Globals.IsMultiplayer || !PlortCollectorAuthority.IsAuthoritative(__instance))
                return;

            if (__instance == null || __instance.model == null || __instance.model.gameObj == null)
                return;

            LandPlotLocation location = __instance.model.gameObj.GetComponent<LandPlotLocation>();
            if (location == null)
                return;

            new PacketLandPlotCollect()
            {
                ID = location.id,
                collectorNextTime = __instance.model.collectorNextTime,
                endCollectAt = __instance.endCollectAt,
                forceCollectUntil = __instance.forceCollectUntil
            }.Send();
        }
    }

    [HarmonyPatch(typeof(PlortCollector))]
    [HarmonyPatch("StartCollection")]
    class PlortCollector_StartCollection
    {
        static void Prefix(PlortCollector __instance)
        {
            if (!Globals.IsMultiplayer || Globals.HandlePacket ||
                !PlortCollectorAuthority.IsAuthoritative(__instance))
                return;

            if (__instance == null || __instance.model == null || __instance.model.gameObj == null)
                return;

            if (__instance.joints.Count == 0 && __instance.timeDir.HasReached(__instance.forceCollectUntil))
            {
                LandPlotLocation location = __instance.model.gameObj.GetComponent<LandPlotLocation>();
                if (location == null)
                    return;

                new PacketLandPlotStartCollection()
                {
                    ID = location.id
                }.Send();
            }
        }
    }

    /// <summary>
    /// Network handlers use Globals.HandlePacket as a recursion/suppression
    /// guard. The original handlers reset it at the bottom of the method, but
    /// custom-packet early returns and thrown exceptions can bypass that reset.
    /// Harmony finalizers guarantee the flag is cleared on every exit path.
    /// </summary>
    [HarmonyPatch(typeof(NetworkHandlerClient), "HandlePacket")]
    class NetworkHandlerClient_HandlePacketGuard
    {
        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception)
        {
            Globals.HandlePacket = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(NetworkHandlerServer), "HandlePacket")]
    class NetworkHandlerServer_HandlePacketGuard
    {
        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception)
        {
            Globals.HandlePacket = false;
            return __exception;
        }
    }
}
