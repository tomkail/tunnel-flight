using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TunnelFlight.Tests
{
    // Smoke test of the flight scene on the real tunnel package: the level builds, the ship flies,
    // and with no input it eventually meets a wall without passing through it.
    public class FlightSceneTests
    {
        Ship ship;
        TunnelCamera tunnelCamera;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Flight", LoadSceneMode.Single);
            ship = Object.FindAnyObjectByType<Ship>();
            tunnelCamera = Object.FindAnyObjectByType<TunnelCamera>();
            for (int i = 0; i < 30 && !ship.Running; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator ShipStartsInsideTheTunnelAndFlies()
        {
            Assert.IsTrue(ship.Tunnel.IsReady, "Tunnel ready");
            Assert.IsTrue(ship.Running, "Ship running");
            Assert.Greater(ship.Tunnel.Length, 500f);
            Assert.Greater(ship.Sensor.Nearest.wall.clearance, 0.5f, "Starts clear of the walls");

            var tunnelMesh = Object.FindAnyObjectByType<Tunnel.TunnelMesh>();
            Assert.IsNotNull(tunnelMesh, "Tunnel mesh in scene");
            Assert.IsNotNull(tunnelMesh.Mesh, "Tunnel mesh built");
            Debug.Log($"[FlightSceneTests] Tunnel mesh: {tunnelMesh.Mesh.vertexCount} vertices, bounds {tunnelMesh.Mesh.bounds}, uv {tunnelMesh.Mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)}, normals {tunnelMesh.Mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal)}, renderer {tunnelMesh.GetComponent<MeshRenderer>().enabled}, material {tunnelMesh.GetComponent<MeshRenderer>().sharedMaterial?.name}");
            Assert.Greater(tunnelMesh.Mesh.vertexCount, 0, "Tunnel mesh has vertices");

            float start = ship.S;
            yield return Wait(1f);
            Assert.Greater(ship.S - start, 30f, "Covers ground at cruise speed");
            Capture("flight-1s");
            yield return Wait(1.5f);
            Capture("flight-2-5s");
        }

        [UnityTest]
        public IEnumerator FlyingStraightMeetsTheWallWithoutTunnelling()
        {
            bool touched = false;
            float worst = float.PositiveInfinity;
            ship.WallContact += _ => touched = true;

            bool capturedNear = false;
            for (float t = 0f; t < 8f && ship.Running; t += Time.deltaTime)
            {
                foreach (WallProbeSample sample in ship.Sensor.Samples) worst = Mathf.Min(worst, sample.wall.clearance);
                if (!capturedNear && ship.Sensor.Nearest.wall.clearance < 1.5f)
                {
                    capturedNear = true;
                    Capture("near-wall");
                }
                yield return null;
                if (touched) break;
            }

            Assert.IsTrue(touched || ship.Health.IsDead, "With no steering, the bend brings the wall to the ship");
            Assert.Greater(worst, -1f, $"Probes stay near the inside of the wall (worst clearance {worst:0.00} m)");
            Debug.Log($"[FlightSceneTests] First contact at s = {ship.S:0.0} m, kind {ship.LastHit.kind}, vn {ship.LastHit.normalSpeed:0.0} m/s, health {ship.Health.Current:0}");
        }

        [UnityTest]
        public IEnumerator ShipStaysInFrame()
        {
            var recorder = tunnelCamera.gameObject.AddComponent<FramingRecorder>();
            recorder.ship = ship;
            for (float t = 0f; t < 8f && ship.Running; t += Time.deltaTime) yield return null;
            Debug.Log($"[FlightSceneTests] Furthest probe from centre: {recorder.worst:0.00} of the half-frame, at s = {recorder.worstAt:0.0} m");
            Assert.AreEqual(0, recorder.behindCamera, "No probe behind the camera");
            Assert.Less(recorder.worst, 0.95f, "Wingtips and nose stay inside the frame");
        }

        [UnityTest]
        public IEnumerator LiftedCameraKeepsShipInFrameAndStaysInsideTheTunnel()
        {
            CameraSettings settings = tunnelCamera.Settings;
            float previousLift = settings.cameraLift;
            settings.cameraLift = 1.5f;
            try
            {
                var recorder = tunnelCamera.gameObject.AddComponent<FramingRecorder>();
                recorder.ship = ship;
                float minClearance = float.PositiveInfinity;
                bool captured = false;
                for (float t = 0f; t < 6f && ship.Running; t += Time.deltaTime)
                {
                    yield return null;
                    Vector3 eye = tunnelCamera.transform.position;
                    TunnelPoint point = ship.Tunnel.Project(eye, Mathf.Max(0f, ship.S - 10f));
                    minClearance = Mathf.Min(minClearance, ship.Tunnel.QueryWall(point).clearance);
                    if (!captured && t > 1f)
                    {
                        Capture("lifted-camera");
                        captured = true;
                    }
                }
                Debug.Log($"[FlightSceneTests] Lifted: furthest probe {recorder.worst:0.00} of the half-frame; camera clearance min {minClearance:0.00} m");
                Assert.AreEqual(0, recorder.behindCamera, "No probe behind the camera");
                Assert.Less(recorder.worst, 0.95f, "Wingtips and nose stay inside the frame");
                Assert.Greater(minClearance, 0f, "Camera stays inside the tunnel");
            }
            finally
            {
                settings.cameraLift = previousLift;
            }
        }

        // Measures after the camera's LateUpdate, so camera and ship are from the same frame.
        [DefaultExecutionOrder(10000)]
        sealed class FramingRecorder : MonoBehaviour
        {
            public Ship ship;
            public float worst, worstAt;
            public int behindCamera;
            Camera cam;

            void Awake() => cam = GetComponent<Camera>();

            void LateUpdate()
            {
                if (ship == null || !ship.Running) return;
                foreach (WallProbe probe in ship.Probes)
                {
                    Vector3 v = cam.WorldToViewportPoint(ship.transform.TransformPoint(probe.localOffset));
                    if (v.z <= 0f) behindCamera++;
                    float e = Mathf.Max(Mathf.Abs(v.x - 0.5f) * 2f, Mathf.Abs(v.y - 0.5f) * 2f);
                    if (e > worst)
                    {
                        worst = e;
                        worstAt = ship.S;
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator FirstPersonLooksAlongTheShipsHeading()
        {
            tunnelCamera.SetFirstPerson(true);
            yield return Wait(1f);
            Transform cam = tunnelCamera.transform;
            Assert.Less(Vector3.Distance(cam.position, ship.transform.position), 1f, "Eye is on the ship");
            Assert.Greater(Vector3.Dot(cam.forward, ship.transform.forward), 0.99f, "Looks along the heading");
            Capture("first-person");
            tunnelCamera.SetFirstPerson(false);
        }

        [UnityTest]
        public IEnumerator RespawnRebuildsTheTunnelAroundTheShip()
        {
            yield return Wait(3f);
            Assume.That(ship.S, Is.GreaterThan(100f));
            ship.TryRestart();
            yield return null;
            yield return null;

            var tunnelMesh = Object.FindAnyObjectByType<Tunnel.TunnelMesh>();
            Bounds bounds = tunnelMesh.Mesh.bounds;
            Vector3 local = tunnelMesh.transform.InverseTransformPoint(ship.transform.position);
            Assert.IsTrue(bounds.Contains(local), $"Mesh bounds {bounds} contain the respawned ship at {local}");
            Assert.Less(bounds.min.z, local.z, "Mesh starts behind the ship");
            Capture("after-respawn");
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        // Saves the game camera's view when TF_CAPTURE_DIR is set.
        void Capture(string name)
        {
            if (tunnelCamera != null) CaptureWith(tunnelCamera.GetComponent<Camera>(), name);
        }

        static void CaptureWith(Camera cam, string name)
        {
            string folder = System.Environment.GetEnvironmentVariable("TF_CAPTURE_DIR");
            if (string.IsNullOrEmpty(folder) || cam == null) return;
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(1280, 720, 24);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = target;
            cam.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            Object.Destroy(pixels);
            Object.Destroy(target);
        }
    }
}
