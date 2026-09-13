using DG.Tweening;
using Lidgren.Network;
using MonomiPark.SlimeRancher.DataModel;
using MonomiPark.SlimeRancher.Regions;
using SRMultiplayer.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using SRMultiplayer.Packets.ModCompat;
using UnityEngine;

namespace SRMultiplayer.Networking
{
    public static class NetworkHandlerClient
    {
        public static void HandlePacket(PacketType type, NetIncomingMessage im)
        {
            Globals.HandlePacket = true;
            if (!Globals.PacketSize.ContainsKey(type))
                Globals.PacketSize.Add(type, 0);
            Globals.PacketSize[type] += im.LengthBytes;

            if (Globals.CustomPackets.TryGetValue(type, out var packetClass))
            {
                Packet packet = (Packet)packetClass.GetConstructor(new[] { typeof(NetIncomingMessage) })
                    ?.Invoke(new object[] { im });

                if (packet != null)
                    packet.InvokeMethod("HandleClient", packet);
                else
                    SRMP.Log($"Got unhandled packet:  {type}" + Enum.GetName(typeof(PacketType), type));

                return;
            }

            switch (type)
            {
                //Player amimations
                case PacketType.PlayerAnimation: OnPlayerAnimation(new PacketPlayerAnimation(im)); break;

                //Players
                case PacketType.PlayerJoined: OnPlayerJoined(new PacketPlayerJoined(im)); break;
                case PacketType.PlayerLeft: OnPlayerLeft(new PacketPlayerLeft(im)); break;
                case PacketType.PlayerLoaded: OnPlayerLoaded(new PacketPlayerLoaded(im)); break;
                case PacketType.PlayerPosition: OnPlayerPosition(new PacketPlayerPosition(im)); break;
                case PacketType.PlayerFX: OnPlayerFX(new PacketPlayerFX(im)); break;
                case PacketType.PlayerCurrency: OnPlayerCurrency(new PacketPlayerCurrency(im)); break;
                case PacketType.PlayerCurrencyDisplay:
                    OnPlayerCurrencyDisplay(new PacketPlayerCurrencyDisplay(im)); break;
                case PacketType.PlayerUpgrade: OnPlayerUpgrade(new PacketPlayerUpgrade(im)); break;
                case PacketType.PlayerUpgradeUnlock: OnPlayerUpgradeUnlock(new PacketPlayerUpgradeUnlock(im)); break;
                case PacketType.PlayerChat: OnPlayerChat(new PacketPlayerChat(im)); break;
                case PacketType.KickPlayer: OnPlayerKicked(new PacketKickClient(im)); break;

                // Region
                case PacketType.RegionOwner: OnRegionOwner(new PacketRegionOwner(im)); break;

                //Actors
                case PacketType.Actors: OnActors(new PacketActors(im)); break;
                case PacketType.ActorSpawn: OnActorSpawn(new PacketActorSpawn(im)); break;
                case PacketType.ActorDestroy: OnActorDestroy(new PacketActorDestroy(im)); break;
                case PacketType.ActorOwner: OnActorOwner(new PacketActorOwner(im)); break;
                case PacketType.ActorPosition: OnActorPosition(new PacketActorPosition(im)); break;
                case PacketType.ActorResourceAttach: OnActorResourceAttach(new PacketActorResourceAttach(im)); break;
                case PacketType.ActorResourceState: OnActorResourceState(new PacketActorResourceState(im)); break;
                case PacketType.ActorReproduceTime: OnActorReproduceTime(new PacketActorReproduceTime(im)); break;
                case PacketType.ActorEmotions: OnActorEmotions(new PacketActorEmotions(im)); break;
                case PacketType.ActorFeral: OnActorFeral(new PacketActorFeral(im)); break;
                case PacketType.ActorFX: OnActorFX(new PacketActorFX(im)); break;

                //LandPlots
                case PacketType.LandPlots: OnLandPlots(new PacketLandplots(im)); break;
                case PacketType.LandPlotSiloInsert: OnLandPlotSiloInsert(new PacketLandPlotSiloInsert(im)); break;
                case PacketType.LandPlotSiloRemove: OnLandPlotSiloRemove(new PacketLandPlotSiloRemove(im)); break;
                case PacketType.LandPlotAsh: OnLandPlotAsh(new PacketLandPlotAsh(im)); break;
                case PacketType.LandPlotSiloSlot: OnLandPlotSiloSlot(new PacketLandPlotSiloSlot(im)); break;
                case PacketType.LandPlotFeederSpeed: OnLandPlotFeederSpeed(new PacketLandPlotFeederSpeed(im)); break;
                case PacketType.LandPlotCollect: OnLandPlotCollect(new PacketLandPlotCollect(im)); break;
                case PacketType.LandPlotReplace: OnLandPlotReplace(new PacketLandPlotReplace(im)); break;
                case PacketType.LandPlotUpgrade: OnLandPlotUpgrade(new PacketLandPlotUpgrade(im)); break;
                case PacketType.LandPlotPlantGarden: OnLandPlotPlantGarden(new PacketLandPlotPlantGarden(im)); break;
                case PacketType.LandPlotStartCollection:
                    OnLandPlotStartCollection(new PacketLandPlotStartCollection(im)); break;
                case PacketType.LandPlotSiloAmmoAdd: OnLandPlotSiloAmmoAdd(new PacketLandPlotSiloAmmoAdd(im)); break;
                case PacketType.LandPlotSiloAmmoRemove:
                    OnLandPlotSiloAmmoRemove(new PacketLandPlotSiloAmmoRemove(im)); break;
                case PacketType.LandPlotSiloAmmoClear:
                    OnLandPlotSiloAmmoClear(new PacketLandPlotSiloAmmoClear(im)); break;

                //World
                case PacketType.WorldData: OnWorldData(new PacketWorldData(im)); break;
                case PacketType.WorldTime: OnWorldTime(new PacketWorldTime(im)); break;
                case PacketType.Pong: OnPong(new PacketPong(im)); break;
                case PacketType.PlayerPings: OnPlayerPings(new PacketPlayerPings(im)); break;
                case PacketType.WorldFastForward: OnWorldFastForward(new PacketWorldFastForward(im)); break;
                case PacketType.WorldProgress: OnWorldProgress(new PacketWorldProgress(im)); break;
                case PacketType.WorldKey: OnWorldKey(new PacketWorldKey(im)); break;
                case PacketType.WorldMapUnlock: OnWorldMapUnlock(new PacketWorldMapUnlock(im)); break;
                case PacketType.WorldSwitchActivate: OnWorldSwitchActivate(new PacketWorldSwitchActivate(im)); break;
                case PacketType.WorldSelectPalette: OnWorldSelectPalette(new PacketWorldSelectPalette(im)); break;
                case PacketType.WorldSwitches: OnWorldSwitches(new PacketWorldSwitches(im)); break;
                case PacketType.WorldDecorizer: OnWorldDecorizer(new PacketWorldDecorizer(im)); break;
                case PacketType.WorldDecorizerSetting:
                    OnWorldDecorizerSetting(new PacketWorldDecorizerSetting(im)); break;
                case PacketType.WorldMarketPrices: OnWorldMarketPrices(new PacketWorldMarketPrices(im)); break;
                case PacketType.WorldCredits: OnWorldCredits(new PacketWorldCredits(im)); break;
                case PacketType.WorldMailSend: OnWorldMailSend(new PacketWorldMailSend(im)); break;
                case PacketType.WorldMailRead: OnWorldMailRead(new PacketWorldMailRead(im)); break;

                //FX
                case PacketType.GlobalFX: OnGlobalFX(new PacketGlobalFX(im)); break;
                case PacketType.IncinerateFX: OnIncinerateFX(new PacketIncinerateFX(im)); break;
                case PacketType.PlayAudio: OnPlayAudio(new PacketPlayAudio(im)); break;

                //AccessDoors
                case PacketType.AccessDoors: OnAccessDoors(new PacketAccessDoors(im)); break;
                case PacketType.AccessDoorOpen: OnAccessDoorOpen(new PacketAccessDoorOpen(im)); break;

                //Gordos
                case PacketType.Gordos: OnGordos(new PacketGordos(im)); break;
                case PacketType.GordoEat: OnGordoEat(new PacketGordoEat(im)); break;

                //PuzzleSlots
                case PacketType.PuzzleSlots: OnPuzzleSlots(new PacketPuzzleSlots(im)); break;
                case PacketType.PuzzleSlotFilled: OnPuzzleSlotFilled(new PacketPuzzleSlotFilled(im)); break;
                case PacketType.PuzzleGateActivate: OnPuzzleGateActivate(new PacketPuzzleGateActivate(im)); break;

                //Pedia
                case PacketType.PediaShowPopup: OnPediaShowPopup(new PacketPediaShowPopup(im)); break;
                case PacketType.PediaUnlock: OnPediaUnlock(new PacketPediaUnlock(im)); break;

                //gadgets
                case PacketType.Gadgets: OnGadgets(new PacketGadgets(im)); break;
                case PacketType.GadgetRemove: OnGadgetRemove(new PacketGadgetRemove(im)); break;
                case PacketType.GadgetRotation: OnGadgetRotation(new PacketGadgetRotation(im)); break;
                case PacketType.GadgetRefinerySpend: OnGadgetRefinerySpend(new PacketGadgetRefinerySpend(im)); break;
                case PacketType.GadgetAdd: OnGadgetAdd(new PacketGadgetAdd(im)); break;
                case PacketType.GadgetAddBlueprint: OnGadgetAddBlueprint(new PacketGadgetAddBlueprint(im)); break;
                case PacketType.GadgetSpend: OnGadgetSpend(new PacketGadgetSpend(im)); break;
                case PacketType.GadgetSpawn: OnGadgetSpawn(new PacketGadgetSpawn(im)); break;
                case PacketType.GadgetExtractorUpdate:
                    OnGadgetExtractorUpdate(new PacketGadgetExtractorUpdate(im)); break;
                case PacketType.GadgetTurrets: OnGadgetTurrets(new PacketGadgetTurrets(im)); break;
                case PacketType.GadgetSnareAttach: OnGadgetSnareAttach(new PacketGadgetSnareAttach(im)); break;
                case PacketType.GadgetSnareGordo: OnGadgetSnareGordo(new PacketGadgetSnareGordo(im)); break;
                case PacketType.GadgetEchoNetTime: OnGadgetEchoNetTime(new PacketGadgetEchoNetTime(im)); break;

                //Fashion
                case PacketType.FashionAttach: OnFashionAttach(new PacketFashionAttach(im)); break;
                case PacketType.FashionDetachAll: OnFashionDetachAll(new PacketFashionDetachAll(im)); break;

                //Drone
                case PacketType.DroneAmmoAdd: OnDroneAmmoAdd(new PacketDroneAmmoAdd(im)); break;
                case PacketType.DroneAmmoClear: OnDroneAmmoClear(new PacketDroneAmmoClear(im)); break;
                case PacketType.DroneAmmoRemove: OnDroneAmmoRemove(new PacketDroneAmmoRemove(im)); break;
                case PacketType.DroneAnimation: OnDroneAnimation(new PacketDroneAnimation(im)); break;
                case PacketType.DronePrograms: OnDronePrograms(new PacketDronePrograms(im)); break;
                case PacketType.DroneLiquid: OnDroneLiquid(new PacketDroneLiquid(im)); break;
                case PacketType.DroneStationEnabled: OnDroneStationEnabled(new PacketDroneStationEnabled(im)); break;
                case PacketType.DronePosition: OnDronePosition(new PacketDronePosition(im)); break;
                case PacketType.DroneActive: OnDroneActive(new PacketDroneActive(im)); break;

                //TreasurePod
                case PacketType.TreasurePods: OnTreasurePods(new PacketTreasurePods(im)); break;
                case PacketType.TreasurePodOpen: OnTreasurePodOpen(new PacketTreasurePodOpen(im)); break;

                //Exchanges
                case PacketType.ExchangeOffers: OnExchangeOffers(new PacketExchangeOffers(im)); break;
                case PacketType.ExchangeOffer: OnExchangeOffer(new PacketExchangeOffer(im)); break;
                case PacketType.ExchangeClear: OnExchangeClear(new PacketExchangeClear(im)); break;
                case PacketType.ExchangePrepareDaily: OnExchangePrepareDaily(new PacketExchangePrepareDaily(im)); break;
                case PacketType.ExchangeTryAccept: OnExchangeTryAccept(new PacketExchangeTryAccept(im)); break;

                //Oasis
                case PacketType.Oasis: OnOasis(new PacketOasis(im)); break;
                case PacketType.OasisLive: OnOasisLive(new PacketOasisLive(im)); break;

                //FireColumns
                case PacketType.FireColumnActivate: OnFireColumnActivate(new PacketFireColumnActivate(im)); break;
                case PacketType.FireStormMode: OnFireStormMode(new PacketFireStormMode(im)); break;

                //Others
                case PacketType.GingerAction: OnGingerAction(new PacketGingerAction(im)); break;
                case PacketType.KookadobaAction: OnKookadobaAction(new PacketKookadobaAction(im)); break;
                case PacketType.GingerAttach: OnGingerAttach(new PacketGingerAttach(im)); break;
                case PacketType.KookadobaAttach: OnKookadobaAttach(new PacketKookadobaAttach(im)); break;

                // Race
                case PacketType.RaceActivate: OnRaceActivate(new PacketRaceActivate(im)); break;
                case PacketType.RaceEnd: OnRaceEnd(new PacketRaceEnd(im)); break;
                case PacketType.RaceTime: OnRaceTime(new PacketRaceTime(im)); break;
                case PacketType.RaceTrigger: OnRaceTrigger(new PacketRaceTrigger(im)); break;

#if SRML
                // Mod Compatibility
                case PacketType.SetNickname: OnSetNickname(new PacketSetNickname(im)); break;
                case PacketType.VRPositions: OnVRPositions(new PacketVRPositions(im)); break;
#endif

                default:
                    SRMP.Log($"Got unhandled packet: {type} " + Enum.GetName(typeof(PacketType), type));
                    break;
            }

            Globals.HandlePacket = false;
        }

