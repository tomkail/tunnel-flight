using NUnit.Framework;
using UnityEngine;

namespace TunnelFlight.Tests
{
    public class FlightModelTests
    {
        FlightSettings settings;
        FlightModel flight;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<FlightSettings>();
            settings.rateResponseTime = 0f;
            settings.invertPitch = false; // Tests use raw input: +pitch is nose up.
            flight = new FlightModel();
            flight.Reset(Vector3.zero, Quaternion.identity);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [Test]
        public void PositiveRollRollsRight()
        {
            flight.Step(settings, 1f, 0f, 0f, 0.1f);
            Vector3 up = flight.Rotation * Vector3.up;
            Assert.Greater(up.x, 0f, "Up should tilt toward the right when rolling right.");
            Assert.AreEqual(1f, Vector3.Dot(flight.Forward, Vector3.forward), 1e-5f, "Roll does not change heading.");
        }

        [Test]
        public void PositivePitchIsNoseUp()
        {
            flight.Step(settings, 0f, 1f, 0f, 0.1f);
            Assert.Greater(flight.Forward.y, 0f);
        }

        [Test]
        public void InvertPitchFlipsIt()
        {
            settings.invertPitch = true;
            flight.Step(settings, 0f, 1f, 0f, 0.1f);
            Assert.Less(flight.Forward.y, 0f);
        }

        [Test]
        public void MovesAlongHeadingAtSpeed()
        {
            flight.Step(settings, 0f, 0f, 50f, 0.02f);
            Assert.AreEqual(1f, flight.Position.z, 1e-5f);
        }

        [Test]
        public void RolledPitchTurnsTowardTheSide()
        {
            // Roll 90 degrees right, then pitch up: the heading turns right. That's how you reach a side wall.
            flight.Reset(Vector3.zero, Quaternion.AngleAxis(-90f, Vector3.forward));
            flight.Step(settings, 0f, 1f, 0f, 0.1f);
            Assert.Greater(flight.Forward.x, 0f);
        }
    }
}
