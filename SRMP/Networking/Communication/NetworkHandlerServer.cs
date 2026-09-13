using DG.Tweening;
using Lidgren.Network;
using MonomiPark.SlimeRancher.DataModel;
using MonomiPark.SlimeRancher.Regions;
using Newtonsoft.Json.Linq;
using SRMultiplayer.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using SRMultiplayer.Packets.ModCompat;
using UnityEngine;

namespace SRMultiplayer.Networking
{
    public static class NetworkHandlerServer
    {
        public static void HandlePacket(PacketType type, NetIncomingMessage im, NetworkPlayer player)
        {
            Globals.HandlePacket = true;
            if (!Globals.PacketSize.ContainsKey(type))
                Globals.PacketSize.Add(type, 0);
            Globals.PacketSize[type] += im.LengthBytes;

            if (Globals.CustomPackets.TryGetValue(type, out var packetClass))
            {
                Packet packet = (Packet)packetClass.GetConstructor(new Type[] { typeof(NetIncomingMessage) })?.Invoke(new object[] { im });
                
                if (packet != null)
                    packet.InvokeMethod("HandleServer", packet, player);
                else
                    SRMP.Log($"Got unhandled packet from {player}:  {type}" + Enum.GetName(typeof(PacketType), type));
                
                return;
            }
            
            switch (type)
            {
                //Player animation
                case PacketType.PlayerAnimation: OnPlayerAnimation(new PacketPlayerAnimation(im), player); break;
                //Player
                case PacketType.PlayerLoaded: OnPlayerLoaded(new PacketPlayerLoaded(im), player); break;
                case PacketType.PlayerPosition: OnPlayerPosition(new PacketPlayerPosition(im), player); break;
                case PacketType.PlayerCurrency: OnPlayerCurrency(new PacketPlayerCurrency(im), player); break;
                case PacketType.PlayerCurrencyDisplay: OnPlayerCurrencyDisplay(new PacketPlayerCurrencyDisplay(im), player); break;
                case PacketType.PlayerUpgrade: OnPlayerUpgrade(new PacketPlayerUpgrade(im), player); break;
                case PacketType.PlayerUpgradeUnlock: OnPlayerUpgradeUnlock(new PacketPlayerUpgradeUnlock(im), player); break;
                case PacketType.PlayerFX: OnPlayerFX(new PacketPlayerFX(im), player); break;
                case PacketType.PlayerChat: OnPlayerChat(new PacketPlayerChat(im), player); break;
                case PacketType.Ping: OnPing(new PacketPing(im), player); break;
                //Actors
                case PacketType.ActorSpawn: OnActorSpawn(new PacketActorSpawn(im), player); break;
                case PacketType.ActorDestroy: OnActorDestroy(new PacketActorDestroy(im), player); break;
                case PacketType.ActorPosition: OnActorPosition(new PacketActorPosition(im), player); break;
                case PacketType.ActorOwner: OnActorOwner(new PacketActorOwner(im), player); break;
                case PacketType.ActorResourceAttach: OnActorResourceAttach(new PacketActorResourceAttach(im), player); break;
                case PacketType.ActorResourceState: OnActorResourceState(new PacketActorResourceState(im), player); break;
                case PacketType.ActorReproduceTime: OnActorReproduceTime(new PacketActorReproduceTime(im), player); break;
                case PacketType.ActorEmotions: OnActorEmotions(new PacketActorEmotions(im), player); break;
                case PacketType.ActorFeral: OnActorFeral(new PacketActorFeral(im), player); break;
                case PacketType.ActorFX: OnActorFX(new PacketActorFX(im), player); break;
                //Region
                case PacketType.RegionOwner: OnRegionOwner(new PacketRegionOwner(im), player); break;
                case PacketType.RegionChange: OnRegionChange(new PacketRegionChange(im), player); break;
                //LandPlots
                case PacketType.LandPlotSiloInsert: OnLandPlotSiloInsert(new PacketLandPlotSiloInsert(im), player); break;
                case PacketType.LandPlotSiloRemove: OnLandPlotSiloRemove(new PacketLandPlotSiloRemove(im), player); break;
                case PacketType.LandPlotAsh: OnLandPlotAsh(new PacketLandPlotAsh(im), player); break;
                case PacketType.LandPlotSiloSlot: OnLandPlotSiloSlot(new PacketLandPlotSiloSlot(im), player); break;
                case PacketType.LandPlotFeederSpeed: OnLandPlotFeederSpeed(new PacketLandPlotFeederSpeed(im), player); break;
                case PacketType.LandPlotCollect: OnLandPlotCollect(new PacketLandPlotCollect(im), player); break;
                case PacketType.LandPlotReplace: OnLandPlotReplace(new PacketLandPlotReplace(im), player); break;
                case PacketType.LandPlotUpgrade: OnLandPlotUpgrade(new PacketLandPlotUpgrade(im), player); break;
                case PacketType.LandPlotPlantGarden: OnLandPlotPlantGarden(new PacketLandPlotPlantGarden(im), player); break;
                case PacketType.LandPlotStartCollection: OnLandPlotStartCollection(new PacketLandPlotStartCollection(im), player); break;
                case PacketType.LandPlotSiloAmmoAdd: OnLandPlotSiloAmmoAdd(new PacketLandPlotSiloAmmoAdd(im), player); break;
                case PacketType.LandPlotSiloAmmoRemove: OnLandPlotSiloAmmoRemove(new PacketLandPlotSiloAmmoRemove(im), player); break;
                case PacketType.LandPlotSiloAmmoClear: OnLandPlotSiloAmmoClear(new PacketLandPlotSiloAmmoClear(im), player); break;
                //FX
                case PacketType.GlobalFX: OnGlobalFX(new PacketGlobalFX(im), player); break;
                case PacketType.IncinerateFX: OnIncinerateFX(new PacketIncinerateFX(im), player); break;
                case PacketType.PlayAudio: OnPlayAudio(new PacketPlayAudio(im), player); break;
                //AccessDoors
                case PacketType.AccessDoorOpen: OnAccessDoorOpen(new PacketAccessDoorOpen(im), player); break;
                //World
                case PacketType.WorldFastForward: OnWorldFastForward(new PacketWorldFastForward(im), player); break;
                case PacketType.WorldProgress: OnWorldProgress(new PacketWorldProgress(im), player); break;
                case PacketType.WorldKey: OnWorldKey(new PacketWorldKey(im), player); break;
                case PacketType.WorldMapUnlock: OnWorldMapUnlock(new PacketWorldMapUnlock(im), player); break;
                case PacketType.WorldSwitchActivate: OnWorldSwitchActivate(new PacketWorldSwitchActivate(im), player); break;
                case PacketType.WorldSelectPalette: OnWorldSelectPalette(new PacketWorldSelectPalette(im), player); break;
                case PacketType.WorldDecorizer: OnWorldDecorizer(new PacketWorldDecorizer(im), player); break;
                case PacketType.WorldDecorizerSetting: OnWorldDecorizerSetting(new PacketWorldDecorizerSetting(im), player); break;
                case PacketType.WorldMarketSold: OnWorldMarketSold(new PacketWorldMarketSold(im), player); break;
                case PacketType.WorldCredits: OnWorldCredits(new PacketWorldCredits(im), player); break;
                case PacketType.WorldMailRead: OnWorldMailRead(new PacketWorldMailRead(im), player); break;
                case PacketType.WorldMailSend: OnWorldMailSend(new PacketWorldMailSend(im), player); break;
                //PuzzleSlots
                case PacketType.PuzzleSlotFilled: OnPuzzleSlotFilled(new PacketPuzzleSlotFilled(im), player); break;
                case PacketType.PuzzleGateActivate: OnPuzzleGateActivate(new PacketPuzzleGateActivate(im), player); break;
                //Pedia
                case PacketType.PediaShowPopup: OnPediaShowPopup(new PacketPediaShowPopup(im), player); break;
                case PacketType.PediaUnlock: OnPediaUnlock(new PacketPediaUnlock(im), player); break;
                //Fashion
                case PacketType.FashionAttach: OnFashionAttach(new PacketFashionAttach(im), player); break;
                case PacketType.FashionDetachAll: OnFashionDetachAll(new PacketFashionDetachAll(im), player); break;
                //gadgets
                case PacketType.GadgetRemove: OnGadgetRemove(new PacketGadgetRemove(im), player); break;
                case PacketType.GadgetRotation: OnGadgetRotation(new PacketGadgetRotation(im), player); break;
                case PacketType.GadgetRefinerySpend: OnGadgetRefinerySpend(new PacketGadgetRefinerySpend(im), player); break;
                case PacketType.GadgetAdd: OnGadgetAdd(new PacketGadgetAdd(im), player); break;
                case PacketType.GadgetAddBlueprint: OnGadgetAddBlueprint(new PacketGadgetAddBlueprint(im), player); break;
                case PacketType.GadgetSpend: OnGadgetSpend(new PacketGadgetSpend(im), player); break;
                case PacketType.GadgetSpawn: OnGadgetSpawn(new PacketGadgetSpawn(im), player); break;
                case PacketType.GadgetExtractorUpdate: OnGadgetExtractorUpdate(new PacketGadgetExtractorUpdate(im), player); break;
                case PacketType.GadgetTurrets: OnGadgetTurrets(new PacketGadgetTurrets(im), player); break;
                case PacketType.GadgetSnareAttach: OnGadgetSnareAttach(new PacketGadgetSnareAttach(im), player); break;
                case PacketType.GadgetSnareGordo: OnGadgetSnareGordo(new PacketGadgetSnareGordo(im), player); break;
                case PacketType.GadgetEchoNetTime: OnGadgetEchoNetTime(new PacketGadgetEchoNetTime(im), player); break;
                //Drones
                case PacketType.DroneAmmoAdd: OnDroneAmmoAdd(new PacketDroneAmmoAdd(im), player); break;
                case PacketType.DroneAmmoClear: OnDroneAmmoClear(new PacketDroneAmmoClear(im), player); break;
                case PacketType.DroneAmmoRemove: OnDroneAmmoRemove(new PacketDroneAmmoRemove(im), player); break;
                case PacketType.DroneAnimation: OnDroneAnimation(new PacketDroneAnimation(im), player); break;
                case PacketType.DronePrograms: OnDronePrograms(new PacketDronePrograms(im), player); break;
                case PacketType.DroneLiquid: OnDroneLiquid(new PacketDroneLiquid(im), player); break;
                case PacketType.DroneStationEnabled: OnDroneStationEnabled(new PacketDroneStationEnabled(im), player); break;
                case PacketType.DronePosition: OnDronePosition(new PacketDronePosition(im), player); break;
                case PacketType.DroneActive: OnDroneActive(new PacketDroneActive(im), player); break;
                //TreasurePods
                case PacketType.TreasurePodOpen: OnTreasurePodOpen(new PacketTreasurePodOpen(im), player); break;
                //Exchanges
                case PacketType.ExchangeOffer: OnExchangeOffer(new PacketExchangeOffer(im), player); break;
                case PacketType.ExchangeClear: OnExchangeClear(new PacketExchangeClear(im), player); break;
                case PacketType.ExchangePrepareDaily: OnExchangePrepareDaily(new PacketExchangePrepareDaily(im), player); break;
                case PacketType.ExchangeTryAccept: OnExchangeTryAccept(new PacketExchangeTryAccept(im), player); break;
                //Gordos
                case PacketType.GordoEat: OnGordoEat(new PacketGordoEat(im), player); break;
                //Oasis
                case PacketType.OasisLive: OnOasisLive(new PacketOasisLive(im), player); break;
                //Others
                case PacketType.GingerAction: OnGingerAction(new PacketGingerAction(im), player); break;
                case PacketType.KookadobaAction: OnKookadobaAction(new PacketKookadobaAction(im), player); break;
                // Race
                case PacketType.RaceActivate: OnRaceActivate(new PacketRaceActivate(im), player); break;
                case PacketType.RaceEnd: OnRaceEnd(new PacketRaceEnd(im), player); break;
                case PacketType.RaceTime: OnRaceTime(new PacketRaceTime(im), player); break;
                case PacketType.RaceTrigger: OnRaceTrigger(new PacketRaceTrigger(im), player); break;
                #if SRML
                // Mod Compatibility
                case PacketType.SetNickname: OnSetNickname(new PacketSetNickname(im), player); break;
                case PacketType.VRPositions: OnVRPositions(new PacketVRPositions(im), player); break;
                #endif                
                default:
                    SRMP.Log($"Got unhandled packet from {player}:  {type}" + Enum.GetName(typeof(PacketType), type));
                    break;
            }
            Globals.HandlePacket = false;
        }

