using Lidgren.Network;
using MonomiPark.SlimeRancher.DataModel;
using MonomiPark.SlimeRancher.Regions;
using Newtonsoft.Json;
using SRMultiplayer;
using SRMultiplayer.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SRMultiplayer.EpicSDK;
using UnityEngine;

namespace SRMultiplayer.Networking
{
    public partial class NetworkPlayer : MonoBehaviour
    {
        public Guid UUID { get; internal set; }

        public byte ID { get; internal set; }
        public string Username { get; internal set; }
        public bool IsLocal { get { return ID == Globals.LocalID; } }
        public List<string> Mods { get; set; }
        public List<DLCPackage.Id> DLCs { get; set; }

        public NetworkPlayerState State;

        public bool HasLoaded;

        public bool IsVR;

        /// <summary>
        /// Measured round trip time in milliseconds. Only the client can measure
        /// its own, so remote values arrive from the server via PacketPlayerPings.
        /// </summary>
        public int Ping;
        
        public RegionRegistry.RegionSetId CurrentRegionSet
        {
            get
            {
                return m_RegionSet;
            }
            set
            {
                m_RegionMember.actorModel.currRegionSetId = value;
                m_RegionSet = value;
            }
        }
        public QuicksilverEnergyGenerator CurrentGenerator
        {
            get
            {
                QuicksilverEnergyGenerator generator = null;
                foreach (var region in Globals.LocalPlayer.Regions)
                {
                    generator = region.GetComponent<QuicksilverEnergyGenerator>();
                    if (generator != null)
                    {
                        break;
                    }
                }
                return generator;
            }
        }

        private RegionRegistry.RegionSetId m_RegionSet;
        private RegionMember m_RegionMember;
        private Transform m_Label;
        private float m_MovementUpdateTimer;
        private Vector3 m_ActualPosition; // where we render
        private Vector3 m_LatestPosition; // last position reported by server
        private float m_LatestPositionTime; // the time we want to arrive at the last position reported by server
        private Vector3 m_PreviousPosition; // the previous point we're interpolating from
        private float m_InterpolationPeriod = 0.1f;
        private float m_PreviousRotation;
        private float m_LatestRotation;
        private float m_ActualRotation;
        private float m_ActualWeaponY;
        private float m_LatestWeaponY;
        private float m_PreviousWeaponY;
        private Animator m_Animator;
        private GameObject m_Camera;
        private SECTR_PointSource m_VacAudio;
        private SECTR_PointSource m_JetpackAudio;
        private SECTR_AudioCue m_VacStartCue;
        private SECTR_AudioCue m_VacRunCue;
        private SECTR_AudioCue m_VacEndCue;
        private SECTR_AudioCue m_JetpackStartCue;
        private SECTR_AudioCue m_JetpackRunCue;
        private SECTR_AudioCue m_JetpackEndCue;
        private bool m_VacAudioActive;
        private bool m_JetpackAudioActive;
        private Transform m_LeftHandTarget;
        private WeaponVacuum m_WeaponVacuum;

        public GameObject PlayerObj;
        public GameObject VacFX;
        public GameObject AirBurstFX;
        public GameObject ShootFX;
        public GameObject CaptureFX;
        public GameObject CaptureFailedFX;
        public GameObject DestroyOnVacFX;
        public Transform VacTransform;
        public List<NetworkRegion> Regions = new List<NetworkRegion>();
        public float airBurstRadius;
        public float airBurstPower;

