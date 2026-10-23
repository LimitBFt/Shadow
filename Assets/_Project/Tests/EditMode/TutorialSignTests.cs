#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Lighting;
using ShadowPath.Player;
using ShadowPath.Tutorial;
using TMPro;
using UnityEngine;

namespace ShadowPath.Tests.EditMode
{
    public class TutorialSignTests
    {
        private GameObject root;
        private TutorialSign sign;
        private TutorialSignUI ui;
        private GameObject prompt;
        private GameObject panel;
        private TMP_Text title;
        private TMP_Text body;
        private Collider playerCollider;
        private LightSourceController light;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Sign Test");
            GameObject uiOwner = Child("UI Controller");
            uiOwner.SetActive(false);
            ui = uiOwner.AddComponent<TutorialSignUI>();
            prompt = Child("Read Prompt", uiOwner.transform);
            panel = Child("Tutorial Panel", uiOwner.transform);
            title = Label("Title");
            body = Label("Body");
            Set(ui, "interactionPrompt", prompt);
            Set(ui, "tutorialPanel", panel);
            Set(ui, "titleLabel", title);
            Set(ui, "bodyLabel", body);
            ui.HideTutorial();
            ui.HideInteractionPrompt();
            uiOwner.SetActive(true);

            GameObject lightOwner = Child("Inactive Light");
            lightOwner.SetActive(false);
            light = lightOwner.AddComponent<LightSourceController>();
            light.SetAutoFollow(true);
            light.SetManualControlEnabled(false);
            GameObject player = Child("Inactive Player");
            player.SetActive(false);
            player.AddComponent<PlayerController>();
            playerCollider = player.GetComponent<Collider>();
            sign = NewSign("Movement Sign");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void ProximityOnlyShowsReadPrompt_FirstSignNeverChangesGameplayOrTimeScale()
        {
            float scale = Time.timeScale;
            Enter(sign, playerCollider);
            Assert.That(prompt.activeSelf, Is.True);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(light.AutoFollowPlayer, Is.True);
            Assert.That(light.ManualControlEnabled, Is.False);
            ui.ToggleFocusedTutorial();
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(prompt.activeSelf, Is.False);
            Assert.That(body.text, Is.EqualTo("LEFT STICK — MOVE\nA — JUMP"));
            Assert.That(light.AutoFollowPlayer, Is.True);
            Assert.That(light.ManualControlEnabled, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            ui.ToggleFocusedTutorial();
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(prompt.activeSelf, Is.True);
        }

        [Test]
        public void LightSign_UnlocksOnlyOnFirstReadingWithoutSnap()
        {
            Set(sign, "unlockManualLightControl", true);
            Set(sign, "disableLightAutoFollow", true);
            Set(sign, "tutorialText", "RIGHT STICK — MOVE LIGHT");
            Vector3 position = new Vector3(7f, 8f, 9f);
            light.transform.position = position;
            Set(light, "autoFollowVelocity", Vector2.one);
            Enter(sign, playerCollider);
            Assert.That(light.AutoFollowPlayer, Is.True);
            ui.ToggleFocusedTutorial();
            Assert.That(light.AutoFollowPlayer, Is.False);
            Assert.That(light.ManualControlEnabled, Is.True);
            Assert.That(light.transform.position, Is.EqualTo(position));
            Assert.That(Get(light, "autoFollowVelocity"), Is.EqualTo(Vector2.zero));
            Assert.That(body.text, Is.EqualTo("RIGHT STICK — MOVE LIGHT"));
            ui.ToggleFocusedTutorial();
            light.SetAutoFollow(true);
            light.SetManualControlEnabled(false);
            ui.ToggleFocusedTutorial();
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(light.AutoFollowPlayer, Is.True, "Reading again must not replay gameplay actions.");
            Assert.That(light.ManualControlEnabled, Is.False);
            Assert.That(light.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void MultiplePlayerColliders_OnlyLastExitClosesTheTutorial()
        {
            Collider second = playerCollider.gameObject.AddComponent<BoxCollider>();
            Enter(sign, playerCollider);
            Enter(sign, second);
            ui.ToggleFocusedTutorial();
            Exit(sign, playerCollider);
            Assert.That(panel.activeSelf, Is.True);
            Exit(sign, second);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(prompt.activeSelf, Is.False);
        }

        [Test]
        public void OverlappingSigns_OnlyOneSignHandlesAnInteraction()
        {
            TutorialSign second = NewSign("Second Sign");
            Set(second, "unlockManualLightControl", true);
            Set(second, "disableLightAutoFollow", true);
            Enter(sign, playerCollider);
            Enter(second, playerCollider);
            ui.ToggleFocusedTutorial();
            Assert.That(light.ManualControlEnabled, Is.False);
            Exit(sign, playerCollider);
            Assert.That(ui.FocusedSign, Is.EqualTo(second));
            Assert.That(panel.activeSelf, Is.False);
            ui.ToggleFocusedTutorial();
            Assert.That(light.ManualControlEnabled, Is.True);
        }

        [Test]
        public void NonPlayerRigidbody_DoesNotShowReadPrompt()
        {
            GameObject other = Child("Not Player");
            other.AddComponent<Rigidbody>();
            Enter(sign, other.AddComponent<BoxCollider>());
            Assert.That(prompt.activeSelf, Is.False);
        }

        [Test]
        public void ManualPermission_DoesNotInterruptAutoFollowOrMoveTheLight()
        {
            Vector3 position = new Vector3(3f, 4f, 5f);
            light.transform.position = position;
            Set(light, "autoFollowVelocity", new Vector2(1f, 2f));
            light.SetManualControlEnabled(true);
            light.SetManualControlEnabled(false);
            Assert.That(light.AutoFollowPlayer, Is.True);
            Assert.That(Get(light, "autoFollowVelocity"), Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(light.RawManualInput, Is.EqualTo(Vector2.zero));
            Assert.That(light.HasManualLightInput, Is.False);
            Assert.That(light.transform.position, Is.EqualTo(position));
        }

        private TutorialSign NewSign(string name)
        {
            GameObject owner = Child(name);
            owner.SetActive(false);
            TutorialSign result = owner.AddComponent<TutorialSign>();
            Set(result, "tutorialUI", ui);
            Set(result, "lightSourceController", light);
            owner.SetActive(true);
            return result;
        }
        private TMP_Text Label(string name)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            owner.transform.SetParent(panel.transform, false);
            return owner.AddComponent<TextMeshProUGUI>();
        }
        private GameObject Child(string name, Transform parent = null)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(parent != null ? parent : root.transform, false);
            return owner;
        }
        private static void Enter(TutorialSign sign, Collider collider) => Invoke(sign, "OnTriggerEnter", collider);
        private static void Exit(TutorialSign sign, Collider collider) => Invoke(sign, "OnTriggerExit", collider);
        private static object Invoke(object owner, string name, params object[] args) => owner.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        private static object Get(object owner, string name) => Field(owner, name).GetValue(owner);
    }
}
#endif
