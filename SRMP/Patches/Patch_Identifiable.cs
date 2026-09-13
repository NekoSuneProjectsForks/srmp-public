using HarmonyLib;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SRMultiplayer.Patches
{
    [HarmonyPatch(typeof(Identifiable))]
    [HarmonyPatch("OnDestroy")]
    class Identifiable_OnDestroy
    {
        static void Postfix(Identifiable __instance)
        {
            if (!Globals.IsMultiplayer || Globals.HandlePacket || __instance.id == Identifiable.Id.NONE || Identifiable.SCENE_OBJECTS.Contains(__instance.id)) return;

            //Leaving the world destroys every actor in the scene, and each one
            //used to announce itself as destroyed - which wiped the host's world
            //for everyone the moment a single player quit to the menu.
            if (!Globals.GameLoaded) return;

            var netActor = __instance.GetComponent<NetworkActor>();
            if (netActor != null)
            {
                if (Globals.Actors.ContainsKey(netActor.ID))
                {
                    //Only the simulating side may retire an actor. Without this a
                    //client can delete things it never owned, and a scene teardown
                    //becomes a world wipe. Unowned actors are fair game: nobody is
                    //simulating them, so whoever noticed is as good an authority.
                    if (!Globals.IsServer && !netActor.IsLocal && netActor.Owner != 0)
                    {
                        return;
                    }

                    //check if this is an exchange box
                    var exchangeBreakOnImpact = netActor.GetComponentInChildren<ExchangeBreakOnImpact>();
                    if (exchangeBreakOnImpact != null)
                    {
                        //exchange box was found processing it with the ondestroy command instead of just removing it!
                        //netActor.OnDestroyEffect();
                        //Destroyer.DestroyActor(netActor.gameObject, "NetworkHandlerServer.OnActorDestroy");
                    }
                    
                    //make sure the actor still gets cleaned up
                    Globals.Actors.Remove(netActor.ID);
                    
                    new PacketActorDestroy()
                    {
                        ID = netActor.ID
                    }.Send();
                }
            }
        }
    }
}