        #region Race

        private static void OnRaceTrigger(PacketRaceTrigger packet)
        {
            if (Globals.RaceTriggers.TryGetValue(packet.ID, out NetworkRaceTrigger trigger))
            {
                trigger.Activate();
            }
        }

        private static void OnRaceTime(PacketRaceTime packet)
        {
            var generator = QuicksilverEnergyGenerator.allGenerators.FirstOrDefault(g => g.id == packet.ID);
            if (generator)
            {
                generator.ExtendActiveDuration(packet.Time);
            }
        }

        private static void OnRaceEnd(PacketRaceEnd packet)
        {
            var generator = QuicksilverEnergyGenerator.allGenerators.FirstOrDefault(g => g.id == packet.ID);
            if (generator)
            {
                generator.SetState(QuicksilverEnergyGenerator.State.COOLDOWN,
                    Globals.LocalPlayer.CurrentGenerator != null &&
                    Globals.LocalPlayer.CurrentGenerator.id == generator.id);
            }
        }

        private static void OnRaceActivate(PacketRaceActivate packet)
        {
            var generator = QuicksilverEnergyGenerator.allGenerators.FirstOrDefault(g => g.id == packet.ID);
            if (generator)
            {
                if (Globals.LocalPlayer.CurrentGenerator != null &&
                    Globals.LocalPlayer.CurrentGenerator.id == generator.id)
                {
                    generator.Activate();
                }
                else
                {
                    generator.model.state = QuicksilverEnergyGenerator.State.COUNTDOWN;
                    generator.model.timer =
                        new double?(generator.timeDirector.HoursFromNow(generator.countdownMinutes * 0.0166666675f));
                }
            }
        }

        #endregion

        #region Others

        private static void OnKookadobaAction(PacketKookadobaAction packet)
        {
            if (Globals.Kookadobas.TryGetValue(packet.ID, out NetworkKookadobaPatchNode node))
            {
                if (packet.Grow)
                {
                    node.Node.bed.SetActive(true);
                    node.Node.spawnJoint.gameObject.SetActive(true);
                    SRMP.Log($"Kookadoba Grow {packet.ID}", "CLIENT");
                }
                else if (packet.Harvest)
                {
                    node.Node.Harvested();
                    SRMP.Log($"Kokadoba Harvested {packet.ID}", "CLIENT");
                }
            }
        }

        private static void OnGingerAction(PacketGingerAction packet)
        {
            var node = GingerPatchNode.allGingerPatches.FirstOrDefault(g => g.id == packet.ID);
            if (node != null)
            {
                if (packet.Grow)
                {
                    node.bed.SetActive(true);
                    node.spawnJoint.gameObject.SetActive(true);
                    SRMP.Log($"Ginger Grow {packet.ID}", "CLIENT");
                }
                else if (packet.Harvest)
                {
                    node.Harvested();
                    SRMP.Log($"Ginger Harvested {packet.ID}", "CLIENT");
                }
                else
                {
                    node.HidePatchAndReset();
                    SRMP.Log($"Ginger HidePatchAndReset {packet.ID}", "CLIENT");
                }
            }
        }

        private static void OnGingerAttach(PacketGingerAttach packet)
        {
            var node = GingerPatchNode.allGingerPatches.FirstOrDefault(g => g.id == packet.ID);
            if (node != null && Globals.Actors.TryGetValue(packet.ActorID, out NetworkActor actor))
            {
                actor.GetComponent<ResourceCycle>().Attach(node.spawnJoint, null,
                    new ResourceCycle.DetachmentEvent(node.Harvested));
                SRMP.Log($"Ginger {packet.ActorID} attached to {packet.ID}", "CLIENT");
            }
        }

        private static void OnKookadobaAttach(PacketKookadobaAttach packet)
        {
            if (Globals.Kookadobas.TryGetValue(packet.ID, out NetworkKookadobaPatchNode node) &&
                Globals.Actors.TryGetValue(packet.ActorID, out NetworkActor actor))
            {
                actor.GetComponent<ResourceCycle>().Attach(node.Node.spawnJoint, null,
                    new ResourceCycle.DetachmentEvent(node.Node.Harvested));
                SRMP.Log($"Kookadoba {packet.ActorID} attached to {packet.ID}", "CLIENT");
            }
        }

        #endregion

        #region FireColumns

        private static void OnFireStormMode(PacketFireStormMode packet)
        {
            SRSingleton<SceneContext>.Instance.GameModel.world.currFirestormMode = (FirestormActivator.Mode)packet.Mode;
        }

        private static void OnFireColumnActivate(PacketFireColumnActivate packet)
        {
            if (Globals.FireColumns.TryGetValue(packet.ID, out NetworkFireColumn netColumn))
            {
                netColumn.Column.ActivateFire();
            }
        }

        #endregion

        #region Oasis

        private static void OnOasisLive(PacketOasisLive packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllOases().TryGetValue(packet.ID, out OasisModel model))
            {
                if (model.gameObj != null)
                {
                    var oasis = model.gameObj.GetComponent<Oasis>();
                    oasis.SetLive(!model.gameObj.activeInHierarchy);

                    var oasisTriggers = GameObject.FindObjectsOfType<OasisWaterTrigger>();
                    foreach (var trigger in oasisTriggers)
                    {
                        if (trigger.oasisToScale == oasis && !trigger.hasAlreadyActivated)
                        {
                            if (trigger.scaleCue != null)
                            {
                                SECTR_AudioSystem.Play(trigger.scaleCue, trigger.transform.position, false);
                            }

                            if (trigger.scaleFX != null)
                            {
                                SRBehaviour.InstantiateDynamic(trigger.scaleFX, trigger.transform.position,
                                    trigger.transform.rotation, false);
                            }

                            if (trigger.indicatorObj != null)
                            {
                                trigger.indicatorObj.SetActive(true);
                            }

                            if (trigger.indicatorReplacesObj != null)
                            {
                                trigger.indicatorReplacesObj.SetActive(false);
                            }

                            trigger.hasAlreadyActivated = true;
                        }
                    }
                }
            }
        }