        private void Update()
        {
            if (!HasLoaded) return;
            
            if(IsLocal)
            {
                m_MovementUpdateTimer -= Time.deltaTime;
                if (m_MovementUpdateTimer <= 0 && m_WeaponVacuum != null)
                {
                    if (Vector3.Distance(m_ActualPosition, SRSingleton<SceneContext>.Instance.Player.transform.position) > 0.1f || 
                        !Utils.CloseEnoughForMe(m_ActualRotation, SRSingleton<SceneContext>.Instance.Player.transform.eulerAngles.y, 0.1f) ||
                        !Utils.CloseEnoughForMe(m_ActualWeaponY, m_WeaponVacuum.transform.eulerAngles.x, 0.1f))
                    {
                        m_ActualPosition = SRSingleton<SceneContext>.Instance.Player.transform.position;
                        m_ActualRotation = SRSingleton<SceneContext>.Instance.Player.transform.eulerAngles.y;
                        m_ActualWeaponY = m_WeaponVacuum.transform.eulerAngles.x;

                        m_MovementUpdateTimer = 0.1f;
                        new PacketPlayerPosition()
                        {
                            ID = Globals.LocalID,
                            Position = SRSingleton<SceneContext>.Instance.Player.transform.position,
                            Rotation = SRSingleton<SceneContext>.Instance.Player.transform.eulerAngles.y,
                            RegionSet = (byte)SRSingleton<SceneContext>.Instance.PlayerState.model.currRegionSetId,
                            WeaponY = m_WeaponVacuum.transform.eulerAngles.x
                        }.Send();
                    }
                    m_Animator.SetFloat("MoveX", SRInput.Actions.horizontal);
                    m_Animator.SetFloat("MoveY", SRInput.Actions.vertical);
                }
            }
            else
            {
                if (m_Label != null)
                {
                    m_Label.rotation = Quaternion.LookRotation(m_Label.position - Camera.main.transform.position);
                }

                float t = 1.0f - ((m_LatestPositionTime - Time.time) / m_InterpolationPeriod);
                m_ActualPosition = Vector3.Lerp(m_PreviousPosition, m_LatestPosition, t);
                transform.position = m_ActualPosition;

                m_ActualRotation = Mathf.LerpAngle(m_PreviousRotation, m_LatestRotation, t);
                transform.eulerAngles = new Vector3(0, m_ActualRotation, 0);

                if (m_Animator != null && !IsVR)
                {
                    m_ActualWeaponY = Mathf.LerpAngle(m_PreviousWeaponY, m_LatestWeaponY, t);
                    m_Animator.SetFloat("WeaponY", m_ActualWeaponY);
                }
            }
            
            if (IsVR)
                VRPlayerUpdate();
        }

        public float GetWeaponLocation()
        {
            return m_ActualWeaponY;
        } 


        public void UpdateWeaponRotation(float angle)
        {
            if (angle > 180)
                angle -= 360;

            var val = (1 - (Mathf.InverseLerp(-90, 90, angle) * 2)) + 0.1f;
            m_LatestWeaponY = val;
            m_PreviousWeaponY = m_ActualWeaponY;
        }

        public void PositionRotationUpdate(Vector3 pos, float rot, bool immediate)
        {
            if(Vector3.Distance(pos, m_ActualPosition) > 10f) // Teleport detection
            {
                immediate = true;
            }
            if (!immediate)
            {
                m_LatestPosition = pos;
                m_LatestPositionTime = Time.time + m_InterpolationPeriod;
                m_PreviousPosition = m_ActualPosition;

                m_LatestRotation = rot;
                m_PreviousRotation = m_ActualRotation;
            }
            else
            {
                m_LatestPosition = pos;
                m_LatestPositionTime = Time.time + m_InterpolationPeriod;
                m_PreviousPosition = pos;
                m_ActualPosition = pos;
                transform.position = m_ActualPosition;

                m_PreviousRotation = rot;
                m_LatestRotation = rot;
                m_ActualRotation = rot;
                transform.eulerAngles = new Vector3(0, rot, 0);
            }
        }

