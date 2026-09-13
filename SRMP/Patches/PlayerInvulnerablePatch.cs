using HarmonyLib;
using SRMultiplayer.Server;

namespace SRMultiplayer.Patches
{
    /// <summary>
    /// Makes the host character unkillable, but only while this process is acting
    /// as an unattended server. A player hosting for friends from their own client
    /// is deliberately unaffected: they are playing the game, not running one.
    /// </summary>
    [HarmonyPatch(typeof(PlayerState), "CanBeDamaged")]
    internal static class PlayerState_CanBeDamaged
    {
        private static bool Prefix(ref bool __result)
        {
            if (AutoHost.Instance == null || !AutoHost.Instance.IsParked)
            {
                return true; //normal game, run the original
            }

            __result = false;
            return false;
        }
    }
}
