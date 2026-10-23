#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Lighting;
using ShadowPath.Player;
using ShadowPath.Tutorial;
using UnityEngine;

namespace ShadowPath.Tests.EditMode
{
    public class LightControlTutorialTests
    {
        private GameObject triggerObject;
        private GameObject lightObject;
        private GameObject prompt;
        private GameObject player;
        private LightControlTutorialTrigger trigger;
        private LightSourceController light;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            triggerObject = new GameObject("Tutorial Test");
            triggerObject.SetActive(false);
            trigger = triggerObject.AddComponent<LightControlTutorialTrigger>();
            lightObject = new GameObject("Inactive Light");
            lightObject.SetActive(false);
            light = lightObject.AddComponent<LightSourceController>();
            light.SetAutoFollow(true);
            prompt = new GameObject("Prompt");
            Set(trigger, "lightSourceController", light);
            Set(trigger, "tutorialPrompt", prompt);
            triggerObject.SetActive(true);
            Invoke(trigger, "Awake");
            player = new GameObject("Inactive Player");
            player.SetActive(false);
            player.AddComponent<PlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Object.DestroyImmediate(triggerObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(prompt);
                Object.DestroyImmediate(player);
            }
            finally { Time.timeScale = previousTimeScale; }
        }

        [Test]
        public void ArbitraryRigidbody_DoesNotGrantLightControl()
        {
            var other = new GameObject("Not a Player");
            try
            {
                other.AddComponent<Rigidbody>();
                Enter(other.AddComponent<BoxCollider>());
                Assert.That(light.AutoFollowPlayer, Is.True);
                Assert.That(prompt.activeSelf, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void PlayerEntry_HandsOverWithoutSnapAndOnlyRunsOnce()
        {
            Vector3 position = new Vector3(7f, 8f, 9f);
            lightObject.transform.position = position;
            Set(light, "autoFollowVelocity", Vector2.one);
            Set(light, "smoothedKeyboardInput", Vector2.one);
            Collider collider = player.GetComponent<Collider>();
            Enter(collider);
            Assert.That(light.AutoFollowPlayer, Is.False);
            Assert.That(lightObject.transform.position, Is.EqualTo(position));
            Assert.That(Get(light, "autoFollowVelocity"), Is.EqualTo(Vector2.zero));
            Assert.That(Get(light, "smoothedKeyboardInput"), Is.EqualTo(Vector2.zero));
            Assert.That(prompt.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Get(trigger, "tutorialPauseActive"), Is.True);
            trigger.UpdatePrompt(Vector2.zero, 1000f);
            Assert.That(prompt.activeSelf, Is.True, "No timeout may start before manual input.");
            Assert.That(Time.timeScale, Is.Zero);
            trigger.UpdatePrompt(Vector2.right, 1000f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Get(trigger, "tutorialPauseActive"), Is.False);
            Assert.That(Get(trigger, "validInputDetected"), Is.True);
            Assert.That(Get(trigger, "tutorialCompleted"), Is.True);
            Assert.That(light.AutoFollowPlayer, Is.False);
            Assert.That(lightObject.transform.position, Is.EqualTo(position));
            trigger.UpdatePrompt(Vector2.zero, 1000.5f);
            Assert.That(prompt.activeSelf, Is.True);
            trigger.UpdatePrompt(Vector2.zero, 1000.7f);
            Assert.That(prompt.activeSelf, Is.False);
            Assert.That(prompt != null, Is.True, "Hide must not destroy the UI.");
            light.SetAutoFollow(true);
            Enter(collider);
            Assert.That(light.AutoFollowPlayer, Is.True, "A completed one-shot trigger must not run again.");
            Assert.That(prompt.activeSelf, Is.False);
        }

        [Test]
        public void RepeatableTrigger_CanShowAgainAfterCompletion()
        {
            Set(trigger, "triggerOnce", false);
            Collider collider = player.GetComponent<Collider>();
            Enter(collider);
            trigger.UpdatePrompt(Vector2.right, 0f);
            trigger.UpdatePrompt(Vector2.zero, 2f);
            Enter(collider);
            Assert.That(prompt.activeSelf, Is.True);
            trigger.UpdatePrompt(Vector2.zero, 1000f);
            Assert.That(prompt.activeSelf, Is.True, "A new visit requires a new manual input.");
            Assert.That(Time.timeScale, Is.Zero);
        }

        [TestCase(0f, false)]
        [TestCase(0.19f, false)]
        [TestCase(0.2f, false)]
        [TestCase(0.201f, true)]
        [TestCase(-0.201f, true)]
        [TestCase(1f, true)]
        public void Pause_RequiresRawInputStrictlyAboveThreshold(float input, bool completes)
        {
            Enter(player.GetComponent<Collider>());
            trigger.UpdatePrompt(new Vector2(input, 0f), 50f);
            Assert.That(Get(trigger, "validInputDetected"), Is.EqualTo(completes));
            Assert.That(Get(trigger, "tutorialCompleted"), Is.EqualTo(completes));
            Assert.That(Time.timeScale, Is.EqualTo(completes ? 1f : 0f));
            Assert.That(prompt.activeSelf, Is.True);
        }

        [TestCase("OnDisable")]
        [TestCase("OnDestroy")]
        public void Cleanup_ResumesOnlyItsOwnPause(string callback)
        {
            Enter(player.GetComponent<Collider>());
            Assert.That(Time.timeScale, Is.Zero);
            Invoke(trigger, callback);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Get(trigger, "tutorialPauseActive"), Is.False);

            Time.timeScale = 0f; // Another system now owns a pause.
            Invoke(trigger, callback);
            Assert.That(Time.timeScale, Is.Zero, "Cleanup without ownership must not unpause another system.");
        }

        [Test]
        public void Trigger_DoesNotClaimAnExistingPause()
        {
            Time.timeScale = 0f;
            Enter(player.GetComponent<Collider>());
            Assert.That(Get(trigger, "tutorialPauseActive"), Is.False);
            Assert.That(light.AutoFollowPlayer, Is.True);
            Assert.That(prompt.activeSelf, Is.False);
        }

        [Test]
        public void PromptHide_UsesUnscaledTimeWithoutOverridingASubsequentPause()
        {
            Enter(player.GetComponent<Collider>());
            trigger.UpdatePrompt(Vector2.right, 100f);
            Time.timeScale = 0f; // Pause again after the tutorial already released ownership.
            trigger.UpdatePrompt(Vector2.zero, 100.7f);
            Assert.That(prompt.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
        }

        private void Enter(Collider collider) => Invoke(trigger, "OnTriggerEnter", collider);
        private static object Invoke(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        private static object Get(object owner, string name) => Field(owner, name).GetValue(owner);
    }
}
#endif
