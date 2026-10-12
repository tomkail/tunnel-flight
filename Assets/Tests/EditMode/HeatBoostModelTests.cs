using NUnit.Framework;
using UnityEngine;

namespace TunnelFlight.Tests
{
    public class HeatBoostModelTests
    {
        const float Dt = 0.02f;
        HeatBoostSettings settings;
        HeatBoostModel model;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<HeatBoostSettings>();
            model = new HeatBoostModel();
            model.Reset(settings);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        void Run(float seconds, bool boost)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) model.Step(settings, boost, Dt, Dt);
        }

        [Test]
        public void HeatRisesAtBaseRateWhenCruising()
        {
            Run(1f, false);
            Assert.AreEqual(settings.heatFillPerSecond, model.Heat, 0.01f);
            Assert.AreEqual(settings.normalMaxSpeed, model.Speed, 0.001f);
        }

        [Test]
        public void HeatRunsOnRealTime()
        {
            model.Step(settings, false, 0f, 1f);
            Assert.AreEqual(settings.heatFillPerSecond, model.Heat, 0.001f);
            Assert.AreEqual(settings.normalMaxSpeed, model.Speed, 0.001f);
        }

        [Test]
        public void CannotBoostWithoutHeat()
        {
            model.Step(settings, true, Dt, Dt);
            Assert.IsFalse(model.Boosting);
            Assert.AreEqual(settings.normalMaxSpeed, model.Speed, 0.001f);
        }

        [Test]
        public void BoostAcceleratesAndDrainsHeat()
        {
            model.Reset(settings, 50f);
            Run(1f, true);
            Assert.IsTrue(model.Boosting);
            Assert.Less(model.Heat, 50f);
            Assert.Greater(model.Speed, settings.normalMaxSpeed);
            Assert.LessOrEqual(model.Speed, settings.BoostSpeed);
        }

        [Test]
        public void HeldBoostStopsWhenHeatRunsOut()
        {
            model.Reset(settings, 10f);
            Run(3f, true);
            Assert.IsFalse(model.Boosting);
            Assert.Less(model.Heat, settings.minHeatToStartBoost + 1f);
        }

        [Test]
        public void ForcedLockEngagesAtFullHeatAndHoldsUntilEmpty()
        {
            int started = 0, ended = 0;
            model.ForcedLockStarted += () => started++;
            model.ForcedLockEnded += () => ended++;

            model.Reset(settings, 99.95f);
            model.Step(settings, false, Dt, Dt);
            Assert.IsTrue(model.ForcedLock);
            Assert.AreEqual(1, started);

            Run(1f, false);
            Assert.IsTrue(model.ForcedLock, "Lock holds while heat is above zero, even with boost released.");
            Assert.IsTrue(model.Boosting);

            Run(20f, false);
            Assert.IsFalse(model.ForcedLock);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void SpeedCoastsExponentiallyAfterRelease()
        {
            model.Reset(settings, 100f);
            Run(0.5f, true);
            float excess = model.Speed - settings.normalMaxSpeed;
            Assume.That(excess, Is.GreaterThan(1f));

            // The boosted model is still forced-locked, so coast from the same speed in a fresh model with no heat.
            var coast = new HeatBoostModel();
            coast.Reset(settings, 0f);
            coast.ScaleSpeed(model.Speed / settings.normalMaxSpeed);
            for (int i = 0; i < Mathf.RoundToInt(settings.coastTimeConstant / Dt); i++) coast.Step(settings, false, Dt, Dt);

            Assert.AreEqual(excess * Mathf.Exp(-1f), coast.Speed - settings.normalMaxSpeed, excess * 0.02f);
        }
    }
}