        internal void Spawn()
        {
            SRMP.Log("Spawning " + Username);

            PlayerObj = GameObject.Instantiate(Globals.BeatrixModel);
            PlayerObj.transform.SetParent(transform);
            PlayerObj.transform.localPosition = Vector3.zero;

            m_Animator = PlayerObj.GetComponentInChildren<Animator>();
            m_Animator.runtimeAnimatorController = Globals.BeatrixController;
            m_Animator.applyRootMotion = false;

            StartCoroutine(SetupAnimator());

            m_WeaponVacuum = SRSingleton<SceneContext>.Instance.player.GetComponentInChildren<WeaponVacuum>();

            if (!IsLocal)
            {
                var barrel = PlayerObj.transform.FindDisabled("bone_barrel");
                var handle = PlayerObj.transform.FindDisabled("bone_handle");
                VacTransform = new GameObject("VacTransform").transform;
                VacTransform.SetParent(barrel, false);
                VacTransform.localEulerAngles = new Vector3(90, 0, 0);

                handle.localPosition = new Vector3(0.0092f, -0.00916f, 0.01449f);
                m_LeftHandTarget = new GameObject("LeftHandTarget").transform;
                m_LeftHandTarget.SetParent(handle, false);
                m_LeftHandTarget.localPosition = new Vector3(0.0174f, -0.0024f, -0.0045f);
                m_LeftHandTarget.localEulerAngles = new Vector3(27.636f, 72.67101f, -15.03f);

                var energyJetpack = SRSingleton<SceneContext>.Instance.player.GetComponentInChildren<EnergyJetpack>();
                m_JetpackAudio = energyJetpack.jetpackAudio.CopyComponent(PlayerObj);
                m_JetpackStartCue = energyJetpack.jetpackStartCue;
                m_JetpackRunCue = energyJetpack.jetpackRunCue;
                m_JetpackEndCue = energyJetpack.jetpackEndCue;
                
                m_VacAudio = m_WeaponVacuum.vacAudio.CopyComponent(PlayerObj);
                m_VacStartCue = m_WeaponVacuum.vacStartCue;
                m_VacRunCue = m_WeaponVacuum.vacRunCue;
                m_VacEndCue = m_WeaponVacuum.vacEndCue;

                airBurstPower = m_WeaponVacuum.airBurstPower;
                airBurstRadius = m_WeaponVacuum.airBurstRadius;

                AirBurstFX = m_WeaponVacuum.airBurstFX;
                CaptureFailedFX = m_WeaponVacuum.captureFailedFX;
                CaptureFX = m_WeaponVacuum.captureFX;
                DestroyOnVacFX = m_WeaponVacuum.destroyOnVacFX;
                ShootFX = m_WeaponVacuum.shootFX;
                VacFX = GameObject.Instantiate(m_WeaponVacuum.vacFX, VacTransform, false);
                VacFX.transform.localScale = new Vector3(.05f, .05f, .05f);
                VacFX.transform.localPosition = new Vector3(0, 0, 0);

                var realMarker = SRSingleton<SceneContext>.Instance.player.GetComponent<PlayerDisplayOnMap>();
                m_RegionMember = PlayerObj.AddComponent<RegionMember>();
                var mapDisplay = PlayerObj.AddComponent<NetworkPlayerDisplayOnMap>();
                m_RegionMember.canHibernate = false;
                m_RegionMember.actorModel = new PlayerModel();

                mapDisplay.Player = this;
                mapDisplay.Member = m_RegionMember;
                mapDisplay.markerPrefab = realMarker.markerPrefab;
                mapDisplay.HideInFog = realMarker.HideInFog;

                var label = new GameObject("PlayerLabel");
                var textMesh = label.AddComponent<TextMesh>();
                textMesh.text = $"{this}";
                textMesh.characterSize = 0.2f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.fontSize = 24;

                label.transform.SetParent(PlayerObj.transform);
                label.transform.localPosition = new Vector3(0, 3, 0);
                label.transform.localScale *= 0.5f;

                m_Label = label.transform;
                foreach (var collider in PlayerObj.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
                var col = PlayerObj.AddComponent<CapsuleCollider>();
                col.height = 2;
                col.center = new Vector3(0, 1.2f, 0);
            }
            else
            {
                PlayerObj.transform.localEulerAngles = new Vector3(0, 0, 0);
                foreach (var renderer in PlayerObj.GetComponentsInChildren<Renderer>())
                {
                    renderer.enabled = false;
                }
                foreach (var collider in PlayerObj.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }

                var cameraObj = new GameObject("Camera");
                cameraObj.transform.SetParent(PlayerObj.transform);
                cameraObj.transform.localPosition = new Vector3(0, 3, -4);
                cameraObj.transform.localEulerAngles = new Vector3(20, 0, 0);
                cameraObj.AddComponent<Camera>();
                cameraObj.SetActive(false);

                m_Camera = cameraObj;

                IsVR = Globals.VRInstalled;
            }
            
            GetVRObjects();
            
            PlayerObj.SetActive(true);
        }

        internal void Save()
        {
            var path = Path.Combine(SRMP.ModDataPath, SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.GetName(), UUID.ToString() + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new NetworkPlayerSave()
            {
                LastUsername = Username,
                RegionSet = (byte)CurrentRegionSet,
                WeaponY = m_LatestWeaponY,
                PositionX = transform.position.x,
                PositionY = transform.position.y,
                PositionZ = transform.position.z,
                Rotation = transform.eulerAngles.y
            }));
        }

