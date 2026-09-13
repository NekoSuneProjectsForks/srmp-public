using HarmonyLib;
using SRMultiplayer.Server;

namespace SRMultiplayer.Patches
{
    /// <summary>
    /// God mode for an unattended server's host character. A player hosting for
    /// friends from their own client is deliberately unaffected: they are playing
    /// the game, not running one.
    /// </summary>
    internal static class HostGodMode
    {
        /// <summary>
        /// Only true while this process is acting as a headless server.
        /// </summary>
        internal static bool Active
        {
            get { return AutoHost.Instance != null && AutoHost.Instance.IsGodMode; }
        }
    }

    /// <summary>
    /// Blocks ordinary damage: slimes, fall damage, rads.
    /// </summary>
    [HarmonyPatch(typeof(PlayerState), "CanBeDamaged")]
    internal static class PlayerState_CanBeDamaged
    {
        private static bool Prefix(ref bool __result)
        {
            if (!HostGodMode.Active) return true;

            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Blocks death itself. Damage is not the only way the game kills a player:
    /// kill volumes and the out-of-bounds emergency return go straight here
    /// without ever consulting CanBeDamaged, and each one fades the screen,
    /// clears carried ammo and teleports the player back to the ranch. On a
    /// server that is pure disruption, so the host simply never dies.
    /// </summary>
    [HarmonyPatch(typeof(PlayerDeathHandler), "OnDeath")]
    internal static class PlayerDeathHandler_OnDeath
    {
        private static bool Prefix(DeathHandler.Source source)
        {
            if (!HostGodMode.Active) return true;

            SRMP.Log($"[AutoHost] Ignoring host death from {source} (god mode)");
            return false;
        }
    }
}
