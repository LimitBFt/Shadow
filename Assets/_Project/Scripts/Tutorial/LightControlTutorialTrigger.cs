using ShadowPath.Lighting;
using ShadowPath.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShadowPath.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class LightControlTutorialTrigger : MonoBehaviour
    {
        private const float ManualInputThreshold = 0.2f;

        [SerializeField] private LightSourceController lightSourceController;
        [SerializeField] private GameObject tutorialPrompt;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField, Min(0f)] private float hideDelayAfterInput = 0.6f;
        [Tooltip("Normal HUD Canvas containing the prompt. Also used by the Create HUD Prompt context menu.")]
        [SerializeField] private Canvas promptCanvas;

        [Header("Runtime Debug")]
        [SerializeField] private bool hasTriggered;
        [SerializeField] private bool promptActive;
        [SerializeField] private bool tutorialPauseActive;
        [SerializeField] private Vector2 rawTutorialInput;
        [FormerlySerializedAs("manualInputDetected")]
        [SerializeField] private bool validInputDetected;
        [SerializeField] private bool tutorialCompleted;
        private float hidePromptAt;

        private void Awake()
        {
            // Pause ownership is runtime-only, even if Inspector debug values were serialized.
            tutorialPauseActive = false;
            if (HasSafePrompt()) tutorialPrompt.SetActive(false);
        }

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private bool HasSafePrompt()
        {
            return tutorialPrompt != null && !transform.IsChildOf(tutorialPrompt.transform)
                && (promptCanvas == null || tutorialPrompt.transform.IsChildOf(promptCanvas.transform));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || promptActive || (triggerOnce && hasTriggered)
                || other == null || other.GetComponentInParent<PlayerController>() == null)
                return;

            // Do not take ownership of a pause that belongs to another system/tutorial.
            if (Time.timeScale <= 0f) return;

            if (lightSourceController == null || !HasSafePrompt())
            {
                Debug.LogWarning("Light Control Tutorial requires a Light Source Controller and a separate HUD Prompt. "
                    + "Assign the HUD Canvas, then use Create HUD Prompt in this component's context menu.", this);
                return;
            }

            // The existing handover preserves the Light's position and clears motion history.
            lightSourceController.SetAutoFollow(false);
            tutorialPrompt.SetActive(true);
            hasTriggered = promptActive = true;
            validInputDetected = tutorialCompleted = false;
            rawTutorialInput = Vector2.zero;
            tutorialPauseActive = true;
            Time.timeScale = 0f;
        }

        private void LateUpdate()
        {
            if (!promptActive) return;
            // Dynamic Input System updates continue while scaled game time and physics are paused.
            UpdatePrompt(lightSourceController != null ? lightSourceController.RawManualInput : Vector2.zero,
                Time.unscaledTime);
        }

        internal void UpdatePrompt(Vector2 rawManualInput, float unscaledNow)
        {
            if (!promptActive) return;
            rawTutorialInput = rawManualInput;
            if (tutorialPauseActive)
            {
                if (rawTutorialInput.sqrMagnitude <= ManualInputThreshold * ManualInputThreshold) return;
                validInputDetected = tutorialCompleted = true;
                ReleaseTutorialPause();
                hidePromptAt = unscaledNow + Mathf.Max(0f, hideDelayAfterInput);
            }
            if (!tutorialCompleted || unscaledNow < hidePromptAt) return;
            if (tutorialPrompt != null) tutorialPrompt.SetActive(false);
            promptActive = false;
        }

        private void ReleaseTutorialPause()
        {
            if (!tutorialPauseActive) return;
            tutorialPauseActive = false;
            Time.timeScale = 1f;
        }

        private void OnDisable()
        {
            ReleaseTutorialPause();
            if (promptActive && HasSafePrompt()) tutorialPrompt.SetActive(false);
            promptActive = false;
        }

        private void OnDestroy()
        {
            ReleaseTutorialPause();
        }

        private void OnValidate()
        {
            hideDelayAfterInput = Mathf.Max(0f, hideDelayAfterInput);
        }
    }
}