        private static void OnOasis(PacketOasis packet)
        {
            foreach (var oasisData in packet.Oasis)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllOases()
                    .TryGetValue(oasisData.ID, out OasisModel model))
                {
                    model.isLive = oasisData.Model.isLive;

                    model.Init();
                    model.NotifyParticipants();
                }
            }
        }

        #endregion

        #region Exchanges

        private static void OnExchangeTryAccept(PacketExchangeTryAccept packet)
        {
            //get the exchange type
            var type = (ExchangeDirector.OfferType)packet.Type;
            //check if current scene (view) contains the item in question
            if (SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers.ContainsKey(type))
            {
                //handle the scene changes for the given offer
                var offer = SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers[type];
                //cycle through requested items 
                foreach (ExchangeDirector.RequestedItemEntry requestedItemEntry in offer.requests)
                {
                    //check if the item can be accespted 
                    //is on the board and not already completed
                    if (requestedItemEntry.id == (Identifiable.Id)packet.ID && !requestedItemEntry.IsComplete())
                    {
                        //mark submit to log
                        SRMP.Log(
                            $"Exchange TryAccept for {(Identifiable.Id)packet.ID} ({(ExchangeDirector.OfferType)packet.Type}",
                            "SERVER");
                        //mark progress
                        requestedItemEntry.progress++;

                        //if the given item completes the necesary quantity
                        if (offer.IsComplete())
                        {
                            foreach (var rewarder in Resources.FindObjectsOfTypeAll<RancherProgressAwarder>())
                            {
                                rewarder.AwardIfType(type);
                            }

                            //trigger fireworks
                            //get ExchangeEjector
                            foreach (var eject in Resources.FindObjectsOfTypeAll<ExchangeEjector>())
                            {
                                //send off fireworks 
                                SRBehaviour.InstantiateDynamic(eject.awardFX, eject.awardAt.position,
                                    eject.awardAt.rotation);
                            }

                            //dont clear out the offer yet, we arent done with it
                            //SRSingleton<SceneContext>.Instance.ExchangeDirector.ClearOffer(type);
                        }

                        //trigger offer status changed
                        SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();
                    }
                }
            }
        }

        private static void OnExchangePrepareDaily(PacketExchangePrepareDaily packet)
        {
            SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.pendingOfferRancherIds =
                packet.pendingOfferRancherIds;
            SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();

            SRMP.Log($"ExchangePrepareDaily", "CLIENT");
        }

        private static void OnExchangeClear(PacketExchangeClear packet)
        {
            SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers.Remove(
                (ExchangeDirector.OfferType)packet.Type);
            SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();

            SRMP.Log($"ExchangeClear", "CLIENT");
        }

        private static void OnExchangeOffer(PacketExchangeOffer packet)
        {
            if ((ExchangeDirector.OfferType)packet.Type == ExchangeDirector.OfferType.GENERAL)
            {
                SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.pendingOfferRancherIds.Clear();
            }

            SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers[
                (ExchangeDirector.OfferType)packet.Type] = packet.Offer;
            SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();

            SRMP.Log($"ExchangeOffer for {(ExchangeDirector.OfferType)packet.Type}", "CLIENT");
        }

        private static void OnExchangeOffers(PacketExchangeOffers packet)
        {
            SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.pendingOfferRancherIds =
                packet.pendingOfferRancherIds;
            SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();
            foreach (var offerData in packet.Offers)
            {
                SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers[
                    (ExchangeDirector.OfferType)offerData.Type] = offerData.Offer;
                SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();
            }
        }

        #endregion

        #region TreasurePods

        private static void OnTreasurePodOpen(PacketTreasurePodOpen packet)
        {
            if (Globals.TreasurePods.TryGetValue(packet.ID, out NetworkTreasurePod netTreasurePod))
            {
                netTreasurePod.Activate();
                SRMP.Log($"TreasurePot {packet.ID} activated", "CLIENT");
            }
        }

        private static void OnTreasurePods(PacketTreasurePods packet)
        {
            foreach (var pod in packet.TreasurePods)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllPods()
                    .TryGetValue(pod.ID, out TreasurePodModel model))
                {
                    model.state = pod.Model.state;

                    model.Init();
                    model.NotifyParticipants();
                }
            }
        }

        #endregion

        #region Drones

        private static void OnDroneActive(PacketDroneActive packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.onActiveCue.enabled = packet.Enabled;
                }
            }
        }

        private static void OnDronePosition(PacketDronePosition packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    var netDrone = model.attached.transform.GetComponentInChildren<NetworkDrone>(true);
                    if (netDrone != null)
                    {
                        netDrone.PositionRotationUpdate(packet.Position, packet.Rotation, false);
                    }
                }
            }
        }

        private static void OnDroneStationEnabled(PacketDroneStationEnabled packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().station.animator.SetEnabled(packet.Enabled);
                }
            }
        }

        private static void OnDroneLiquid(PacketDroneLiquid packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    if (model.attached.transform.gameObject.activeInHierarchy)
                    {
                        model.attached.transform.GetComponent<DroneGadget>().station.battery
                            .AddLiquid(Identifiable.Id.NONE, 0);
                    }
                    else
                    {
                        model.attached.transform.GetComponent<DroneGadget>().station.battery
                            .Reset(model.attached.transform.GetComponent<DroneGadget>().droneModel);
                    }
                }
            }
        }

        private static void OnDronePrograms(PacketDronePrograms packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    var drone = model.attached.transform.GetComponent<DroneGadget>();
                    drone.SetPrograms(drone.ProgramsFromData(packet.Programs));
                }
            }
        }

        private static void OnDroneAnimation(PacketDroneAnimation packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.animator
                        .SetAnimation((DroneAnimator.Id)packet.Anim);
                }
            }
        }

        private static void OnDroneAmmoRemove(PacketDroneAmmoRemove packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.ammo.Decrement(0, 1);
                }
            }
        }

        private static void OnDroneAmmoClear(PacketDroneAmmoClear packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.ammo.Clear();
                }
            }
        }

        private static void OnDroneAmmoAdd(PacketDroneAmmoAdd packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.ammo
                        .MaybeAddToSpecificSlot((Identifiable.Id)packet.Ident, null, 0, 1);
                }
            }
        }

        #endregion

        #region Fashions

        private static void OnFashionDetachAll(PacketFashionDetachAll packet)
        {
            AttachFashions attachFashions = null;
            if (packet.Type == 1)
            {
                if (Globals.Actors.TryGetValue(packet.IDInt, out NetworkActor netActor))
                {
                    attachFashions = netActor.GetComponentInChildren<AttachFashions>(true);
                }
            }
            else if (packet.Type == 3)
            {
                if (Globals.Gordos.TryGetValue(packet.IDString, out NetworkGordo netGordo))
                {
                    attachFashions = netGordo.GetComponentInChildren<AttachFashions>(true);
                }
            }
            else if (packet.Type == 4)
            {
                if (Globals.GadgetSites.TryGetValue(packet.IDString, out NetworkGadgetSite netGadgetSite))
                {
                    attachFashions = netGadgetSite.GetComponentInChildren<AttachFashions>(true);
                }
            }

            if (attachFashions != null)
            {
                var fashionRemover = Resources.FindObjectsOfTypeAll<FashionRemover>()[0];
                fashionRemover.transform.position = packet.Position;
                fashionRemover.transform.rotation = packet.Rotation;
                attachFashions.DetachAll(fashionRemover);
            }
        }

        private static void OnFashionAttach(PacketFashionAttach packet)
        {
            AttachFashions attachFashions = null;
            if (packet.Type == 1)
            {
                if (Globals.Actors.TryGetValue(packet.IDInt, out NetworkActor netActor))
                {
                    attachFashions = netActor.GetComponentInChildren<AttachFashions>(true);
                }
            }
            else if (packet.Type == 3)
            {
                if (Globals.Gordos.TryGetValue(packet.IDString, out NetworkGordo netGordo))
                {
                    attachFashions = netGordo.GetComponentInChildren<AttachFashions>(true);
                }
            }
            else if (packet.Type == 4)
            {
                if (Globals.GadgetSites.TryGetValue(packet.IDString, out NetworkGadgetSite netGadgetSite))
                {
                    attachFashions = netGadgetSite.GetComponentInChildren<AttachFashions>(true);
                }
            }

            if (attachFashions != null)
            {
                var component = SRSingleton<GameContext>.Instance.LookupDirector
                    .GetPrefab((Identifiable.Id)packet.Fashion)?.GetComponent<Fashion>();
                if (component != null)
                {
                    attachFashions.Attach(component, !attachFashions.gameObject.activeInHierarchy);
                }
            }
        }

        #endregion

        #region Gadgets

        private static void OnGadgetEchoNetTime(PacketGadgetEchoNetTime packet)
        {
            if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
            {
                var echoNet = netSite.Site.GetComponentInChildren<EchoNet>(true);
                if (echoNet != null)
                {
                    echoNet.ResetSpawnTime(echoNet.model);
                }
            }
        }

        private static void OnGadgetSnareGordo(PacketGadgetSnareGordo packet)
        {
            if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
            {
                var snare = netSite.Site.GetComponentInChildren<GordoSnare>(true);
                if (snare != null)
                {
                    if (!snare.IsBaited() || snare.HasSnaredGordo())
                    {
                        return;
                    }

                    snare.SnareGordo((Identifiable.Id)packet.Ident);
                }
            }
        }

        private static void OnGadgetSnareAttach(PacketGadgetSnareAttach packet)
        {
            if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
            {
                var snare = netSite.Site.GetComponentInChildren<GordoSnare>(true);
                if (snare != null)
                {
                    snare.AttachBait((Identifiable.Id)packet.Ident);
                }
            }
        }

        private static void OnGadgetTurrets(PacketGadgetTurrets packet)
        {
            if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
            {
                foreach (var data in packet.Turrets)
                {
                    netSite.UpdateTurretRotation(data.Index, data.Rotation);
                }
            }
        }

        private static void OnGadgetExtractorUpdate(PacketGadgetExtractorUpdate packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model != null && model.attached != null && model.attached is ExtractorModel)
                {
                    var extractor = (ExtractorModel)model.attached;

                    extractor.cycleEndTime = packet.cycleEndTime;
                    extractor.cyclesRemaining = packet.cyclesRemaining;
                    extractor.nextProduceTime = packet.nextProduceTime;
                    extractor.queuedToProduce = packet.queuedToProduce;

                    if (extractor.cyclesRemaining <= 0)
                    {
                        var extractorScript = extractor.transform.GetComponent<Extractor>();
                        if (extractorScript != null && extractorScript.gameObject.activeInHierarchy)
                        {
                            extractorScript.StartCoroutine(extractorScript.AnimAndDestroy());
                        }
                    }
                }
            }
        }

        private static void OnGadgetSpawn(PacketGadgetSpawn packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite);
                bool didUnproxy = false;
                if (netSite != null)
                {
                    if (!netSite.Region.Region.root.activeSelf)
                    {
                        netSite.Region.Region.Unproxy();
                        didUnproxy = true;
                    }
                }

                GameObject prefab = SRSingleton<GameContext>.Instance.LookupDirector
                    .GetGadgetDefinition((Gadget.Id)packet.Ident).prefab;
                var gadgetObj = SRSingleton<SceneContext>.Instance.GameModel.InstantiateGadget(prefab, model);
                gadgetObj.transform.eulerAngles = new Vector3(0, packet.Rotation, 0);

                GameObject.FindObjectOfType<PlaceGadgetUI>()?.RebuildUI();

                if (didUnproxy)
                {
                    netSite.Region.Region.Proxy();
                }
            }
        }

        private static void OnGadgetSpend(PacketGadgetSpend packet)
        {
            SRSingleton<SceneContext>.Instance.GadgetDirector.SpendGadget((Gadget.Id)packet.ID);
            GameObject.FindObjectOfType<PlaceGadgetUI>()?.RebuildUI();
        }

        private static void OnGadgetAddBlueprint(PacketGadgetAddBlueprint packet)
        {
            SRSingleton<SceneContext>.Instance.GadgetDirector.AddBlueprint((Gadget.Id)packet.ID);
        }

        private static void OnGadgetAdd(PacketGadgetAdd packet)
        {
            SRSingleton<SceneContext>.Instance.GadgetDirector.AddGadget((Gadget.Id)packet.ID);
            GameObject.FindObjectOfType<PlaceGadgetUI>()?.RebuildUI();
        }

        private static void OnGadgetRefinerySpend(PacketGadgetRefinerySpend packet)
        {
            foreach (var data in packet.Amounts)
            {
                SRSingleton<SceneContext>.Instance.GameModel.GetGadgetsModel()
                    .craftMatCounts[(Identifiable.Id)data.Key] -= data.Value;
                if (SRSingleton<SceneContext>.Instance.GameModel.GetGadgetsModel()
                        .craftMatCounts[(Identifiable.Id)data.Key] < 0)
                {
                    SRSingleton<SceneContext>.Instance.GameModel.GetGadgetsModel()
                        .craftMatCounts[(Identifiable.Id)data.Key] = 0;
                }
            }

            GameObject.FindObjectOfType<RefineryUI>()?.Rebuild();
        }

        private static void OnGadgetRotation(PacketGadgetRotation packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<Gadget>().SetRotation(packet.Rotation);
                }
            }
        }

        private static void OnGadgetRemove(PacketGadgetRemove packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                .TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.transform.GetComponent<GadgetSite>().DestroyAttached();
                    GameObject.FindObjectOfType<PlaceGadgetUI>()?.RebuildUI();
                }
            }
        }

        private static void OnGadgets(PacketGadgets packet)
        {
            List<int> regions = new List<int>();
            foreach (var gadgetData in packet.Gadgets)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites()
                    .TryGetValue(gadgetData.ID, out GadgetSiteModel model))
                {
                    if (Globals.GadgetSites.TryGetValue(gadgetData.ID, out NetworkGadgetSite netSite))
                    {
                        if (!netSite.Region.Region.root.activeSelf && !regions.Contains(netSite.Region.ID))
                        {
                            netSite.Region.Region.Unproxy();
                            regions.Add(netSite.Region.ID);
                        }
                    }

                    GameObject gameObj =
                        SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.prefabInstantiator
                            .InstantiateGadget(gadgetData.gadgetId, model,
                                SRSingleton<SceneContext>.Instance.GameModel);
                    GadgetModel attached = model.attached;
                    attached.PushBase(gadgetData.waitForChargeupTime, gadgetData.yRotation);
                    if (attached is ExtractorModel)
                    {
                        ((ExtractorModel)attached).cyclesRemaining = ((ExtractorModel)gadgetData.Model).cyclesRemaining;
                        ((ExtractorModel)attached).queuedToProduce = ((ExtractorModel)gadgetData.Model).queuedToProduce;
                        ((ExtractorModel)attached).cycleEndTime = ((ExtractorModel)gadgetData.Model).cycleEndTime;
                        ((ExtractorModel)attached).cyclesRemaining = ((ExtractorModel)gadgetData.Model).cyclesRemaining;
                    }
                    else if (attached is WarpDepotModel)
                    {
                        ((WarpDepotModel)attached).Push(((WarpDepotModel)gadgetData.Model).isPrimary,
                            ((WarpDepotModel)gadgetData.Model).ammo.slots);
                    }
                    else if (attached is SnareModel)
                    {
                        ((SnareModel)attached).Push(((SnareModel)gadgetData.Model).baitTypeId,
                            ((SnareModel)gadgetData.Model).gordoTypeId, ((SnareModel)gadgetData.Model).gordoEatenCount,
                            ((SnareModel)gadgetData.Model).fashions);
                    }
                    else if (attached is EchoNetModel)
                    {
                        ((EchoNetModel)attached).Push(((EchoNetModel)gadgetData.Model).lastSpawnTime);
                    }
                    else if (attached is DroneModel)
                    {
                        ((DroneModel)attached).position = ((DroneModel)gadgetData.Model).position;
                        ((DroneModel)attached).rotation = ((DroneModel)gadgetData.Model).rotation;
                        ((DroneModel)attached).ammo.Push(((DroneModel)gadgetData.Model).ammo.slots);
                        ((DroneModel)attached).fashions = ((DroneModel)gadgetData.Model).fashions;
                        ((DroneModel)attached).noClip = ((DroneModel)gadgetData.Model).noClip;
                        ((DroneModel)attached).batteryDepleteTime = ((DroneModel)gadgetData.Model).batteryDepleteTime;
                        ((DroneModel)attached).programs = ((DroneModel)gadgetData.Model).programs;
                    }
                    else
                    {
                        BasicGadgetModel basicGadgetModel = attached as BasicGadgetModel;
                    }

                    attached.NotifyParticipants(gameObj);
                }
            }

            foreach (var region in regions)
            {
                if (Globals.Regions.TryGetValue(region, out NetworkRegion netRegion))
                {
                    netRegion.Region.Proxy();
                }
            }
        }

        #endregion

        #region Pedia

        private static void OnPediaUnlock(PacketPediaUnlock packet)
        {
            SRSingleton<SceneContext>.Instance.PediaDirector.Unlock(packet.IDs.Select(i => (PediaDirector.Id)i)
                .ToArray());
        }

        private static void OnPediaShowPopup(PacketPediaShowPopup packet)
        {
            SRSingleton<SceneContext>.Instance.PediaDirector.MaybeShowPopup((PediaDirector.Id)packet.ID);
        }

        #endregion

        #region PuzzleSlots

        private static void OnPuzzleGateActivate(PacketPuzzleGateActivate packet)
        {
            var puzzleGateActivator = GameObject.FindObjectOfType<PuzzleGateActivator>();
            if (puzzleGateActivator != null && puzzleGateActivator.gameObject.activeInHierarchy)
            {
                puzzleGateActivator.StartCoroutine(puzzleGateActivator.DoDeactivateSequence());
            }
        }

        private static void OnPuzzleSlotFilled(PacketPuzzleSlotFilled packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllSlots()
                .TryGetValue(packet.ID, out PuzzleSlotModel model))
            {
                if (!model.filled)
                {
                    model.filled = true;
                    var slot = model.gameObj.GetComponent<PuzzleSlot>();
                    SRBehaviour.SpawnAndPlayFX(slot.changeFX, slot.transform.position, slot.transform.rotation);
                    slot.ActivateOnFill();
                    if (slot.puzLockable != null)
                    {
                        slot.puzLockable.NotifySlotChanged(!slot.puzLockable.gameObject.activeInHierarchy);
                        SECTR_AudioCue cueForLastSlot = slot.puzLockable.GetCueForLastSlot();
                        SECTR_AudioSystem.Play(slot.localFillCue, slot.transform.position, false);
                        if (slot.gameObject.activeInHierarchy)
                        {
                            slot.StartCoroutine(slot.DelayedPlayLockCue(cueForLastSlot));
                        }
                    }
                }
            }
        }

        private static void OnPuzzleSlots(PacketPuzzleSlots packet)
        {
            foreach (var puzzleSlotData in packet.PuzzleSlots)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllSlots()
                    .TryGetValue(puzzleSlotData.ID, out PuzzleSlotModel model))
                {
                    model.filled = puzzleSlotData.Model.filled;
                    model.NotifyParticipants();
                }
            }
        }

        #endregion

        #region Gordos

        private static void OnGordoEat(PacketGordoEat packet)
        {
            if (!Globals.Gordos.TryGetValue(packet.ID, out NetworkGordo netGordo))
            {
                if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
                {
                    netGordo = netSite.GetComponentInChildren<NetworkGordo>(true);
                }
            }

            if (netGordo != null)
            {
                netGordo.Eat(packet.Position, packet.Rotation);
                netGordo.Gordo.SetEatenCount(netGordo.Gordo.GetEatenCount() + packet.Count);
                if (packet.Favorite)
                {
                    SRBehaviour.SpawnAndPlayFX(netGordo.Gordo.EatFavoriteFX, packet.Position, packet.Rotation);
                }

                if (netGordo.Gordo.GetEatenCount() >= netGordo.Gordo.GetTargetCount())
                {
                    netGordo.Burst();
                }
            }
        }

        private static void OnGordos(PacketGordos packet)
        {
            var nicknamesEnabled = Globals.NicknamesModInstalled;
            foreach (var gordoData in packet.Gordos)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllGordos()
                    .TryGetValue(gordoData.ID, out GordoModel model))
                {
                    model.fashions = gordoData.Model.fashions;
                    model.gordoEatenCount = gordoData.Model.gordoEatenCount;

                    model.Init();
                    model.NotifyParticipants();
#if SRML
                    if (nicknamesEnabled)
                        model.gameObj.GetComponent(AccessTools.TypeByName("Nicknames.GordoNickname"))
                            .SetField("Name", gordoData.NicknameModValue);
#endif
                }
            }
        }

        #endregion

        #region AccessDoors

        private static void OnAccessDoorOpen(PacketAccessDoorOpen packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllDoors()
                .TryGetValue(packet.ID, out AccessDoorModel model))
            {
                GameObject doorObj = model.gameObj;
                if (doorObj != null && doorObj.GetComponent<AccessDoor>() != null)
                {
                    AccessDoor door = doorObj.GetComponent<AccessDoor>();
                    door.CurrState = (AccessDoor.State)packet.State;
                    if (door.linkedDoors != null)
                    {
                        foreach (AccessDoor accessDoor in door.linkedDoors)
                        {
                            if (accessDoor.CurrState == AccessDoor.State.LOCKED)
                            {
                                accessDoor.CurrState = AccessDoor.State.CLOSED;
                            }
                        }
                    }
                }
            }
        }

        private static void OnAccessDoors(PacketAccessDoors packet)
        {
            foreach (var doorData in packet.Doors)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllDoors()
                    .TryGetValue(doorData.ID, out AccessDoorModel model))
                {
                    if (model.gameObj != null)
                    {
                        model.state = doorData.Model.state;
                        model.Init();
                        model.NotifyParticipants();
                    }
                }
            }
        }

        #endregion

        #region FX

        private static void OnPlayAudio(PacketPlayAudio packet)
        {
            if (Globals.Audios.TryGetValue(packet.CueName, out SECTR_AudioCue cue))
            {
                var before = cue.Spatialization;
                cue.Spatialization = SECTR_AudioCue.Spatializations.Local3D;
                SECTR_AudioSystem.Play(cue, packet.Position, packet.Loop);
                cue.Spatialization = before;
            }
        }

        private static void OnIncinerateFX(PacketIncinerateFX packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                var incinerate = model.gameObj.GetComponentInChildren<Incinerate>();
                if (incinerate != null)
                {
                    SRBehaviour.SpawnAndPlayFX(incinerate.ExplosionFX, packet.Position, packet.Rotation);
                    if (packet.Small)
                    {
                        incinerate.incinerateAudio.Cue = incinerate.smallCue;
                        incinerate.incinerateAudio.Play();
                    }
                    else
                    {
                        incinerate.incinerateAudio.Cue = incinerate.largeCue;
                        incinerate.incinerateAudio.Play();
                    }

                    if (packet.Ash)
                    {
                        SRBehaviour.SpawnAndPlayFX(incinerate.ashFX, packet.Position, packet.Rotation);
                    }
                }
            }
        }

        private static void OnGlobalFX(PacketGlobalFX packet)
        {
            if (Globals.FXPrefabs.TryGetValue(packet.Name, out GameObject prefabFX))
            {
                SRBehaviour.SpawnAndPlayFX(prefabFX, packet.Position, Quaternion.identity);
            }
        }

        #endregion

        #region World

        private static void OnWorldMailRead(PacketWorldMailRead packet)
        {
            var mail = SRSingleton<SceneContext>.Instance.MailDirector.model.allMail.FirstOrDefault(m =>
                m.Equals(new MailDirector.Mail((MailDirector.Type)packet.Type, packet.Key)));

            if (mail != null)
            {
                SRSingleton<SceneContext>.Instance.MailDirector.MarkRead(mail);
            }
        }

        private static void OnWorldMailSend(PacketWorldMailSend packet)
        {
            SRSingleton<SceneContext>.Instance.MailDirector.SendMail((MailDirector.Type)packet.Type, packet.Key);
        }

        private static void OnWorldCredits(PacketWorldCredits packet)
        {
            SRSingleton<GameContext>.Instance.UITemplates.CreateCreditsPrefab(false);
        }

        private static void OnWorldMarketPrices(PacketWorldMarketPrices packet)
        {
            foreach (var data in packet.Prices)
            {
                SRSingleton<SceneContext>.Instance.EconomyDirector.currValueMap[(Identifiable.Id)data.Key].currValue =
                    data.Value.currValue;
                SRSingleton<SceneContext>.Instance.EconomyDirector.currValueMap[(Identifiable.Id)data.Key].prevValue =
                    data.Value.prevValue;
            }

            foreach (var data in packet.Saturation)
            {
                SRSingleton<SceneContext>.Instance.EconomyDirector.worldModel.marketSaturation[
                    (Identifiable.Id)data.Key] = data.Value;
            }

            SRSingleton<SceneContext>.Instance.EconomyDirector.didUpdateDelegate?.Invoke();
        }

        private static void OnWorldDecorizerSetting(PacketWorldDecorizerSetting packet)
        {
            var storage =
                SRSingleton<SceneContext>.Instance.GameModel.decorizer.participants.FirstOrDefault(c =>
                    ((DecorizerStorage)c).id == packet.ID);
            if (storage != null)
            {
                ((DecorizerStorage)storage).selected = (Identifiable.Id)packet.Selected;
            }
        }

        private static void OnWorldDecorizer(PacketWorldDecorizer packet)
        {
            foreach (var c in packet.Contents)
            {
                for (int i = 0; i < c.Value; i++)
                {
                    SRSingleton<SceneContext>.Instance.GameModel.decorizer.contents.Increment(c.Key);
                }
            }

            foreach (var setting in packet.Settings)
            {
                var storage =
                    SRSingleton<SceneContext>.Instance.GameModel.decorizer.participants.FirstOrDefault(c =>
                        ((DecorizerStorage)c).id == setting.Key);
                if (storage != null)
                {
                    ((DecorizerStorage)storage).selected = (Identifiable.Id)setting.Value;
                }
            }
        }

        private static void OnWorldSwitches(PacketWorldSwitches packet)
        {
            foreach (var switchData in packet.Switches)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllSwitches()
                    .TryGetValue(switchData.ID, out MasterSwitchModel model))
                {
                    model.state = switchData.Model.state;
                    model.NotifyParticipants();
                    model.gameObj.GetComponent<WorldStateMasterSwitch>().firstUpdate = true;
                }
            }
        }

        private static void OnWorldSelectPalette(PacketWorldSelectPalette packet)
        {
            SRSingleton<SceneContext>.Instance.GameModel.GetRanchModel()
                .SelectPalette((RanchDirector.PaletteType)packet.Type, (RanchDirector.Palette)packet.Pal);
        }

        private static void OnWorldSwitchActivate(PacketWorldSwitchActivate packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllSwitches()
                .TryGetValue(packet.ID, out MasterSwitchModel model))
            {
                var master = model.gameObj.GetComponent<WorldStateMasterSwitch>();
                master.SetStateForAll((SwitchHandler.State)packet.State, !model.gameObj.activeInHierarchy);
                master.blockSwitchActivationUntil = Time.time + 2f;
            }
        }

        private static void OnWorldMapUnlock(PacketWorldMapUnlock packet)
        {
            SRSingleton<SceneContext>.Instance.PlayerState.UnlockMap((ZoneDirector.Zone)packet.Zone);
            if (SRSingleton<Map>.Instance.mapUI.gameObject.activeInHierarchy)
            {
                SRSingleton<Map>.Instance.mapUI.AddZoneToReveal((ZoneDirector.Zone)packet.Zone);
                SRSingleton<Map>.Instance.mapUI.UpdateZoneFog();
            }

            foreach (var map in Resources.FindObjectsOfTypeAll<MapDataEntry>())
            {
                if (map.zone == (ZoneDirector.Zone)packet.Zone)
                {
                    map.UpdateHologramState();
                }
            }
        }

        private static void OnWorldKey(PacketWorldKey packet)
        {
            if (packet.Added)
            {
                SRSingleton<SceneContext>.Instance.PlayerState.AddKey();
            }
            else
            {
                SRSingleton<SceneContext>.Instance.PlayerState.SpendKey();
            }
        }

        private static void OnWorldProgress(PacketWorldProgress packet)
        {
            SRSingleton<SceneContext>.Instance.ProgressDirector.SetProgress((ProgressDirector.ProgressType)packet.Type,
                packet.Amount);
        }

        private static void OnWorldFastForward(PacketWorldFastForward packet)
        {
            SRSingleton<SceneContext>.Instance.TimeDirector.FastForwardTo(packet.FastForwardTill);
        }

        private static void OnWorldTime(PacketWorldTime packet)
            => ApplyServerWorldTime(packet.Time, 0f);

        /// <summary>
        /// Completes a round trip: derives the RTT from our own echoed stamp, so
        /// no shared clock is needed, and uses it to correct the world time that
        /// travelled with the reply.
        /// </summary>
        private static void OnPong(PacketPong packet)
        {
            //realtimeSinceStartup, not Time.time: it keeps ticking while paused
            //and is unaffected by timeScale, which the game changes
            float rtt = (float)(Time.realtimeSinceStartup - packet.ClientTime);
            if (rtt < 0f) return; //clock went backwards; the sample is useless

            SRMP.RecordPingSample(rtt);
            ApplyServerWorldTime(packet.WorldTime, rtt * 0.5f);
        }

        private static void OnPlayerPings(PacketPlayerPings packet)
        {
            Globals.HostFps = packet.HostFps;

            foreach (var entry in packet.Pings)
            {
                if (Globals.Players.TryGetValue(entry.ID, out var player) && player != null)
                {
                    player.Ping = entry.Ping;
                }
            }
        }

        /// <summary>
        /// Adopts the server's world clock, advanced by however much it moved on
        /// while the packet was in flight. World time is not real time, so the
        /// one-way delay is converted using the rate the clock is currently
        /// running at - which also covers fast-forward.
        /// </summary>
        private static void ApplyServerWorldTime(double serverWorldTime, float oneWayDelaySeconds)
        {
            var timeDirector = SRSingleton<SceneContext>.Instance.TimeDirector;

            double corrected = serverWorldTime;
            if (oneWayDelaySeconds > 0f && Time.deltaTime > 0f)
            {
                //world time per real second, read from the game itself rather
                //than assumed, so fast-forward and any future retune still hold
                double worldTimePerSecond = timeDirector.DeltaWorldTime() / Time.deltaTime;
                if (worldTimePerSecond > 0 && !double.IsInfinity(worldTimePerSecond))
                {
                    corrected += worldTimePerSecond * oneWayDelaySeconds;
                }
            }

            timeDirector.worldModel.worldTime = corrected;
        }

        private static void OnWorldData(PacketWorldData packet)
        {
            SRSingleton<SceneContext>.Instance.GameModel.player.SetKeys(packet.Keys);
            SRSingleton<SceneContext>.Instance.GameModel.player.SetCurrency(packet.Currency);
            SRSingleton<SceneContext>.Instance.TimeDirector.worldModel.worldTime = packet.WorldTime;
            SRSingleton<SceneContext>.Instance.GameModel.player.SetUnlockedZoneMaps(packet.MapUnlocks
                .Select(m => (ZoneDirector.Zone)m).ToList());
            foreach (var progress in packet.Progress)
            {
                SRSingleton<SceneContext>.Instance.ProgressDirector.SetProgress(
                    (ProgressDirector.ProgressType)progress.Key, progress.Value);
            }

            SRSingleton<SceneContext>.Instance.GadgetDirector.model.blueprints = packet.GadgetsModel.blueprints;
            SRSingleton<SceneContext>.Instance.GadgetDirector.model.blueprintLockData =
                packet.GadgetsModel.blueprintLockData;
            SRSingleton<SceneContext>.Instance.GadgetDirector.model.availBlueprints =
                packet.GadgetsModel.availBlueprints;
            SRSingleton<SceneContext>.Instance.GadgetDirector.model.registeredBlueprints =
                packet.GadgetsModel.registeredBlueprints;
            SRSingleton<SceneContext>.Instance.GadgetDirector.model.gadgets = packet.GadgetsModel.gadgets;
            SRSingleton<SceneContext>.Instance.GadgetDirector.model.craftMatCounts = packet.GadgetsModel.craftMatCounts;
            SRSingleton<SceneContext>.Instance.GadgetDirector.model.placedGadgetCounts =
                packet.GadgetsModel.placedGadgetCounts;
            foreach (var pal in packet.Palette)
            {
                SRSingleton<SceneContext>.Instance.GameModel.GetRanchModel()
                    .SelectPalette((RanchDirector.PaletteType)pal.Key, (RanchDirector.Palette)pal.Value);
            }

            foreach (var upgrade in packet.AvailUpgrades.Select(u => (PlayerState.Upgrade)u))
            {
                if (!SRSingleton<SceneContext>.Instance.PlayerState.model.availUpgrades.Contains(upgrade))
                {
                    SRSingleton<SceneContext>.Instance.PlayerState.model.availUpgrades.Add(upgrade);
                }

                SRSingleton<SceneContext>.Instance.PlayerState.model.upgradeLocks.Remove(upgrade);
            }

            SRSingleton<SceneContext>.Instance.PlayerState.model.SetUpgrades(packet.Upgrades
                .Select(u => (PlayerState.Upgrade)u).ToList());
            SRSingleton<SceneContext>.Instance.PediaDirector.Unlock(packet.PediaUnlocks.Select(u => (PediaDirector.Id)u)
                .ToArray());

            SRSingleton<SceneContext>.Instance.EconomyDirector.worldModel.econSeed = packet.Seed;
            foreach (var data in packet.Prices)
            {
                SRSingleton<SceneContext>.Instance.EconomyDirector.currValueMap[(Identifiable.Id)data.Key].currValue =
                    data.Value.currValue;
                SRSingleton<SceneContext>.Instance.EconomyDirector.currValueMap[(Identifiable.Id)data.Key].prevValue =
                    data.Value.prevValue;
            }

            foreach (var data in packet.Saturation)
            {
                SRSingleton<SceneContext>.Instance.EconomyDirector.worldModel.marketSaturation[
                    (Identifiable.Id)data.Key] = data.Value;
            }

            SRSingleton<SceneContext>.Instance.EconomyDirector.didUpdateDelegate?.Invoke();

            SRSingleton<SceneContext>.Instance.MailDirector.model.Reset();
            foreach (var mail in packet.Mails)
            {
                SRSingleton<SceneContext>.Instance.MailDirector.model.AddMail(mail);
            }

            SRSingleton<SceneContext>.Instance.MailDirector.model.MailListChanged();

            foreach (var mapDataEntry in Resources.FindObjectsOfTypeAll<MapDataEntry>())
            {
                if (mapDataEntry != null && mapDataEntry.hologram != null && mapDataEntry.collider != null &&
                    mapDataEntry.activeFx != null)
                {
                    mapDataEntry.UpdateHologramState();
                }
            }

            var phaseSiteDirector = GameObject.FindObjectOfType<PhaseSiteDirector>();
            if (phaseSiteDirector != null)
            {
                phaseSiteDirector.ResetAllSites();
                foreach (PhaseSite phaseSite in new List<PhaseSite>(phaseSiteDirector.availablePhaseSites))
                {
                    if (packet.LemonTrees.Contains(phaseSite.id))
                    {
                        phaseSiteDirector.PlacePhaseObject(phaseSite);
                    }
                }
            }
            else
            {
                SRMP.Log("Could not find PhaseSiteDirector", "CLIENT");
            }
        }

        #endregion

        #region LandPlots

        private static void OnLandPlotSiloAmmoClear(PacketLandPlotSiloAmmoClear packet)
        {
            //if (packet.ID.Contains("site"))
            //{
            //    if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel site) && site.HasAttached())
            //    {
            //        var silocatcher = site.transform.GetComponentInChildren<SiloCatcher>();
            //        if (silocatcher != null)
            //        {
            //            silocatcher.storageSilo.ammo.Clear(packet.Slot);
            //        }
            //    }
            //}
            //else
            //{
            //    var storage = SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()[packet.ID].gameObj.GetComponentsInChildren<SiloCatcher>().First(c => c.type == (SiloCatcher.Type)packet.Type).storageSilo;
            //    storage.ammo.Clear(packet.Slot);
            //}
            if (NetworkAmmo.All.TryGetValue(packet.ID, out NetworkAmmo ammo))
            {
                ammo.Clear(packet.Slot);
                SRMP.Log($"NetworkAmmo clear slot {packet.Slot} for {packet.ID}", "CLIENT");
            }
            else
            {
                SRMP.Log("NetworkAmmo not found for clear: " + packet.ID);
            }
        }

        private static void OnLandPlotSiloAmmoRemove(PacketLandPlotSiloAmmoRemove packet)
        {
            if (NetworkAmmo.All.TryGetValue(packet.ID, out NetworkAmmo ammo))
            {
                ammo.Decrement(packet.Slot, packet.Count);
                SRMP.Log($"NetworkAmmo remove slot {packet.Slot} (Amount: {packet.Count}) for {packet.ID}", "CLIENT");
            }
            else
            {
                SRMP.Log("NetworkAmmo not found for remove: " + packet.ID);
            }
            //if (packet.ID.Contains("site"))
            //{
            //    if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel site) && site.HasAttached())
            //    {
            //        var silocatcher = site.transform.GetComponentInChildren<SiloCatcher>();
            //        if (silocatcher != null)
            //        {
            //            silocatcher.storageSilo.ammo.Decrement(packet.Slot, packet.Count);
            //        }
            //    }
            //}
            //else
            //{
            //    var storage = SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()[packet.ID].gameObj.GetComponentsInChildren<SiloCatcher>().First(c => c.type == (SiloCatcher.Type)packet.Type).storageSilo;
            //    storage.ammo.Decrement(packet.Slot, packet.Count);
            //}
        }

        private static void OnLandPlotSiloAmmoAdd(PacketLandPlotSiloAmmoAdd packet)
        {
            if (NetworkAmmo.All.TryGetValue(packet.ID, out NetworkAmmo ammo))
            {
                ammo.MaybeAddToSpecificSlot((Identifiable.Id)packet.Ident, null, packet.Slot, packet.Count,
                    packet.Overflow);
                SRMP.Log(
                    $"NetworkAmmo add slot {packet.Slot} (Type: {(Identifiable.Id)packet.Ident} - Count: {packet.Count}) for {packet.ID}",
                    "CLIENT");
            }
            else
            {
                SRMP.Log("NetworkAmmo not found for add: " + packet.ID);
            }
            //if (packet.ID.Contains("site"))
            //{
            //    if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel site) && site.HasAttached())
            //    {
            //        var silocatcher = site.transform.GetComponentInChildren<SiloCatcher>();
            //        if (silocatcher != null)
            //        {
            //            silocatcher.storageSilo.ammo.MaybeAddToSpecificSlot((Identifiable.Id)packet.Ident, null, packet.Slot, packet.Count, packet.Overflow);
            //        }
            //    }
            //}
            //else
            //{
            //    var storage = SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()[packet.ID].gameObj.GetComponentsInChildren<SiloCatcher>().First(c => c.type == (SiloCatcher.Type)packet.Type).storageSilo;
            //    storage.ammo.MaybeAddToSpecificSlot((Identifiable.Id)packet.Ident, null, packet.Slot, packet.Count, packet.Overflow);
            //}
        }

        private static void OnLandPlotStartCollection(PacketLandPlotStartCollection packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                var collector = model.gameObj.GetComponentInChildren<PlortCollector>();
                if (collector != null)
                {
                    collector.StartCollection();
                }
            }
        }

        private static void OnLandPlotPlantGarden(PacketLandPlotPlantGarden packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                var ident = (Identifiable.Id)packet.Type;
                GardenCatcher garden = model.gameObj.GetComponentInChildren<GardenCatcher>(true);
                if (garden != null)
                {
                    if (ident != Identifiable.Id.NONE && garden.CanAccept(ident))
                    {
                        if (model.gameObj.activeInHierarchy)
                        {
                            garden.Plant(ident, false);
                        }
                        else
                        {
                            GameObject gameObject = GameObject.Instantiate<GameObject>(
                                garden.activator.HasUpgrade(LandPlot.Upgrade.DELUXE_GARDEN)
                                    ? garden.deluxeDict[ident]
                                    : garden.plantableDict[ident], garden.activator.transform.position,
                                garden.activator.transform.rotation);
                            garden.activator.Attach(gameObject, true, false, null);
                        }
                    }
                    else if (ident == Identifiable.Id.NONE)
                    {
                        Destroyer.Destroy(garden.activator.attached, "LandPlot.DestroyAttached");
                        garden.activator.attached = null;
                        garden.activator.model.attachedId = SpawnResource.Id.NONE;
                    }
                }
            }
        }

        private static void OnLandPlotUpgrade(PacketLandPlotUpgrade packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel landPlot))
            {
                var upgrade = (LandPlot.Upgrade)packet.Upgrade;
                GameObject plotObj = landPlot.gameObj;
                //SRMP.Log($"Upgrade {plotObj.name} with {upgrade}", "CLIENT");
                if (plotObj != null && !landPlot.HasUpgrade(upgrade))
                {
                    if (plotObj.activeInHierarchy)
                    {
                        plotObj.GetComponentInChildren<LandPlot>(true).AddUpgrade(upgrade);
                        GameObject.FindObjectOfType<LandPlotUI>()?.RebuildUI();
                    }
                    else
                    {
                        landPlot.upgrades.Add(upgrade);
                        foreach (PlotUpgrader plotUpgrader in plotObj.GetComponentsInChildren<PlotUpgrader>(true))
                        {
                            plotUpgrader.Apply(upgrade);
                        }
                    }
                }
            }
        }

        private static void OnLandPlotReplace(PacketLandPlotReplace packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                if (model.gameObj != null)
                {
                    model.InstantiatePlot(
                        SRSingleton<GameContext>.Instance.LookupDirector.GetPlotPrefab((LandPlot.Id)packet.Type),
                        false);
                    model.Init();

                    model.gameObj.GetComponentInChildren<LandPlot>(true)?.Awake();
                    model.gameObj.GetComponentInChildren<GardenCatcher>(true)?.Awake();
                    model.gameObj.GetComponentInChildren<SiloStorage>(true)?.Awake();

                    model.NotifyParticipants();
                }
            }
        }

        private static void OnLandPlotCollect(PacketLandPlotCollect packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                var collector = model.gameObj.GetComponentInChildren<PlortCollector>();
                model.collectorNextTime = packet.collectorNextTime;
                if (collector != null)
                {
                    collector.endCollectAt = packet.endCollectAt;
                    collector.forceCollectUntil = packet.forceCollectUntil;
                }
            }
        }

        private static void OnLandPlotFeederSpeed(PacketLandPlotFeederSpeed packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                model.gameObj.GetComponentInChildren<SlimeFeeder>().SetFeederSpeed((SlimeFeeder.FeedSpeed)packet.Speed);
            }
        }

        private static void OnLandPlotSiloSlot(PacketLandPlotSiloSlot packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                model.siloStorageIndices[packet.ActivatorID] = packet.Slot;
                var activator = model.gameObj.GetComponentsInChildren<SiloStorageActivator>()
                    .FirstOrDefault(a => a.activatorIdx == packet.ActivatorID);
                if (activator != null)
                {
                    activator.OnActiveSlotChanged();
                }
            }
        }

        private static void OnLandPlotAsh(PacketLandPlotAsh packet)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                .TryGetValue(packet.ID, out LandPlotModel model))
            {
                model.ashUnits = packet.Amount;
                model.gameObj.GetComponentInChildren<FillableAshSource>().UpdateAshPosition();
            }
        }

        private static void OnLandPlotSiloRemove(PacketLandPlotSiloRemove packet)
        {
            var type = (SiloCatcher.Type)packet.CatcherType;
            var id = (Identifiable.Id)packet.Ident;
            switch (type)
            {
                case SiloCatcher.Type.DECORIZER:
                    SRSingleton<SceneContext>.Instance.GameModel.decorizer.Remove(id);
                    break;
                case SiloCatcher.Type.VIKTOR_STORAGE:
                    ((GlitchStorage)SRSingleton<SceneContext>.Instance.GameModel.Glitch.storage[packet.ID].participant)
                        .Remove(out id);
                    break;
            }
        }

        private static void OnLandPlotSiloInsert(PacketLandPlotSiloInsert packet)
        {
            var type = (SiloCatcher.Type)packet.CatcherType;
            var id = (Identifiable.Id)packet.Ident;
            switch (type)
            {
                case SiloCatcher.Type.REFINERY:
                    SRSingleton<SceneContext>.Instance.GadgetDirector.AddToRefinery(id);
                    GameObject.FindObjectOfType<RefineryUI>()?.Rebuild();
                    break;
                case SiloCatcher.Type.DECORIZER:
                    SRSingleton<SceneContext>.Instance.GameModel.decorizer.Add(id);
                    break;
                case SiloCatcher.Type.VIKTOR_STORAGE:
                    ((GlitchStorage)SRSingleton<SceneContext>.Instance.GameModel.Glitch.storage[packet.ID].participant)
                        .Add(id);
                    break;
            }
        }

        private static void OnLandPlots(PacketLandplots packet)
        {
            foreach (var plotData in packet.LandPlots)
            {
                if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots()
                    .TryGetValue(plotData.ID, out LandPlotModel model))
                {
                    if (model.gameObj != null)
                    {
                        model.InstantiatePlot(
                            SRSingleton<GameContext>.Instance.LookupDirector.GetPlotPrefab(plotData.Model.typeId),
                            false);
                        model.gameObj.GetComponentInChildren<LandPlot>(true)?.Awake();
                        model.Init();

                        model.ashUnits = plotData.Model.ashUnits;
                        model.attachedDeathTime = plotData.Model.attachedDeathTime;
                        model.attachedId = plotData.Model.attachedId;
                        model.attachedResourceId = plotData.Model.attachedResourceId;
                        model.collectorNextTime = plotData.Model.collectorNextTime;
                        model.feederCycleSpeed = plotData.Model.feederCycleSpeed;
                        model.nextFeedingTime = plotData.Model.nextFeedingTime;
                        model.remainingFeedOperations = plotData.Model.remainingFeedOperations;
                        model.siloStorageIndices = plotData.Model.siloStorageIndices;
                        model.typeId = plotData.Model.typeId;
                        model.upgrades = plotData.Model.upgrades.ToHashSet();
                        foreach (var ammo in plotData.Model.siloAmmo)
                        {
                            if (model.siloAmmo.TryGetValue(ammo.Key, out AmmoModel ammoModel))
                            {
                                ammoModel.usableSlots = ammo.Value.usableSlots;
                                ammoModel.slots = ammo.Value.slots;
                            }
                            else
                            {
                                ammoModel = new AmmoModel();
                                ammoModel.usableSlots = ammo.Value.usableSlots;
                                ammoModel.slots = ammo.Value.slots;
                                model.siloAmmo.Add(ammo.Key, ammoModel);
                            }
                        }

                        model.NotifyParticipants();
                    }
                }
            }
        }

        #endregion

        #region Actors

        private static void OnActorFX(PacketActorFX packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var type = (PacketActorFX.FXType)packet.Type;
                var slimeEat = netActor.GetComponentInChildren<SlimeEat>();
                if (slimeEat != null)
                {
                    if (type == PacketActorFX.FXType.SlimeEatFavoriteFX)
                    {
                        SRBehaviour.SpawnAndPlayFX(slimeEat.EatFavoriteFX, slimeEat.transform.position,
                            slimeEat.transform.rotation);
                    }
                    else if (type == PacketActorFX.FXType.SlimeEatFX)
                    {
                        SRBehaviour.SpawnAndPlayFX(slimeEat.EatFX, slimeEat.transform.position,
                            slimeEat.transform.rotation);
                    }
                    else if (type == PacketActorFX.FXType.SlimeTransformFX)
                    {
                        SRBehaviour.SpawnAndPlayFX(slimeEat.TransformFX, slimeEat.transform.position,
                            slimeEat.transform.rotation);
                    }
                    else if (type == PacketActorFX.FXType.SlimeProduceFX)
                    {
                        if (slimeEat.ProduceFX != null)
                        {
                            RecolorSlimeMaterial[] componentsInChildren = SRBehaviour
                                .SpawnAndPlayFX(slimeEat.ProduceFX,
                                    slimeEat.transform.TransformPoint(SlimeEat.LOCAL_PRODUCE_LOC),
                                    slimeEat.transform.rotation).GetComponentsInChildren<RecolorSlimeMaterial>();
                            if (componentsInChildren != null && componentsInChildren.Length != 0)
                            {
                                SlimeAppearance.Palette appearancePalette =
                                    slimeEat.appearanceApplicator.GetAppearancePalette();
                                RecolorSlimeMaterial[] array = componentsInChildren;
                                for (int j = 0; j < array.Length; j++)
                                {
                                    array[j].SetColors(appearancePalette.Top, appearancePalette.Middle,
                                        appearancePalette.Bottom);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void OnActorFeral(PacketActorFeral packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var slimeFeral = netActor.GetComponentInChildren<SlimeFeral>(true);
                if (slimeFeral != null)
                {
                    if (packet.Feral)
                    {
                        slimeFeral.MakeFeral();
                    }
                    else
                    {
                        slimeFeral.MakeNotFeral(packet.Deagitate);
                    }
                }
            }
        }

        private static void OnActorEmotions(PacketActorEmotions packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var emotions = netActor.GetComponentInChildren<SlimeEmotions>(true);
                if (emotions != null)
                {
                    emotions.model.emotionAgitation.currVal = packet.Agitation;
                    emotions.model.emotionFear.currVal = packet.Fear;
                    emotions.model.emotionHunger.currVal = packet.Hunger;
                }
            }
        }

        private static void OnActorReproduceTime(PacketActorReproduceTime packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var reproduce = netActor.GetComponentInChildren<Reproduce>(true);
                if (reproduce != null)
                {
                    reproduce.model.nextReproduceTime = packet.Time;
                }
            }
        }

        private static void OnActorResourceState(PacketActorResourceState packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var cycle = netActor.GetComponentInChildren<ResourceCycle>(true);
                if (cycle != null)
                {
                    var state = (ResourceCycle.State)packet.State;
                    //SRMP.Log($"Resource state for {netActor.name} ({netActor.ID}): {state}", "CLIENT");
                    if (state == ResourceCycle.State.RIPE)
                    {
                        cycle.Ripen();
                        if (cycle.vacuumableWhenRipe)
                        {
                            cycle.vacuumable.enabled = true;
                        }

                        if (cycle.gameObject.transform.localScale.x < cycle.defaultScale.x * 0.33f)
                        {
                            cycle.gameObject.transform.localScale = cycle.defaultScale * 0.33f;
                        }

                        TweenUtil.ScaleTo(cycle.gameObject, cycle.defaultScale, 4f, Ease.InOutQuad);
                    }
                    else if (state == ResourceCycle.State.EDIBLE)
                    {
                        cycle.MakeEdible();
                        cycle.additionalRipenessDelegate = null;
                        var rigid = cycle.GetComponent<Rigidbody>();
                        rigid.isKinematic = false;
                        if (packet.PreparingToRelease)
                        {
                            cycle.preparingToRelease = false;
                            cycle.releaseAt = 0f;
                            cycle.toShake.localPosition = cycle.toShakeDefaultPos;
                            if (cycle.releaseCue != null)
                            {
                                SECTR_PointSource component = cycle.GetComponent<SECTR_PointSource>();
                                component.Cue = cycle.releaseCue;
                                component.Play();
                            }
                        }

                        rigid.WakeUp();
                        cycle.Eject(rigid);
                        cycle.DetachFromJoint();
                        if (cycle.hasVacuumable)
                        {
                            cycle.vacuumable.Pending = false;
                        }
                    }
                    else if (state == ResourceCycle.State.ROTTEN)
                    {
                        cycle.Rot();
                        cycle.SetRotten(!cycle.gameObject.activeInHierarchy);
                    }
                }
            }
        }

        private static void OnActorResourceAttach(PacketActorResourceAttach packet)
        {
            if (packet.PlotID.Length < 1)
            {
                if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor) &&
                    Globals.SpawnResources.TryGetValue(packet.ResourceID, out NetworkSpawnResource netSpawnResource))
                {
                    var cycle = netActor.GetComponentInChildren<ResourceCycle>(true);
                    var joint = netSpawnResource.SpawnResource.SpawnJoints[packet.JointIndex];
                    //SRMP.Log($"Adding {cycle.name} to {joint.gameObject.GetGameObjectPath()}", "CLIENT");
                    cycle?.Attach(joint,
                        new ResourceCycle.AdditionalRipeness(netSpawnResource.SpawnResource
                            .AdditionalRipenessPerSecond), null);
                }
            }
            else
            {
                if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor) && SRSingleton<SceneContext>
                        .Instance.GameModel.AllLandPlots().TryGetValue(packet.PlotID, out LandPlotModel model))
                {
                    var cycle = netActor.GetComponentInChildren<ResourceCycle>(true);
                    var spawner = model.gameObj.GetComponentInChildren<SpawnResource>(true);
                    var joint = spawner.SpawnJoints[packet.JointIndex];
                    //SRMP.Log($"Adding {cycle.name} to {joint.gameObject.GetGameObjectPath()}", "CLIENT");
                    cycle?.Attach(joint, new ResourceCycle.AdditionalRipeness(spawner.AdditionalRipenessPerSecond),
                        null);
                }
            }
        }

        private static void OnActorPosition(PacketActorPosition packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                netActor.PositionRotationUpdate(packet.Position, packet.Rotation, false);
            }
        }

        private static void OnActorOwner(PacketActorOwner packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                netActor.SetOwnership(packet.Owner);
            }
        }

        private static void OnActorDestroy(PacketActorDestroy packet)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                netActor.OnDestroyEffect();
                Destroyer.DestroyActor(netActor.gameObject, "NetworkHandlerServer.OnActorDestroy");
                Globals.Actors.Remove(netActor.ID);
            }
        }

        private static void OnActorSpawn(PacketActorSpawn packet)
        {
            if (!Globals.Actors.ContainsKey(packet.ID))
            {
                var prefab = SRSingleton<GameContext>.Instance.LookupDirector.GetPrefab((Identifiable.Id)packet.Ident);
                var actorObj = SRBehaviour.InstantiateActor(prefab, (RegionRegistry.RegionSetId)packet.RegionSet,
                    packet.Position, packet.Rotation, false);
                var netActor = actorObj.AddComponent<NetworkActor>();
                netActor.ID = packet.ID;
                netActor.Owner = packet.Owner;
                netActor.Ident = packet.Ident;
                netActor.RegionSet = packet.RegionSet;
                netActor.PositionRotationUpdate(packet.Position, packet.Rotation, true);

                Globals.Actors.Add(netActor.ID, netActor);
            }
            else
            {
                SRMP.Log($"Dublicate Actor ID: {packet.ID}");
            }
        }

        private static void OnActors(PacketActors packet)
        {
            var nicknamesEnabled = Globals.NicknamesModInstalled;
            foreach (var actorData in packet.Actors)
            {
                if (!Globals.Actors.ContainsKey(actorData.ID))
                {
                    try
                    {
                        var prefab =
                            SRSingleton<GameContext>.Instance.LookupDirector.GetPrefab(
                                (Identifiable.Id)actorData.Ident);
                        var actorObj = SRBehaviour.InstantiateActor(prefab,
                            (RegionRegistry.RegionSetId)actorData.RegionSet, actorData.Position, actorData.Rotation,
                            false);
                        var netActor = actorObj.AddComponent<NetworkActor>();
                        netActor.ID = actorData.ID;
                        netActor.Owner = actorData.Owner;
                        netActor.Ident = actorData.Ident;
                        netActor.RegionSet = actorData.RegionSet;
                        netActor.PositionRotationUpdate(actorData.Position, actorData.Rotation, true);

                        if (actorData.ProduceModel != null)
                        {
                            var resourceCycle = actorObj.GetComponentInChildren<ResourceCycle>(true);
                            if (resourceCycle != null)
                            {
                                resourceCycle.SetInitState(actorData.ProduceModel.state,
                                    actorData.ProduceModel.progressTime);
                            }
                        }

                        if (actorData.PlortModel != null)
                        {
                            var plortDestroy = actorObj.GetComponentInChildren<DestroyPlortAfterTime>(true);
                            if (plortDestroy != null)
                            {
                                plortDestroy.plortModel.destroyTime = actorData.PlortModel.destroyTime;
                            }
                        }

                        if (actorData.AnimalModel != null)
                        {
                            var reproduce = actorObj.GetComponentInChildren<Reproduce>(true);
                            if (reproduce != null)
                            {
                                reproduce.model.fashions = actorData.AnimalModel.fashions;
                                reproduce.model.nextReproduceTime = actorData.AnimalModel.nextReproduceTime;
                                reproduce.model.transformTime = actorData.AnimalModel.transformTime;

                                reproduce.model.NotifyParticipants(actorObj);
                            }
                        }

                        if (actorData.SlimeModel != null)
                        {
                            var slimeEat = actorObj.GetComponentInChildren<SlimeEat>(true);
                            if (slimeEat != null)
                            {
                                slimeEat.slimeModel.emotionAgitation.currVal =
                                    actorData.SlimeModel.emotionAgitation.currVal;
                                slimeEat.slimeModel.emotionFear.currVal = actorData.SlimeModel.emotionFear.currVal;
                                slimeEat.slimeModel.emotionHunger.currVal = actorData.SlimeModel.emotionHunger.currVal;
                                slimeEat.slimeModel.isFeral = actorData.SlimeModel.isFeral;
                                slimeEat.slimeModel.isGlitch = actorData.SlimeModel.isGlitch;
                                slimeEat.slimeModel.fashions = actorData.SlimeModel.fashions;

                                slimeEat.slimeModel.NotifyParticipants(actorObj);
                            }
                        }



#if SRML
                        if (nicknamesEnabled)
                            netActor.GetComponent(AccessTools.TypeByName("Nicknames.SlimeNickname"))
                                .SetField("Name", actorData.NicknameModValue);
#endif


                        Globals.Actors.Add(netActor.ID, netActor);
                    }
                    catch (Exception ex)
                    {
                        SRMP.Log($"Could not create actor {actorData.ID}: {ex}");
                    }
                }
                else
                {
                    SRMP.Log($"Dublicate Actor ID: {actorData.ID}");
                }
            }
        }

        #endregion

        #region Regions

        private static void OnRegionOwner(PacketRegionOwner packet)
        {
            if (Globals.Regions.TryGetValue(packet.ID, out NetworkRegion netRegion))
            {
                netRegion.SetOwnership(packet.Owner);
            }
        }

        #endregion

        #region Players

        private static void OnPlayerChat(PacketPlayerChat packet)
        {
            ChatUI.Instance.AddChatMessage(packet.message);
        }

        private static void OnPlayerUpgradeUnlock(PacketPlayerUpgradeUnlock packet)
        {
            if (!SRSingleton<SceneContext>.Instance.PlayerState.HasUpgrade((PlayerState.Upgrade)packet.Upgrade) &&
                !SRSingleton<SceneContext>.Instance.PlayerState.model.availUpgrades.Contains(
                    (PlayerState.Upgrade)packet.Upgrade))
            {
                SRSingleton<SceneContext>.Instance.PlayerState.model.availUpgrades.Add(
                    (PlayerState.Upgrade)packet.Upgrade);
                SRSingleton<SceneContext>.Instance.PlayerState.popupDir.QueueForPopup(
                    new PlayerState.AvailUpgradePopupCreator((PlayerState.Upgrade)packet.Upgrade));
                SRSingleton<SceneContext>.Instance.PlayerState.popupDir.MaybePopupNext();
            }

            SRSingleton<SceneContext>.Instance.PlayerState.model.upgradeLocks.Remove(
                (PlayerState.Upgrade)packet.Upgrade);
        }

        private static void OnPlayerUpgrade(PacketPlayerUpgrade packet)
        {
            SRSingleton<SceneContext>.Instance.PlayerState.AddUpgrade((PlayerState.Upgrade)packet.Upgrade);
        }

        private static void OnPlayerCurrencyDisplay(PacketPlayerCurrencyDisplay packet)
        {
            SRSingleton<SceneContext>.Instance.PlayerState.SetCurrencyDisplay(packet.IsNull
                ? null
                : new int?(packet.Currency));
            if (packet.IsNull)
            {
                SRSingleton<PopupElementsUI>.Instance.CreateCoinsPopup(packet.Currency, PlayerState.CoinsType.DRONE);
            }
        }

        private static void OnPlayerCurrency(PacketPlayerCurrency packet)
        {
            SRSingleton<SceneContext>.Instance.PlayerState.model.currency = packet.Total;
            SRSingleton<PopupElementsUI>.Instance.CreateCoinsPopup(packet.Adjust, (PlayerState.CoinsType)packet.Type);
        }

        private static void OnPlayerFX(PacketPlayerFX packet)
        {
            if (Globals.Players.TryGetValue(packet.ID, out NetworkPlayer player))
            {
                var type = (PacketPlayerFX.FXType)packet.Type;

                switch (type)
                {
                    case PacketPlayerFX.FXType.Capture:
                        SRBehaviour.SpawnAndPlayFX(player.CaptureFX, player.VacTransform.gameObject, Vector3.zero,
                            Quaternion.identity);
                        break;
                    case PacketPlayerFX.FXType.CaptureFailed:
                        SRBehaviour.SpawnAndPlayFX(player.CaptureFailedFX, player.VacTransform.gameObject, Vector3.zero,
                            Quaternion.identity);
                        break;
                    case PacketPlayerFX.FXType.Shoot:
                        SRBehaviour.SpawnAndPlayFX(player.ShootFX, player.VacTransform.gameObject, Vector3.zero,
                            Quaternion.identity);
                        break;
                    case PacketPlayerFX.FXType.VacAudio:
                        player.SetVacAudio(packet.Enable);
                        break;
                    case PacketPlayerFX.FXType.Vac:
                        player.VacFX.SetActive(packet.Enable);
                        break;
                    case PacketPlayerFX.FXType.Airburst:
                        player.Airburst();
                        break;
                    case PacketPlayerFX.FXType.DestroyOnVac:

                        break;
                    case PacketPlayerFX.FXType.JetpackAudio:
                        player.SetJetpackAudio(packet.Enable);
                        break;
                }
            }
        }

        private static void OnPlayerAnimation(PacketPlayerAnimation packet)
        {
            //handle character animation triggers
            PacketPlayerAnimation.AnimationType type = (PacketPlayerAnimation.AnimationType)packet.Type;



            if (Globals.Players.TryGetValue(packet.ID, out NetworkPlayer player) && player.HasLoaded)
            {
                if (type == PacketPlayerAnimation.AnimationType.Speed)
                    player.ReadAnimatorSpeed(packet.internalData);
                else if (type == PacketPlayerAnimation.AnimationType.Parameters)
                {
                    player.ReadParameters(packet.internalData);
                }
                else
                    player.ReadAnimatorLayer(packet.internalData);
            }
        }

        private static void OnPlayerPosition(PacketPlayerPosition packet)
        {
            if (Globals.Players.TryGetValue(packet.ID, out NetworkPlayer player))
            {
                if (player.HasLoaded && Globals.GameLoaded)
                {
                    if (player.IsLocal)
                    {
                        if (packet.OnLoad)
                        {

                            var euler = SRSingleton<SceneContext>.Instance.player.GetComponentInChildren<WeaponVacuum>()
                                .transform.eulerAngles;
                            euler.x = packet.WeaponY;
                            SRSingleton<SceneContext>.Instance.player.GetComponentInChildren<WeaponVacuum>().transform
                                .eulerAngles = euler;
                            SRSingleton<SceneContext>.Instance.player.transform.position = packet.Position;
                            SRSingleton<SceneContext>.Instance.player.transform.eulerAngles =
                                new Vector3(0, packet.Rotation, 0);
                            SRSingleton<SceneContext>.Instance.PlayerState.model.SetCurrRegionSet(
                                (RegionRegistry.RegionSetId)packet.RegionSet);


                            //only reload inventory if this is a load up packet and NOT a tp packet
                            if (!Globals.IsServer)
                            {
                                try
                                {
                                    using (FileStream file = new FileStream(
                                               Path.Combine(SRMP.ModDataPath, Globals.CurrentGameName + ".player"),
                                               FileMode.Open))
                                    {
                                        using (BinaryReader reader = new BinaryReader(file))
                                        {
                                            Debug.Log(
                                                $"Loading {Path.Combine(SRMP.ModDataPath, Globals.CurrentGameName + ".player")}");
                                            var ammoCount = reader.ReadInt32();
                                            for (int i = 0; i < ammoCount; i++)
                                            {
                                                var state = (PlayerState.AmmoMode)reader.ReadByte();
                                                SRSingleton<SceneContext>.Instance.PlayerState.model.ammoDict[state]
                                                    .usableSlots = reader.ReadInt32();
                                                var slotCount = reader.ReadInt32();
                                                SRSingleton<SceneContext>.Instance.PlayerState.model.ammoDict[state]
                                                    .slots = new Ammo.Slot[slotCount];
                                                for (int j = 0; j < slotCount; j++)
                                                {
                                                    if (reader.ReadBoolean())
                                                    {
                                                        SRSingleton<SceneContext>.Instance.PlayerState.model
                                                                .ammoDict[state].slots[j] =
                                                            new Ammo.Slot((Identifiable.Id)reader.ReadUInt16(),
                                                                reader.ReadInt32());
                                                        if (reader.ReadBoolean())
                                                        {
                                                            SRSingleton<SceneContext>.Instance.PlayerState.model
                                                                    .ammoDict[state].slots[j].emotions =
                                                                new SlimeEmotionData();
                                                            var emotionCount = reader.ReadInt32();
                                                            for (int k = 0; k < emotionCount; k++)
                                                            {
                                                                SRSingleton<SceneContext>.Instance.PlayerState.model
                                                                    .ammoDict[state].slots[j].emotions
                                                                    .Add((SlimeEmotions.Emotion)reader.ReadUInt16(),
                                                                        reader.ReadSingle());
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        SRSingleton<SceneContext>.Instance.PlayerState.model
                                                            .ammoDict[state].slots[j] = null;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.Log($"No savefile for {Globals.CurrentGameName}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            SRMP.Log("Player is being teleported", "CLIENT");

                            SRSingleton<SceneContext>.Instance.player.transform.position = packet.Position;
                            SRSingleton<SceneContext>.Instance.player.transform.eulerAngles =
                                new Vector3(0, packet.Rotation, 0);
                            SRSingleton<SceneContext>.Instance.PlayerState.model.SetCurrRegionSet(
                                (RegionRegistry.RegionSetId)packet.RegionSet);

                            SRSingleton<Overlay>.Instance.PlayTeleport();

                        }
                    }
                    else
                    {
                        player.PositionRotationUpdate(packet.Position, packet.Rotation, false);
                        player.UpdateWeaponRotation(packet.WeaponY);
                        player.CurrentRegionSet = (RegionRegistry.RegionSetId)packet.RegionSet;
                    }
                }
            }
        }

        private static void OnPlayerLoaded(PacketPlayerLoaded packet)
        {
            if (Globals.Players.TryGetValue(packet.ID, out NetworkPlayer player))
            {
                if (player.IsLocal)
                {
                    Globals.ClientLoaded = true;
                    SRMP.Log("Received all data, multiplayer fully loaded!", "CLIENT");
                    player.IsVR = Globals.VRInstalled;
                }
                else
                {
                    player.HasLoaded = true;

                    if (Globals.GameLoaded)
                    {
                        player.Spawn();
                    }
                }
            }
        }

        private static void OnPlayerLeft(PacketPlayerLeft packet)
        {
            if (Globals.Players.TryGetValue(packet.ID, out NetworkPlayer player))
            {
                GameObject.Destroy(player.gameObject);
                Globals.Players.Remove(packet.ID);
                Globals.EpicToPlayer.Remove(Globals.PlayerToEpic[packet.ID]);
                Globals.PlayerToEpic.Remove(packet.ID);
            }
        }

        private static void OnPlayerJoined(PacketPlayerJoined packet)
        {
            var playerObj = new GameObject($"{packet.Username} ({packet.ID})");
            var player = playerObj.AddComponent<NetworkPlayer>();
            player.ID = packet.ID;
            player.Username = packet.Username;
            player.IsVR = packet.VR;

            Globals.Players.Add(packet.ID, player);
        }

        private static void OnPlayerKicked(PacketKickClient packet)
        {
            MultiplayerUI.Instance.kickData = packet;
        }

        #endregion

#if SRML

        #region Mod Compatibility

        private static void OnSetNickname(PacketSetNickname packet)
        {
            if (packet.type)
            {
                var gordo = Globals.Gordos[packet.gordoId];

                var nicknameGordo = gordo.GetComponent(AccessTools.TypeByName("Nicknames.GordoNickname"));

                nicknameGordo.SetField("Name", packet.nickname);
            }
            else
            {
                var slime = Globals.Actors[packet.actorId];

                var nicknameSlime = slime.GetComponent(AccessTools.TypeByName("Nicknames.SlimeNickname"));

                nicknameSlime.SetField("Name", packet.nickname);
            }
        }


        private static void OnVRPositions(PacketVRPositions packet)
        {
            if (Globals.Players.TryGetValue(packet.ID, out var player))
            {
                player.IsVR = true;
                
                player.PositionRotationUpdateVR(packet);
                player.UpdateHeadRotationVR(packet.headAngle);
            }
        }
        #endregion
#endif
    }
}
