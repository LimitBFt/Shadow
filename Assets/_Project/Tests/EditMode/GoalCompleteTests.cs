#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Player;
using ShadowPath.Stars;
using ShadowPath.Tutorial;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowPath.Tests.EditMode
{
    public class GoalCompleteTests
    {
        private GameObject root;
        private GoalCompleteUI ui;
        private CanvasGroup group;
        private GoalTrigger goal;
        private Collider playerCollider;
        private StarCollectionManager manager;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Goal Test");
            GameObject uiOwner = Child("Goal UI");
            uiOwner.SetActive(false);
            ui = uiOwner.AddComponent<GoalCompleteUI>();
            group = uiOwner.GetComponent<CanvasGroup>();
            Set(ui, "canvasGroup", group);
            Invoke(ui, "Awake");
            uiOwner.SetActive(true);
            goal = Child("Goal").AddComponent<GoalTrigger>();
            Set(goal, "goalCompleteUI", ui);
            manager = Child("Star Manager").AddComponent<StarCollectionManager>();
            Set(goal, "starCollectionManager", manager);
            GameObject player = Child("Inactive Player");
            player.SetActive(false);
            player.AddComponent<PlayerController>();
            playerCollider = player.GetComponent<Collider>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Fade_WaitsThenUsesPassedUnscaledTimeAndNeverChangesTimeScale()
        {
            float scale = Time.timeScale;
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(ui.ShowCompletion(), Is.True);
            ui.AdvancePresentation(0.2f);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(ui.IsFullyVisible, Is.False);
            ui.AdvancePresentation(0.5f);
            Assert.That(group.alpha, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(ui.IsFullyVisible, Is.False);
            ui.AdvancePresentation(0.5f);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(ui.IsFullyVisible, Is.True);
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }

        [Test]
        public void Goal_RecognizesPlayerParentAndDoesNotRestartFadeOnReentry()
        {
            GameObject playerChild = new GameObject("Player child collider");
            playerChild.transform.SetParent(playerCollider.transform, false);
            Collider childCollider = playerChild.AddComponent<BoxCollider>();
            Enter(childCollider);
            Assert.That(goal.Completed, Is.True);
            ui.AdvancePresentation(0.7f);
            float alpha = group.alpha;
            Enter(playerCollider);
            Assert.That(group.alpha, Is.EqualTo(alpha));
            Assert.That(ui.ShowCompletion(), Is.True);
            Assert.That(group.alpha, Is.EqualTo(alpha), "Duplicate goals must not reset an existing fade.");
        }

        [Test]
        public void ArbitraryRigidbody_DoesNotCompleteGoal()
        {
            GameObject other = Child("Not Player");
            other.AddComponent<Rigidbody>();
            Enter(other.AddComponent<BoxCollider>());
            Assert.That(goal.Completed, Is.False);
            Assert.That(Get(ui, "completionRequested"), Is.False);
        }

        [Test]
        public void Goal_RejectsInsufficientLargeStarsAndCompletesOnReentry()
        {
            Set(manager, "requiredLargeStars", 2);
            var small = Child("Small Star").AddComponent<StarCollectible>();
            var firstLarge = Child("First Large Star").AddComponent<StarCollectible>();
            var secondLarge = Child("Second Large Star").AddComponent<StarCollectible>();
            Set(firstLarge, "starType", StarCollectible.StarType.Large);
            Set(secondLarge, "starType", StarCollectible.StarType.Large);
            Set(firstLarge, "scoreValue", 250);

            Assert.That(manager.TryRecordPickup(small), Is.True);
            Assert.That(manager.TryRecordPickup(firstLarge), Is.True);
            Assert.That(manager.TryRecordPickup(firstLarge), Is.False);
            Assert.That(manager.Score, Is.EqualTo(350));
            Assert.That(manager.CollectedSmallStars, Is.EqualTo(1));
            Assert.That(manager.CollectedLargeStars, Is.EqualTo(1));

            LogAssert.Expect(LogType.Warning,
                "COLLECT MORE STARS. Assign a Star HUD with a Goal Hint Label to show this in-game.");
            Enter(playerCollider);
            Assert.That(goal.Completed, Is.False);
            Assert.That(Get(ui, "completionRequested"), Is.False);

            Assert.That(manager.TryRecordPickup(secondLarge), Is.True);
            Enter(playerCollider);
            Assert.That(goal.Completed, Is.True);
            Assert.That(Get(ui, "completionRequested"), Is.True);
        }

        [Test]
        public void ZeroDurationFade_StillWaitsForCompletionDelay()
        {
            Set(ui, "fadeDuration", 0f);
            ui.ShowCompletion();
            ui.AdvancePresentation(0.2f);
            Assert.That(ui.IsFullyVisible, Is.False);
            ui.AdvancePresentation(0.11f);
            Assert.That(ui.IsFullyVisible, Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f));
        }

        [Test]
        public void RestartBeforeFadeCompletes_IsIgnoredWithoutLoadingScene()
        {
            ui.ShowCompletion();
            ui.AdvancePresentation(0.7f);
            Invoke(ui, "RestartCurrentScene");
            Assert.That(ui.IsFullyVisible, Is.False);
            Assert.That(Get(ui, "restartInProgress"), Is.False);
        }

        [Test]
        public void RestartHeldThroughFade_RequiresReleaseAndANewPress()
        {
            ui.ShowCompletion();
            ui.AdvancePresentation(0.5f);
            Assert.That(ui.ShouldRestart(false, true, true), Is.False);
            ui.AdvancePresentation(1f);
            Assert.That(ui.ShouldRestart(false, true, true), Is.False, "The last fade frame cannot restart.");
            Assert.That(ui.ShouldRestart(true, true, false), Is.False);
            Assert.That(ui.ShouldRestart(true, false, false), Is.False);
            Assert.That(ui.ShouldRestart(true, true, true), Is.True);
        }

        [Test]
        public void RestartNewPressAfterFade_IsAcceptedWithoutAnExtraConfirmation()
        {
            ui.ShowCompletion();
            ui.AdvancePresentation(2f);
            Assert.That(ui.ShouldRestart(false, false, false), Is.False);
            Assert.That(ui.ShouldRestart(true, true, true), Is.True);
        }

        private GameObject Child(string name)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(root.transform, false);
            return owner;
        }
        private void Enter(Collider collider) => Invoke(goal, "OnTriggerEnter", collider);
        private static object Invoke(object owner, string name, params object[] args) => owner.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        private static object Get(object owner, string name) => Field(owner, name).GetValue(owner);
    }
}
#endif
