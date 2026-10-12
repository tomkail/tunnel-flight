using NUnit.Framework;
using UnityEngine;

namespace TunnelFlight.Tests
{
    public class WallResponseTests
    {
        const float Dt = 0.02f;
        WallSettings settings;
        FlightModel flight;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<WallSettings>();
            flight = new FlightModel();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        // Ship at the origin heading `incidence` degrees into a wall whose outward normal is +x.
        static WallProbeSample WallAtPlusX(float clearance) => new WallProbeSample
        {
            valid = true,
            wall = new WallSample { clearance = clearance, wallRadius = 5f, normal = Vector2.right },
            worldNormal = Vector3.right,
            wallPoint = Vector3.right * 5f,
        };

        void HeadInto(float incidence) => flight.Reset(Vector3.zero, Quaternion.AngleAxis(incidence, Vector3.up));

        [Test]
        public void ClassifiesByNormalSpeed()
        {
            Assert.AreEqual(WallHitKind.Scrape, WallResponse.Classify(settings, settings.heavyHitThreshold - 0.01f));
            Assert.AreEqual(WallHitKind.Heavy, WallResponse.Classify(settings, settings.heavyHitThreshold));
            Assert.AreEqual(WallHitKind.Fatal, WallResponse.Classify(settings, settings.fatalThreshold));
        }

        [Test]
        public void NoContactWhenInside()
        {
            HeadInto(5f);
            WallHit hit = WallResponse.Resolve(settings, flight, 50f, WallAtPlusX(0.5f), Dt, out float scale);
            Assert.AreEqual(WallHitKind.None, hit.kind);
            Assert.AreEqual(1f, scale);
        }

        [Test]
        public void ScrapeDeflectsAlongTheWallAndPushesOut()
        {
            HeadInto(5f);
            float speed = 50f;
            WallHit hit = WallResponse.Resolve(settings, flight, speed, WallAtPlusX(-0.1f), Dt, out float scale);

            Assert.AreEqual(WallHitKind.Scrape, hit.kind);
            Assert.AreEqual(speed * Mathf.Sin(5f * Mathf.Deg2Rad), hit.normalSpeed, 1e-3f);
            Assert.AreEqual(0f, Vector3.Dot(flight.Forward, Vector3.right), 1e-4f, "Normal component removed.");
            Assert.AreEqual(-0.1f, flight.Position.x, 1e-5f, "Pushed back inside by the penetration depth.");
            Assert.Less(scale, 1f);
            Assert.Greater(hit.damage, 0f);
        }

        [Test]
        public void BoostingMakesTheSameAngleWorse()
        {
            HeadInto(10f);
            WallHit atCruise = WallResponse.Resolve(settings, flight, 30f, WallAtPlusX(-0.1f), Dt, out _);
            HeadInto(10f);
            WallHit atBoost = WallResponse.Resolve(settings, flight, 120f, WallAtPlusX(-0.1f), Dt, out _);

            Assert.AreEqual(WallHitKind.Scrape, atCruise.kind);
            Assert.AreEqual(WallHitKind.Fatal, atBoost.kind);
        }

        [Test]
        public void HeavyHitBouncesAndCostsSpeed()
        {
            float speed = 50f;
            float incidence = Mathf.Asin((settings.heavyHitThreshold + settings.fatalThreshold) * 0.5f / speed) * Mathf.Rad2Deg;
            HeadInto(incidence);
            WallHit hit = WallResponse.Resolve(settings, flight, speed, WallAtPlusX(-0.1f), Dt, out float scale);

            Assert.AreEqual(WallHitKind.Heavy, hit.kind);
            Assert.Less(Vector3.Dot(flight.Forward, Vector3.right), 0f, "Heading now points away from the wall.");
            Assert.AreEqual(settings.heavyHitSpeedRetained, scale);
            Assert.AreEqual(settings.heavyHitDamage, hit.damage);
        }

        [Test]
        public void HeadOnIsFatal()
        {
            HeadInto(90f);
            WallHit hit = WallResponse.Resolve(settings, flight, 50f, WallAtPlusX(-0.1f), Dt, out _);
            Assert.AreEqual(WallHitKind.Fatal, hit.kind);
            Assert.IsTrue(float.IsPositiveInfinity(hit.damage));
        }
    }
}

namespace TunnelFlight.Tests
{
    public class SparkBounceTests
    {
        [Test]
        public void BouncesBackOffTheWallAndKeepsSliding()
        {
            Vector3 v = new Vector3(10f, 0f, 30f);
            Vector3 result = TunnelParticleCollision.Bounce(v, Vector3.right, 0.5f, 0.8f);
            Assert.AreEqual(-5f, result.x, 1e-4f, "Into-wall speed reflected and scaled by bounciness.");
            Assert.AreEqual(24f, result.z, 1e-4f, "Along-wall speed kept at the slide fraction.");
        }

        [Test]
        public void MovingAwayFromTheWallIsNotReflected()
        {
            Vector3 v = new Vector3(-10f, 0f, 30f);
            Vector3 result = TunnelParticleCollision.Bounce(v, Vector3.right, 0.5f, 0.8f);
            Assert.Less(result.x, 0f);
        }
    }
}