        #region Race
        private static void OnRaceTrigger(PacketRaceTrigger packet, NetworkPlayer player)
        {
            if (Globals.RaceTriggers.TryGetValue(packet.ID, out NetworkRaceTrigger trigger))
            {
                trigger.Activate();
            }
            packet.SendToAllExcept(player);
        }

        private static void OnRaceTime(PacketRaceTime packet, NetworkPlayer player)
        {
            var generator = QuicksilverEnergyGenerator.allGenerators.FirstOrDefault(g => g.id == packet.ID);
            if (generator)
            {
                generator.ExtendActiveDuration(packet.Time);
            }
            packet.SendToAllExcept(player);
        }

        private static void OnRaceEnd(PacketRaceEnd packet, NetworkPlayer player)
        {
            var generator = QuicksilverEnergyGenerator.allGenerators.FirstOrDefault(g => g.id == packet.ID);
            if (generator)
            {
                generator.SetState(QuicksilverEnergyGenerator.State.COOLDOWN, Globals.LocalPlayer.CurrentGenerator.id == generator.id);
            }
            packet.SendToAllExcept(player);
        }

        private static void OnRaceActivate(PacketRaceActivate packet, NetworkPlayer player)
        {
            var generator = QuicksilverEnergyGenerator.allGenerators.FirstOrDefault(g => g.id == packet.ID);
            if (generator)
            {
                if (Globals.LocalPlayer.CurrentGenerator.id == generator.id)
                {
                    generator.Activate();
                }
                else
                {
                    generator.model.state = QuicksilverEnergyGenerator.State.COUNTDOWN;
                    generator.model.timer = new double?(generator.timeDirector.HoursFromNow(generator.countdownMinutes * 0.0166666675f));
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Others
        private static void OnKookadobaAction(PacketKookadobaAction packet, NetworkPlayer player)
        {
            if (Globals.Kookadobas.TryGetValue(packet.ID, out NetworkKookadobaPatchNode node))
            {
                if (packet.Grow)
                {
                    node.Node.Grow();
                    SRMP.Log($"Kookadoba Grow {packet.ID}", "SERVER");
                }
                else if (packet.Harvest)
                {
                    node.Node.Harvested();
                    SRMP.Log($"Kokadoba Harvested {packet.ID}", "SERVER");
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnGingerAction(PacketGingerAction packet, NetworkPlayer player)
        {
            var node = GingerPatchNode.allGingerPatches.FirstOrDefault(g => g.id == packet.ID);
            if (node != null)
            {
                if (packet.Grow)
                {
                    node.bed.SetActive(true);
                    node.spawnJoint.gameObject.SetActive(true);
                    SRMP.Log($"Ginger Grow {packet.ID}", "SERVER");
                }
                else if (packet.Harvest)
                {
                    node.Harvested();
                    SRMP.Log($"Ginger Harvested {packet.ID}", "SERVER");
                }
                else
                {
                    node.HidePatchAndReset();
                    SRMP.Log($"Ginger HidePatchAndReset {packet.ID}", "SERVER");
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Oasis
        private static void OnOasisLive(PacketOasisLive packet, NetworkPlayer player)
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
                                SRBehaviour.InstantiateDynamic(trigger.scaleFX, trigger.transform.position, trigger.transform.rotation, false);
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
                    SRMP.Log($"Oasis {packet.ID} SetLive", "SERVER");
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Gordos
        private static void OnGordoEat(PacketGordoEat packet, NetworkPlayer player)
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
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Exchanges
        private static void OnExchangeTryAccept(PacketExchangeTryAccept packet, NetworkPlayer player)
        {
            var type = (ExchangeDirector.OfferType)packet.Type;
            if (SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers.ContainsKey(type))
            {
                var offer = SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers[type];
                foreach (ExchangeDirector.RequestedItemEntry requestedItemEntry in offer.requests)
                {
                    if (requestedItemEntry.id == (Identifiable.Id)packet.ID && !requestedItemEntry.IsComplete())
                    {
                        SRMP.Log($"Exchange TryAccept for {(Identifiable.Id)packet.ID} ({(ExchangeDirector.OfferType)packet.Type}", "SERVER");
                        requestedItemEntry.progress++;
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
                                SRBehaviour.InstantiateDynamic(eject.awardFX, eject.awardAt.position, eject.awardAt.rotation);
                            }

                            //dont clear out the offer yet, we arent done with it
                            //SRSingleton<SceneContext>.Instance.ExchangeDirector.ClearOffer(type);
                        }
                        SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();
                    }
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnExchangePrepareDaily(PacketExchangePrepareDaily packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.pendingOfferRancherIds = packet.pendingOfferRancherIds;
            SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();
            SRMP.Log($"ExchangePrepareDaily", "SERVER");
            packet.SendToAllExcept(player);
        }

        private static void OnExchangeClear(PacketExchangeClear packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers.Remove((ExchangeDirector.OfferType)packet.Type);
            SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();
            SRMP.Log($"ExchangeClear", "SERVER");
            packet.SendToAllExcept(player);
        }

        private static void OnExchangeOffer(PacketExchangeOffer packet, NetworkPlayer player)
        {
            if ((ExchangeDirector.OfferType)packet.Type == ExchangeDirector.OfferType.GENERAL)
            {
                SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.pendingOfferRancherIds.Clear();
            }

            SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers[(ExchangeDirector.OfferType)packet.Type] = packet.Offer;
            SRSingleton<SceneContext>.Instance.ExchangeDirector.OfferDidChange();
            SRMP.Log($"ExchangeOffer for {(ExchangeDirector.OfferType)packet.Type}", "SERVER");
            packet.SendToAllExcept(player);
        }
        #endregion

        #region TreasurePods
        private static void OnTreasurePodOpen(PacketTreasurePodOpen packet, NetworkPlayer player)
        {
            if (Globals.TreasurePods.TryGetValue(packet.ID, out NetworkTreasurePod netTreasurePod))
            {
                netTreasurePod.Activate();
                SRMP.Log($"TreasurePot {packet.ID} activated", "SERVER");
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Drones
        private static void OnDroneActive(PacketDroneActive packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.onActiveCue.enabled = packet.Enabled;
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnDronePosition(PacketDronePosition packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
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
            packet.SendToAllExcept(player, NetDeliveryMethod.Unreliable);
        }

        private static void OnDroneStationEnabled(PacketDroneStationEnabled packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().station.animator.SetEnabled(packet.Enabled);
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnDroneLiquid(PacketDroneLiquid packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    if (model.attached.transform.gameObject.activeInHierarchy)
                    {
                        model.attached.transform.GetComponent<DroneGadget>().station.battery.AddLiquid(Identifiable.Id.NONE, 0);
                    }
                    else
                    {
                        model.attached.transform.GetComponent<DroneGadget>().station.battery.Reset(model.attached.transform.GetComponent<DroneGadget>().droneModel);
                    }
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnDronePrograms(PacketDronePrograms packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    var drone = model.attached.transform.GetComponent<DroneGadget>();
                    drone.SetPrograms(drone.ProgramsFromData(packet.Programs));
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnDroneAnimation(PacketDroneAnimation packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.animator.SetAnimation((DroneAnimator.Id)packet.Anim);
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnDroneAmmoRemove(PacketDroneAmmoRemove packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.ammo.Decrement(0, 1);
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnDroneAmmoClear(PacketDroneAmmoClear packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.ammo.Clear();
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnDroneAmmoAdd(PacketDroneAmmoAdd packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<DroneGadget>().drone.ammo.MaybeAddToSpecificSlot((Identifiable.Id)packet.Ident, null, 0, 1);
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Gadgets
        private static void OnGadgetEchoNetTime(PacketGadgetEchoNetTime packet, NetworkPlayer player)
        {
            if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
            {
                var echoNet = netSite.Site.GetComponentInChildren<EchoNet>(true);
                if (echoNet != null)
                {
                    echoNet.ResetSpawnTime(echoNet.model);
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetSnareGordo(PacketGadgetSnareGordo packet, NetworkPlayer player)
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
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetSnareAttach(PacketGadgetSnareAttach packet, NetworkPlayer player)
        {
            if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
            {
                var snare = netSite.Site.GetComponentInChildren<GordoSnare>(true);
                if (snare != null)
                {
                    snare.AttachBait((Identifiable.Id)packet.Ident);
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetTurrets(PacketGadgetTurrets packet, NetworkPlayer player)
        {
            if (Globals.GadgetSites.TryGetValue(packet.ID, out NetworkGadgetSite netSite))
            {
                foreach (var data in packet.Turrets)
                {
                    netSite.UpdateTurretRotation(data.Index, data.Rotation);
                }
            }
            packet.SendToAllExcept(player, NetDeliveryMethod.Unreliable);
        }

        private static void OnGadgetExtractorUpdate(PacketGadgetExtractorUpdate packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
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
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetSpawn(PacketGadgetSpawn packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
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
                GameObject prefab = SRSingleton<GameContext>.Instance.LookupDirector.GetGadgetDefinition((Gadget.Id)packet.Ident).prefab;
                var gadgetObj = SRSingleton<SceneContext>.Instance.GameModel.InstantiateGadget(prefab, model);
                gadgetObj.transform.eulerAngles = new Vector3(0, packet.Rotation, 0);

                if (didUnproxy)
                {
                    netSite.Region.Region.Proxy();
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetSpend(PacketGadgetSpend packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.GadgetDirector.SpendGadget((Gadget.Id)packet.ID);
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetAddBlueprint(PacketGadgetAddBlueprint packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.GadgetDirector.AddBlueprint((Gadget.Id)packet.ID);
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetAdd(PacketGadgetAdd packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.GadgetDirector.AddGadget((Gadget.Id)packet.ID);
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetRefinerySpend(PacketGadgetRefinerySpend packet, NetworkPlayer player)
        {
            foreach (var data in packet.Amounts)
            {
                SRSingleton<SceneContext>.Instance.GameModel.GetGadgetsModel().craftMatCounts[(Identifiable.Id)data.Key] -= data.Value;
                if (SRSingleton<SceneContext>.Instance.GameModel.GetGadgetsModel().craftMatCounts[(Identifiable.Id)data.Key] < 0)
                {
                    SRSingleton<SceneContext>.Instance.GameModel.GetGadgetsModel().craftMatCounts[(Identifiable.Id)data.Key] = 0;
                }
            }
            GameObject.FindObjectOfType<RefineryUI>()?.Rebuild();
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetRotation(PacketGadgetRotation packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.attached.transform.GetComponent<Gadget>().SetRotation(packet.Rotation);
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnGadgetRemove(PacketGadgetRemove packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllGadgetSites().TryGetValue(packet.ID, out GadgetSiteModel model))
            {
                if (model.HasAttached())
                {
                    model.transform.GetComponent<GadgetSite>().DestroyAttached();
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Fashions
        private static void OnFashionDetachAll(PacketFashionDetachAll packet, NetworkPlayer player)
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
            packet.SendToAllExcept(player);
        }

        private static void OnFashionAttach(PacketFashionAttach packet, NetworkPlayer player)
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
                var component = SRSingleton<GameContext>.Instance.LookupDirector.GetPrefab((Identifiable.Id)packet.Fashion)?.GetComponent<Fashion>();
                if (component != null)
                {
                    attachFashions.Attach(component, !attachFashions.gameObject.activeInHierarchy);
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Pedia
        private static void OnPediaUnlock(PacketPediaUnlock packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.PediaDirector.Unlock(packet.IDs.Select(i => (PediaDirector.Id)i).ToArray());
            packet.SendToAllExcept(player);
        }

        private static void OnPediaShowPopup(PacketPediaShowPopup packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.PediaDirector.MaybeShowPopup((PediaDirector.Id)packet.ID);
            packet.SendToAllExcept(player);
        }
        #endregion

        #region PuzzleSlots
        private static void OnPuzzleGateActivate(PacketPuzzleGateActivate packet, NetworkPlayer player)
        {
            var puzzleGateActivator = GameObject.FindObjectOfType<PuzzleGateActivator>();
            if (puzzleGateActivator != null && puzzleGateActivator.gameObject.activeInHierarchy)
            {
                puzzleGateActivator.StartCoroutine(puzzleGateActivator.DoDeactivateSequence());
            }
            packet.SendToAllExcept(player);
        }

        private static void OnPuzzleSlotFilled(PacketPuzzleSlotFilled packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllSlots().TryGetValue(packet.ID, out PuzzleSlotModel model))
            {
                if (!model.filled)
                {
                    model.filled = true;
                    var slot = model.gameObj.GetComponent<PuzzleSlot>();
                    SRBehaviour.SpawnAndPlayFX(slot.changeFX, slot.transform.position, slot.transform.rotation);
                    slot.ActivateOnFill();
                    if (slot.puzLockable != null)
                    {
                        slot.puzLockable.NotifySlotChanged(false);
                        SECTR_AudioCue cueForLastSlot = slot.puzLockable.GetCueForLastSlot();
                        SECTR_AudioSystem.Play(slot.localFillCue, slot.transform.position, false);
                        if (slot.gameObject.activeInHierarchy)
                        {
                            slot.StartCoroutine(slot.DelayedPlayLockCue(cueForLastSlot));
                        }
                    }
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region World
        private static void OnWorldMailRead(PacketWorldMailRead packet, NetworkPlayer player)
        {
            var mail = SRSingleton<SceneContext>.Instance.MailDirector.model.allMail.FirstOrDefault(m => m.Equals(new MailDirector.Mail((MailDirector.Type)packet.Type, packet.Key)));

            if (mail != null)
            {
                SRSingleton<SceneContext>.Instance.MailDirector.MarkRead(mail);
            }
            packet.SendToAllExcept(player);
        }

        private static void OnWorldMailSend(PacketWorldMailSend packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.MailDirector.SendMail((MailDirector.Type)packet.Type, packet.Key);
            packet.SendToAllExcept(player);
        }

        private static void OnWorldCredits(PacketWorldCredits packet, NetworkPlayer player)
        {
            SRSingleton<GameContext>.Instance.UITemplates.CreateCreditsPrefab(false);
            packet.SendToAllExcept(player);
        }

        private static void OnWorldMarketSold(PacketWorldMarketSold packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.EconomyDirector.RegisterSold((Identifiable.Id)packet.Ident, packet.Count);
        }

        private static void OnWorldDecorizerSetting(PacketWorldDecorizerSetting packet, NetworkPlayer player)
        {
            var storage = SRSingleton<SceneContext>.Instance.GameModel.decorizer.participants.FirstOrDefault(c => ((DecorizerStorage)c).id == packet.ID);
            if (storage != null)
            {
                ((DecorizerStorage)storage).selected = (Identifiable.Id)packet.Selected;
            }
            packet.SendToAllExcept(player);
        }

        private static void OnWorldDecorizer(PacketWorldDecorizer packet, NetworkPlayer player)
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
                var storage = SRSingleton<SceneContext>.Instance.GameModel.decorizer.participants.FirstOrDefault(c => ((DecorizerStorage)c).id == setting.Key);
                if (storage != null)
                {
                    ((DecorizerStorage)storage).selected = (Identifiable.Id)setting.Value;
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnWorldSelectPalette(PacketWorldSelectPalette packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.GameModel.GetRanchModel().SelectPalette((RanchDirector.PaletteType)packet.Type, (RanchDirector.Palette)packet.Pal);
            packet.SendToAllExcept(player);
        }

        private static void OnWorldSwitchActivate(PacketWorldSwitchActivate packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllSwitches().TryGetValue(packet.ID, out MasterSwitchModel model))
            {
                var master = model.gameObj.GetComponent<WorldStateMasterSwitch>();
                master.SetStateForAll((SwitchHandler.State)packet.State, !model.gameObj.activeInHierarchy);
                master.blockSwitchActivationUntil = Time.time + 2f;
            }
            packet.SendToAllExcept(player);
        }

        private static void OnWorldMapUnlock(PacketWorldMapUnlock packet, NetworkPlayer player)
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
            packet.SendToAllExcept(player);
        }

        private static void OnWorldKey(PacketWorldKey packet, NetworkPlayer player)
        {
            if (packet.Added)
            {
                SRSingleton<SceneContext>.Instance.PlayerState.AddKey();
            }
            else
            {
                SRSingleton<SceneContext>.Instance.PlayerState.SpendKey();
            }
            packet.SendToAllExcept(player);
        }

        private static void OnWorldProgress(PacketWorldProgress packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.ProgressDirector.SetProgress((ProgressDirector.ProgressType)packet.Type, packet.Amount);
            packet.SendToAllExcept(player);
        }

        private static void OnWorldFastForward(PacketWorldFastForward packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.TimeDirector.FastForwardTo(packet.FastForwardTill);
            packet.SendToAllExcept(player);
        }
        #endregion

        #region AccessDoors
        private static void OnAccessDoorOpen(PacketAccessDoorOpen packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllDoors().TryGetValue(packet.ID, out AccessDoorModel model))
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
                    SRSingleton<GameContext>.Instance.AutoSaveDirector.SaveAllNow();
                }
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region FX
        private static void OnPlayAudio(PacketPlayAudio packet, NetworkPlayer player)
        {
            if (Globals.Audios.TryGetValue(packet.CueName, out SECTR_AudioCue cue))
            {
                var before = cue.Spatialization;
                cue.Spatialization = SECTR_AudioCue.Spatializations.Local3D;
                SECTR_AudioSystem.Play(cue, packet.Position, packet.Loop);
                cue.Spatialization = before;
            }

            foreach (var p in Globals.Players.Values.ToList())
            {
                if (p.ID != player.ID && Vector3.Distance(p.transform.position, packet.Position) < 100)
                {
                    packet.Send(p, NetDeliveryMethod.Unreliable);
                }
            }
        }

        private static void OnIncinerateFX(PacketIncinerateFX packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
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
            packet.SendToAllExcept(player);
        }

        private static void OnGlobalFX(PacketGlobalFX packet, NetworkPlayer player)
        {
            if (Globals.FXPrefabs.TryGetValue(packet.Name, out GameObject prefabFX))
            {
                SRBehaviour.SpawnAndPlayFX(prefabFX, packet.Position, Quaternion.identity);
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region LandPlots
        private static void OnLandPlotSiloAmmoClear(PacketLandPlotSiloAmmoClear packet, NetworkPlayer player)
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
                SRMP.Log($"NetworkAmmo clear slot {packet.Slot} for {packet.ID}", "SERVER");
            }
            else
            {
                SRMP.Log("NetworkAmmo not found for clear: " + packet.ID, "SERVER");
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotSiloAmmoRemove(PacketLandPlotSiloAmmoRemove packet, NetworkPlayer player)
        {
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
            if (NetworkAmmo.All.TryGetValue(packet.ID, out NetworkAmmo ammo))
            {
                ammo.Decrement(packet.Slot, packet.Count);
                SRMP.Log($"NetworkAmmo remove slot {packet.Slot} (Amount: {packet.Count}) for {packet.ID}", "SERVER");
            }
            else
            {
                SRMP.Log("NetworkAmmo not found for remove: " + packet.ID, "SERVER");
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotSiloAmmoAdd(PacketLandPlotSiloAmmoAdd packet, NetworkPlayer player)
        {
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
            if (NetworkAmmo.All.TryGetValue(packet.ID, out NetworkAmmo ammo))
            {
                ammo.MaybeAddToSpecificSlot((Identifiable.Id)packet.Ident, null, packet.Slot, packet.Count, packet.Overflow);
                SRMP.Log($"NetworkAmmo add slot {packet.Slot} (Type: {(Identifiable.Id)packet.Ident} - Count: {packet.Count}) for {packet.ID}", "SERVER");
            }
            else
            {
                SRMP.Log("NetworkAmmo not found for add: " + packet.ID);
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotStartCollection(PacketLandPlotStartCollection packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
            {
                var collector = model.gameObj.GetComponentInChildren<PlortCollector>();
                if (collector != null)
                {
                    collector.StartCollection();
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotPlantGarden(PacketLandPlotPlantGarden packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
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
                            GameObject gameObject = GameObject.Instantiate<GameObject>(garden.activator.HasUpgrade(LandPlot.Upgrade.DELUXE_GARDEN) ? garden.deluxeDict[ident] : garden.plantableDict[ident], garden.activator.transform.position, garden.activator.transform.rotation);
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
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotUpgrade(PacketLandPlotUpgrade packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel landPlot))
            {
                var upgrade = (LandPlot.Upgrade)packet.Upgrade;
                GameObject plotObj = landPlot.gameObj;
                //SRMP.Log($"Upgrade {plotObj.name} with {upgrade}", "SERVER");
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
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotReplace(PacketLandPlotReplace packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
            {
                if (model.gameObj != null)
                {
                    model.InstantiatePlot(SRSingleton<GameContext>.Instance.LookupDirector.GetPlotPrefab((LandPlot.Id)packet.Type), false);
                    model.Init();

                    model.gameObj.GetComponentInChildren<LandPlot>(true)?.Awake();
                    model.gameObj.GetComponentInChildren<GardenCatcher>(true)?.Awake();
                    model.gameObj.GetComponentInChildren<SiloStorage>(true)?.Awake();

                    model.NotifyParticipants();
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotCollect(PacketLandPlotCollect packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
            {
                var collector = model.gameObj.GetComponentInChildren<PlortCollector>();
                model.collectorNextTime = packet.collectorNextTime;
                if (collector != null)
                {
                    collector.endCollectAt = packet.endCollectAt;
                    collector.forceCollectUntil = packet.forceCollectUntil;
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotFeederSpeed(PacketLandPlotFeederSpeed packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
            {
                model.gameObj.GetComponentInChildren<SlimeFeeder>().SetFeederSpeed((SlimeFeeder.FeedSpeed)packet.Speed);
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotSiloSlot(PacketLandPlotSiloSlot packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
            {
                model.siloStorageIndices[packet.ActivatorID] = packet.Slot;
                var activator = model.gameObj.GetComponentsInChildren<SiloStorageActivator>().FirstOrDefault(a => a.activatorIdx == packet.ActivatorID);
                if (activator != null)
                {
                    activator.OnActiveSlotChanged();
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotAsh(PacketLandPlotAsh packet, NetworkPlayer player)
        {
            if (SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.ID, out LandPlotModel model))
            {
                model.ashUnits = packet.Amount;
                model.gameObj.GetComponentInChildren<FillableAshSource>().UpdateAshPosition();
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotSiloRemove(PacketLandPlotSiloRemove packet, NetworkPlayer player)
        {
            var type = (SiloCatcher.Type)packet.CatcherType;
            var id = (Identifiable.Id)packet.Ident;
            switch (type)
            {
                case SiloCatcher.Type.DECORIZER:
                    SRSingleton<SceneContext>.Instance.GameModel.decorizer.Remove(id);
                    break;
                case SiloCatcher.Type.VIKTOR_STORAGE:
                    ((GlitchStorage)SRSingleton<SceneContext>.Instance.GameModel.Glitch.storage[packet.ID].participant).Remove(out id);
                    break;
            }
            packet.SendToAllExcept(player);
        }

        private static void OnLandPlotSiloInsert(PacketLandPlotSiloInsert packet, NetworkPlayer player)
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
                    ((GlitchStorage)SRSingleton<SceneContext>.Instance.GameModel.Glitch.storage[packet.ID].participant).Add(id);
                    break;
            }
            packet.SendToAllExcept(player);
        }
        #endregion

        #region Regions
        private static void OnRegionChange(PacketRegionChange packet, NetworkPlayer player)
        {
            if (Globals.Regions.TryGetValue(packet.ID, out NetworkRegion netRegion))
            {
                if (packet.Load)
                {
                    netRegion.AddPlayer(player);
                }
                else
                {
                    netRegion.RemovePlayer(player);
                }
            }
        }

        private static void OnRegionOwner(PacketRegionOwner packet, NetworkPlayer player)
        {
            if (Globals.Regions.TryGetValue(packet.ID, out NetworkRegion netRegion))
            {
                if (packet.Owner == 0 && netRegion.Owner == player.ID)
                {
                    netRegion.SetOwnership(0);
                    packet.SendToAll();
                }
                else if (packet.Owner != 0 && netRegion.Owner == 0)
                {
                    netRegion.SetOwnership(packet.Owner);
                    packet.SendToAll();
                }
                else
                {
                    packet.Owner = netRegion.Owner;
                    packet.Send(player);
                }
            }
        }
        #endregion

        #region Actors
        private static void OnActorFX(PacketActorFX packet, NetworkPlayer player)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var type = (PacketActorFX.FXType)packet.Type;
                var slimeEat = netActor.GetComponentInChildren<SlimeEat>();
                if (slimeEat != null)
                {
                    if (type == PacketActorFX.FXType.SlimeEatFavoriteFX)
                    {
                        SRBehaviour.SpawnAndPlayFX(slimeEat.EatFavoriteFX, slimeEat.transform.position, slimeEat.transform.rotation);
                    }
                    else if (type == PacketActorFX.FXType.SlimeEatFX)
                    {
                        SRBehaviour.SpawnAndPlayFX(slimeEat.EatFX, slimeEat.transform.position, slimeEat.transform.rotation);
                    }
                    else if (type == PacketActorFX.FXType.SlimeTransformFX)
                    {
                        SRBehaviour.SpawnAndPlayFX(slimeEat.TransformFX, slimeEat.transform.position, slimeEat.transform.rotation);
                    }
                    else if (type == PacketActorFX.FXType.SlimeProduceFX)
                    {
                        if (slimeEat.ProduceFX != null)
                        {
                            RecolorSlimeMaterial[] componentsInChildren = SRBehaviour.SpawnAndPlayFX(slimeEat.ProduceFX, slimeEat.transform.TransformPoint(SlimeEat.LOCAL_PRODUCE_LOC), slimeEat.transform.rotation).GetComponentsInChildren<RecolorSlimeMaterial>();
                            if (componentsInChildren != null && componentsInChildren.Length != 0)
                            {
                                SlimeAppearance.Palette appearancePalette = slimeEat.appearanceApplicator.GetAppearancePalette();
                                RecolorSlimeMaterial[] array = componentsInChildren;
                                for (int j = 0; j < array.Length; j++)
                                {
                                    array[j].SetColors(appearancePalette.Top, appearancePalette.Middle, appearancePalette.Bottom);
                                }
                            }
                        }
                    }
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnActorFeral(PacketActorFeral packet, NetworkPlayer player)
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
            packet.SendToAllExcept(player);
        }

        private static void OnActorEmotions(PacketActorEmotions packet, NetworkPlayer player)
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
            packet.SendToAllExcept(player);
        }

        private static void OnActorReproduceTime(PacketActorReproduceTime packet, NetworkPlayer player)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var reproduce = netActor.GetComponentInChildren<Reproduce>(true);
                if (reproduce != null)
                {
                    reproduce.model.nextReproduceTime = packet.Time;
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnActorResourceState(PacketActorResourceState packet, NetworkPlayer player)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                var cycle = netActor.GetComponentInChildren<ResourceCycle>(true);
                if (cycle != null)
                {
                    var state = (ResourceCycle.State)packet.State;
                    //SRMP.Log($"Resource state for {netActor.name} ({netActor.ID}): {state}", "SERVER");
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
            packet.SendToAllExcept(player);
        }

        private static void OnActorResourceAttach(PacketActorResourceAttach packet, NetworkPlayer player)
        {
            if (packet.PlotID.Length < 1)
            {
                if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor) && Globals.SpawnResources.TryGetValue(packet.ResourceID, out NetworkSpawnResource netSpawnResource))
                {
                    var cycle = netActor.GetComponentInChildren<ResourceCycle>(true);
                    var joint = netSpawnResource.SpawnResource.SpawnJoints[packet.JointIndex];
                    //SRMP.Log($"Adding {cycle.name} to {joint.gameObject.GetGameObjectPath()}", "SERVER");
                    cycle?.Attach(joint, new ResourceCycle.AdditionalRipeness(netSpawnResource.SpawnResource.AdditionalRipenessPerSecond), null);
                }
            }
            else
            {
                if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor) && SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().TryGetValue(packet.PlotID, out LandPlotModel model))
                {
                    var cycle = netActor.GetComponentInChildren<ResourceCycle>(true);
                    var spawner = model.gameObj.GetComponentInChildren<SpawnResource>(true);
                    var joint = spawner.SpawnJoints[packet.JointIndex];
                    //SRMP.Log($"Adding {cycle.name} to {joint.gameObject.GetGameObjectPath()}", "SERVER");
                    cycle?.Attach(joint, new ResourceCycle.AdditionalRipeness(spawner.AdditionalRipenessPerSecond), null);
                }
            }
            packet.SendToAllExcept(player);
        }

        private static void OnActorOwner(PacketActorOwner packet, NetworkPlayer player)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                if (packet.Owner == 0 && netActor.Owner == player.ID)
                {
                    netActor.SetOwnership(0);
                    packet.SendToAll();
                }
                else if (packet.Owner != 0)
                {
                    netActor.SetOwnership(packet.Owner);
                    packet.SendToAll();
                }
                else
                {
                    packet.Owner = netActor.Owner;
                    packet.Send(player);
                }
            }
        }

        private static void OnActorPosition(PacketActorPosition packet, NetworkPlayer player)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                if (netActor.Owner == player.ID)
                {
                    netActor.PositionRotationUpdate(packet.Position, packet.Rotation, false);

                    packet.SendToAllExcept(player, NetDeliveryMethod.Unreliable);
                }
            }
        }

        private static void OnActorDestroy(PacketActorDestroy packet, NetworkPlayer player)
        {
            if (Globals.Actors.TryGetValue(packet.ID, out NetworkActor netActor))
            {
                //Never take a client's word for destroying something it does not
                //own. A disconnecting or misbehaving client would otherwise be
                //able to delete the whole world out from under everyone.
                if (netActor.Owner != 0 && netActor.Owner != player.ID)
                {
                    SRMP.Log($"[Server] {player.Username} tried to destroy actor {packet.ID} "
                             + $"owned by {netActor.Owner}; ignored");
                    return;
                }

                netActor.OnDestroyEffect();
                Destroyer.DestroyActor(netActor.gameObject, "NetworkHandlerServer.OnActorDestroy");

                packet.SendToAllExcept(player);
                Globals.Actors.Remove(netActor.ID);
            }
        }

        private static void OnActorSpawn(PacketActorSpawn packet, NetworkPlayer player)
        {
            if (!Globals.Actors.ContainsKey(packet.ID))
            {
                var prefab = SRSingleton<GameContext>.Instance.LookupDirector.GetPrefab((Identifiable.Id)packet.Ident);
                var actorObj = SRBehaviour.InstantiateActor(prefab, (RegionRegistry.RegionSetId)packet.RegionSet, packet.Position, packet.Rotation, false);
                var netActor = actorObj.AddComponent<NetworkActor>();
                netActor.ID = packet.ID;
                netActor.Owner = player.ID;
                netActor.Ident = packet.Ident;
                netActor.RegionSet = packet.RegionSet;
                netActor.PositionRotationUpdate(packet.Position, packet.Rotation, true);
                netActor.KnownPlayers.AddRange(Globals.Players.Values.Where(p => p.HasLoaded));

                var resourceCycle = actorObj.GetComponentInChildren<ResourceCycle>(true);
                if (resourceCycle != null)
                {
                    resourceCycle.SetInitState(ResourceCycle.State.UNRIPE, double.MaxValue);
                }

                packet.Owner = player.ID;
                packet.SendToAllExcept(player);

                Globals.Actors.Add(netActor.ID, netActor);
            }
            else
            {
                SRMP.Log($"Dublicate Actor ID: {packet.ID}");
            }
        }
        #endregion

        #region Players
        /// <summary>
        /// Replies immediately so the round trip the client measures reflects the
        /// network, not our own scheduling, and rides the world clock along with
        /// it. The client's own measurement is recorded for the lobby list.
        /// </summary>
        private static void OnPing(PacketPing packet, NetworkPlayer player)
        {
            if (player != null)
            {
                player.Ping = packet.ReportedPing;
            }

            new PacketPong()
            {
                ClientTime = packet.ClientTime,
                WorldTime = SRSingleton<SceneContext>.Instance.TimeDirector.WorldTime()
            }.Send(player, NetDeliveryMethod.Unreliable);
        }

        private static void OnPlayerChat(PacketPlayerChat packet, NetworkPlayer player)
        {
            //commands are answered privately and never relayed to the lobby
            if (Server.ServerCommands.TryHandle(packet.message, player)) return;

            packet.message = player.Username + ": " + packet.message;
            packet.SendToAll();

            ChatUI.Instance.AddChatMessage(packet.message);
        }

        private static void OnPlayerUpgradeUnlock(PacketPlayerUpgradeUnlock packet, NetworkPlayer player)
        {
            if (!SRSingleton<SceneContext>.Instance.PlayerState.HasUpgrade((PlayerState.Upgrade)packet.Upgrade) &&
                !SRSingleton<SceneContext>.Instance.PlayerState.model.availUpgrades.Contains((PlayerState.Upgrade)packet.Upgrade))
            {
                SRSingleton<SceneContext>.Instance.PlayerState.model.availUpgrades.Add((PlayerState.Upgrade)packet.Upgrade);
                SRSingleton<SceneContext>.Instance.PlayerState.popupDir.QueueForPopup(new PlayerState.AvailUpgradePopupCreator((PlayerState.Upgrade)packet.Upgrade));
                SRSingleton<SceneContext>.Instance.PlayerState.popupDir.MaybePopupNext();
            }
            SRSingleton<SceneContext>.Instance.PlayerState.model.upgradeLocks.Remove((PlayerState.Upgrade)packet.Upgrade);

            packet.SendToAllExcept(player);
        }

        private static void OnPlayerUpgrade(PacketPlayerUpgrade packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.PlayerState.AddUpgrade((PlayerState.Upgrade)packet.Upgrade);
            packet.SendToAllExcept(player);
        }

        private static void OnPlayerCurrencyDisplay(PacketPlayerCurrencyDisplay packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.PlayerState.SetCurrencyDisplay(packet.IsNull ? null : new int?(packet.Currency));
            if (packet.IsNull)
            {
                SRSingleton<PopupElementsUI>.Instance.CreateCoinsPopup(packet.Currency, PlayerState.CoinsType.DRONE);
            }
        }

        private static void OnPlayerCurrency(PacketPlayerCurrency packet, NetworkPlayer player)
        {
            SRSingleton<SceneContext>.Instance.PlayerState.model.currency += packet.Adjust;
            SRSingleton<PopupElementsUI>.Instance.CreateCoinsPopup(packet.Adjust, (PlayerState.CoinsType)packet.Type);
            if (SRSingleton<SceneContext>.Instance.PlayerState.model.currency < 0)
            {
                SRSingleton<SceneContext>.Instance.PlayerState.model.currency = 0;
            }

            packet.Total = SRSingleton<SceneContext>.Instance.PlayerState.model.currency;
            packet.SendToAllExcept(player);
        }

        private static void OnPlayerFX(PacketPlayerFX packet, NetworkPlayer player)
        {
            var type = (PacketPlayerFX.FXType)packet.Type;

            switch (type)
            {
                case PacketPlayerFX.FXType.Capture:
                    SRBehaviour.SpawnAndPlayFX(player.CaptureFX, player.VacTransform.gameObject, Vector3.zero, Quaternion.identity);
                    break;
                case PacketPlayerFX.FXType.CaptureFailed:
                    SRBehaviour.SpawnAndPlayFX(player.CaptureFailedFX, player.VacTransform.gameObject, Vector3.zero, Quaternion.identity);
                    break;
                case PacketPlayerFX.FXType.Shoot:
                    SRBehaviour.SpawnAndPlayFX(player.ShootFX, player.VacTransform.gameObject, Vector3.zero, Quaternion.identity);
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

            packet.ID = player.ID;
            packet.SendToAllExcept(player);
        }

        private static void OnPlayerAnimation(PacketPlayerAnimation packet, NetworkPlayer player)
        {
            if (player.HasLoaded)
            {
                switch (packet.Type)
                {
                    case (byte)PacketPlayerAnimation.AnimationType.Speed:
                        player.ReadAnimatorSpeed(packet.internalData);
                        break;
                    case (byte)PacketPlayerAnimation.AnimationType.Layer:
                        player.ReadAnimatorLayer(packet.internalData);
                        break;
                    case (byte)PacketPlayerAnimation.AnimationType.Parameters:
                        player.ReadParameters(packet.internalData);
                        break;
                }

                //make the incoming message an out going message
                packet.SendToAllExcept(player, NetDeliveryMethod.Unreliable);
            }
        }

        private static void OnPlayerPosition(PacketPlayerPosition packet, NetworkPlayer netPlayer)
        {
            //get player id from packet in case of a teleport
            NetworkPlayer player = Globals.Players.Values.FirstOrDefault(p => p.ID.Equals(packet.ID));

            if (player.HasLoaded)
            {
                if (player.IsLocal) //if the server player is the one being moved, teleport them
                {
                    SRSingleton<SceneContext>.Instance.player.transform.position = packet.Position;
                    SRSingleton<SceneContext>.Instance.player.transform.eulerAngles = new Vector3(0, packet.Rotation, 0);
                    SRSingleton<SceneContext>.Instance.PlayerState.model.SetCurrRegionSet((RegionRegistry.RegionSetId)packet.RegionSet);

                    SRSingleton<Overlay>.Instance.PlayTeleport();
                }
                else //else process player movement
                {
                    player.PositionRotationUpdate(packet.Position, packet.Rotation, false);
                    player.UpdateWeaponRotation(packet.WeaponY);
                    player.CurrentRegionSet = (RegionRegistry.RegionSetId)packet.RegionSet;
                    packet.ID = player.ID;
                    packet.SendToAllExcept(netPlayer, NetDeliveryMethod.Unreliable);
                }

                
            }
        }

        private static void OnPlayerLoaded(PacketPlayerLoaded packet, NetworkPlayer player)
        {      
            var nicknamesEnabled = Globals.NicknamesModInstalled;

            new PacketWorldData()
            {
                Seed = SRSingleton<SceneContext>.Instance.EconomyDirector.worldModel.econSeed,
                Prices = SRSingleton<SceneContext>.Instance.EconomyDirector.currValueMap.ToDictionary(k => (ushort)k.Key, v => v.Value),
                Saturation = SRSingleton<SceneContext>.Instance.EconomyDirector.worldModel.marketSaturation.ToDictionary(k => (ushort)k.Key, v => v.Value),
                Keys = SRSingleton<SceneContext>.Instance.GameModel.player.keys,
                Currency = SRSingleton<SceneContext>.Instance.GameModel.player.currency,
                WorldTime = SRSingleton<SceneContext>.Instance.GameModel.world.worldTime,
                AvailUpgrades = SRSingleton<SceneContext>.Instance.GameModel.player.availUpgrades.Select(u => (byte)u).ToList(),
                Upgrades = SRSingleton<SceneContext>.Instance.GameModel.player.upgrades.Select(u => (byte)u).ToList(),
                MapUnlocks = SRSingleton<SceneContext>.Instance.GameModel.player.unlockedZoneMaps.Select(m => (byte)m).ToList(),
                Palette = SRSingleton<SceneContext>.Instance.GameModel.ranch.selectedPalettes.ToDictionary(p => (byte)p.Key, x => (ushort)x.Value),
                Progress = SRSingleton<SceneContext>.Instance.ProgressDirector.model.progressDict.ToDictionary(p => (ushort)p.Key, x => x.Value),
                GadgetsModel = SRSingleton<SceneContext>.Instance.GadgetDirector.model,
                PediaUnlocks = SRSingleton<SceneContext>.Instance.PediaDirector.pediaModel.unlocked.Select(u => (ushort)u).ToList(),
                Mails = SRSingleton<SceneContext>.Instance.MailDirector.model.allMail,
                LemonTrees = Globals.LemonTrees
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketWorldDecorizer()
            {
                Contents = SRSingleton<SceneContext>.Instance.GameModel.decorizer.contents.ToDictionary(c => c.Key, v => v.Value),
                Settings = SRSingleton<SceneContext>.Instance.GameModel.decorizer.settings.ToDictionary(s => s.Key, v => (ushort)v.Value.selected)
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketLandplots()
            {
                LandPlots = Globals.LandPlots.Values.Where(l => l.Plot.model != null).Select(l => new PacketLandplots.LandPlotData() { ID = l.Location.id, Model = l.Plot.model }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketGordos()
            {
                Gordos = Globals.Gordos.Values.Where(g => g.Gordo.gordoModel != null).Select(g => new PacketGordos.GordoData()
                {
                    ID = g.Gordo.id, 
                    Model = g.Gordo.gordoModel,
                    #if SRML
                    NicknameModValue = nicknamesEnabled ? (string)g.GetComponent(AccessTools.TypeByName("Nicknames.GordoNickname")).GetField("Name") : null,
                    #endif
                }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketAccessDoors()
            {
                Doors = Globals.AccessDoors.Values.Select(d => new PacketAccessDoors.AccessDoorData() { ID = d.Door.id, Model = d.Door.model }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketPuzzleSlots()
            {
                PuzzleSlots = Globals.PuzzleSlots.Values.Select(p => new PacketPuzzleSlots.PuzzleSlotData() { ID = p.Slot.id, Model = p.Slot.model }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketWorldSwitches()
            {
                Switches = Globals.Switches.Values.Select(s => new PacketWorldSwitches.WorldSwitchData() { ID = s.Switch.id, Model = s.Switch.model }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketTreasurePods()
            {
                TreasurePods = Globals.TreasurePods.Values.Select(p => new PacketTreasurePods.TreasurePodData() { ID = p.Pod.id, Model = p.Pod.model }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketOasis()
            {
                Oasis = SRSingleton<SceneContext>.Instance.GameModel.AllOases().Values.Select(o => new PacketOasis.OasisData() { ID = o.gameObj.GetComponent<Oasis>().id, Model = o }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketExchangeOffers()
            {
                pendingOfferRancherIds = SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.pendingOfferRancherIds,
                Offers = SRSingleton<SceneContext>.Instance.ExchangeDirector.worldModel.currOffers.Select(o => new PacketExchangeOffers.OfferData() { Type = (byte)o.Key, Offer = o.Value }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            new PacketGadgets()
            {
                Gadgets = Globals.GadgetSites.Values.Where(g => g.Site.model.attached != null).Select(g => new PacketGadgets.GadgetSiteData()
                {
                    ID = g.Site.id,
                    gadgetId = g.Site.model.attached.ident,
                    waitForChargeupTime = g.Site.model.attached.waitForChargeupTime,
                    yRotation = g.Site.model.attached.transform.localEulerAngles.y,
                    Model = g.Site.model.attached
                }).ToList()
            }.Send(player, NetDeliveryMethod.ReliableOrdered);

            player.HasLoaded = true;
            player.Spawn();
            player.Load();

            packet.ID = player.ID;
            packet.SendToAll();
        }
        #endregion
        
        #if SRML
        #region Mod Compatibility
        private static void OnSetNickname(PacketSetNickname packet, NetworkPlayer player)
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
            
            packet.SendToAllExcept(player);
        }

        private static void OnVRPositions(PacketVRPositions packet, NetworkPlayer player)
        {
            //Globals.Players.TryGetValue(packet.ID, out var player)
            
            player.IsVR = true;

            player.PositionRotationUpdateVR(packet);
            player.UpdateHeadRotationVR(packet.headAngle);
            
            packet.SendToAllExcept(player);
        }
        #endregion
        #endif
    }
}
