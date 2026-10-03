using UnityEngine;
using UnityEngine.Rendering;

namespace CameraClipFix
{
    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class CameraClipFix : MonoBehaviour
    {
        private const double initialDelay = 1; // 1 second wait before cam fixing

        private ConfigNode? settings = null;
        private double delayCounter = 0;
        private bool watchdogRun = false;
        private bool isSuborbital = false;

        public void Start()
        {
            settings = GameDatabase.Instance.GetConfigNodes("CameraClipFix").FirstOrDefault(n => n.HasNode("ClipPlanes"));

            GameEvents.onVesselSOIChanged.Add(OnVesselSOIChanged);
            GameEvents.onVesselSituationChange.Add(OnVesselSituationChanged);
        }

        public void OnDestroy()
        {
            GameEvents.onVesselSOIChanged.Remove(OnVesselSOIChanged);
            GameEvents.onVesselSituationChange.Remove(OnVesselSituationChanged);
        }

        public void Update()
        {
            if (watchdogRun)
                return;

            delayCounter += Time.deltaTime;

            if (delayCounter < initialDelay)
                return;

            watchdogRun = true;

            Camera[] cameras = Camera.allCameras;
            string bodyName = FlightGlobals.getMainBody().name;

            if (settings == null) return;

            ConfigNode? clipPlaneSettings;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
            {
                clipPlaneSettings = settings.GetNode("ClipPlanes")?.GetNode("DX11");
            }
            else
            {
                clipPlaneSettings = settings.GetNode("ClipPlanes")?.GetNode("Other");
            }

            if (clipPlaneSettings == null) return;

            foreach (Camera cam in cameras)
            {
                float farClip = -1;
                float nearClip = -1;

                if (cam.name.Equals("Camera 00"))
                {
                    clipPlaneSettings.TryGetValue("cam00FarClip", ref farClip);

                    if (clipPlaneSettings.HasNode(bodyName))
                        clipPlaneSettings.GetNode(bodyName).TryGetValue("cam00FarClip", ref farClip);

                    clipPlaneSettings.TryGetValue("cam00NearClip", ref nearClip);

                    if (clipPlaneSettings.HasNode(bodyName))
                        clipPlaneSettings.GetNode(bodyName).TryGetValue("cam00NearClip", ref nearClip);
                }
                else if (cam.name.Equals("Camera 01"))
                {
                    clipPlaneSettings.TryGetValue("cam01FarClip", ref farClip);

                    if (clipPlaneSettings.HasNode(bodyName))
                        clipPlaneSettings.GetNode(bodyName).TryGetValue("cam01FarClip", ref farClip);

                    clipPlaneSettings.TryGetValue("cam01NearClip", ref nearClip);

                    if (clipPlaneSettings.HasNode(bodyName))
                        clipPlaneSettings.GetNode(bodyName).TryGetValue("cam01NearClip", ref nearClip);
                }
                else if (cam.name.Equals("Camera ScaledSpace"))
                {
                    clipPlaneSettings.TryGetValue("camScaledSpaceFarClip", ref farClip);

                    if (clipPlaneSettings.HasNode(bodyName))
                        clipPlaneSettings.GetNode(bodyName).TryGetValue("camScaledSpaceFarClip", ref farClip);

                    clipPlaneSettings.TryGetValue("camScaledSpaceNearClip", ref nearClip);

                    if (clipPlaneSettings.HasNode(bodyName))
                        clipPlaneSettings.GetNode(bodyName).TryGetValue("camScaledSpaceNearClip", ref nearClip);
                }

                if (nearClip > 0)
                {
                    Debug.Log($"[CameraClipFix] Camera {cam.name} far clip was set to {cam.nearClipPlane}, changing to {nearClip}");
                    cam.nearClipPlane = nearClip;
                }

                if (farClip > 0)
                {
                    Debug.Log($"[CameraClipFix] Camera {cam.name} far clip was set to {cam.farClipPlane}, changing to {farClip}");
                    cam.farClipPlane = farClip;
                }
            }
        }

        public void OnVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> evt)
        {
            watchdogRun = false;
            delayCounter = 0;
        }

        private void OnVesselSituationChanged(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> data)
        {
            Vessel curVessel = data.host;
            if (!curVessel.mainBody.isHomeWorld || !curVessel.isActiveVessel) return;

            if (data.from == Vessel.Situations.FLYING && data.to == Vessel.Situations.SUB_ORBITAL)
            {
                isSuborbital = true;
            }
            else if (isSuborbital && data.to == Vessel.Situations.FLYING)
            {
                isSuborbital = false;
                Debug.Log("[CameraClipFix] Calling StartUpSphere() to prevent missing PQ tiles");
                curVessel.mainBody.pqsController.StartUpSphere();
            }
        }
    }
}