        internal void Load()
        {
            var path = Path.Combine(SRMP.ModDataPath, SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.GetName(), UUID.ToString() + ".json");
            if (File.Exists(path))
            {
                var playerSave = JsonConvert.DeserializeObject<NetworkPlayerSave>(File.ReadAllText(path));
                transform.position = new Vector3(playerSave.PositionX, playerSave.PositionY, playerSave.PositionZ);
                transform.eulerAngles = new Vector3(0, playerSave.Rotation, 0);
                UpdateWeaponRotation(playerSave.WeaponY);
                CurrentRegionSet = (RegionRegistry.RegionSetId)playerSave.RegionSet;

                new PacketPlayerPosition()
                {
                    ID = ID,
                    Position = transform.position,
                    Rotation = transform.eulerAngles.y,
                    WeaponY = m_LatestWeaponY,
                    RegionSet = (byte)CurrentRegionSet
                }.Send(this);
            }
        }

        internal void OnLeft()
        {
            foreach (var netRegion in Globals.Regions.Values)
            {
                if (netRegion.Owner == ID)
                {
                    netRegion.SetOwnership(0);
                }
                netRegion.RemovePlayer(this);
            }
            foreach (var netActor in Globals.Actors.Values)
            {
                if (netActor.Owner == ID)
                {
                    netActor.SetOwnership(0);
                }
                netActor.KnownPlayers.Remove(this);
            }
        }

        public void Airburst()
        {
            Vector3 position = VacTransform.position;
            foreach (Collider collider in Physics.OverlapSphere(position, airBurstRadius))
            {
                if (collider && collider.GetComponent<Rigidbody>() != null && collider.gameObject != gameObject)
                {
                    Identifiable component = collider.gameObject.GetComponent<Identifiable>();
                    if (component != null && (Identifiable.IsSlime(component.id) || component.id == Identifiable.Id.PLAYER) && Vector3.Dot(VacTransform.transform.up, collider.gameObject.transform.position - VacTransform.transform.position) > 0f)
                    {
                        Rigidbody component2 = collider.GetComponent<Rigidbody>();
                        if (component.id == Identifiable.Id.PLAYER)
                        {
                            vp_FPController playerComponent = collider.gameObject.GetComponent<vp_FPController>();
                            Vector3 a = component2.position - position;
                            float magnitude = a.magnitude;
                            a.Normalize();
                            float num = 1f - Mathf.Max(2f, magnitude) / airBurstRadius;
                            var force = a * ((airBurstPower * component2.mass) * num * num);
                            playerComponent.AddForce(force * 0.001f);
                        }
                        else
                        {
                            PhysicsUtil.SoftExplosionForce(airBurstPower * component2.mass, position, airBurstRadius, component2);
                        }
                    }
                }
            }
            GameObject.Instantiate(AirBurstFX, Vector3.zero, Quaternion.identity).transform.SetParent(VacTransform, false);
        }

        internal void SetVacAudio(bool active)
        {
            if (active && !m_VacAudioActive)
            {
                m_VacAudio.Cue = m_VacStartCue;
                m_VacAudio.Play();
                m_VacAudio.Cue = m_VacRunCue;
                m_VacAudio.Play();
            }
            else if (!active && m_VacAudioActive)
            {
                m_VacAudio.Cue = m_VacEndCue;
                m_VacAudio.Play();
            }
            m_VacAudioActive = active;
        }

        internal void SetJetpackAudio(bool active)
        {
            if (active && !m_JetpackAudioActive)
            {
                m_JetpackAudio.Cue = m_JetpackStartCue;
                m_JetpackAudio.Play();
                m_JetpackAudio.Cue = m_JetpackRunCue;
                m_JetpackAudio.Play();
            }
            else if (!active && m_JetpackAudioActive)
            {
                m_JetpackAudio.Cue = m_JetpackEndCue;
                m_JetpackAudio.Play();
            }
            m_JetpackAudioActive = active;
        }

        internal void ToggleThirdPerson(bool enable)
        {
            foreach (var renderer in SRSingleton<SceneContext>.Instance.Player.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = !enable;
            }
            foreach (var renderer in PlayerObj.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = enable;
            }
            m_Camera.SetActive(enable);
        }

        public override bool Equals(object other)
        {
            if (other == null || !(other is NetworkPlayer)) return false;

            return ((NetworkPlayer)other).ID == ID;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Username} ({ID})";
        }

    }
}
