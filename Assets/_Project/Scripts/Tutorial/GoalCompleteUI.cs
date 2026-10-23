using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowPath.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class GoalCompleteUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0f)] private float completionDelay = 0.3f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.8f;

        [Header("Runtime Debug")]
        [SerializeField] private bool completionRequested;
        [SerializeField] private bool readyForRestart;
        [SerializeField] private bool restartInProgress;
        private float presentationElapsed;
        private bool restartArmed;

        public bool IsFullyVisible => readyForRestart;

        private void Awake()
        {
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
            completionRequested = readyForRestart = restartInProgress = restartArmed = false;
            presentationElapsed = 0f;
        }

        private void Reset()
        {
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public bool ShowCompletion()
        {
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("Keep the Goal Complete UI GameObject and component enabled; CanvasGroup hides it.", this);
                return false;
            }
            if (completionRequested) return true;
            EnsureCanvasGroup();
            completionRequested = true;
            readyForRestart = restartInProgress = restartArmed = false;
            presentationElapsed = 0f;
            canvasGroup.alpha = 0f;
            transform.SetAsLastSibling();
            return true;
        }

        private void Update()
        {
            bool wasFullyVisible = readyForRestart;
            AdvancePresentation(Time.unscaledDeltaTime);
            if (!readyForRestart || restartInProgress) return;
            if (ShouldRestart(wasFullyVisible, RestartIsHeld(), RestartWasPressed())) RestartCurrentScene();
        }

        internal bool ShouldRestart(bool wasFullyVisible, bool isHeld, bool wasPressed)
        {
            if (!readyForRestart || restartInProgress) return false;
            if (!wasFullyVisible)
            {
                // Never reuse the press that occurred on the last fade frame.
                restartArmed = !isHeld;
                return false;
            }
            if (!restartArmed)
            {
                restartArmed = !isHeld;
                return false;
            }
            return wasPressed;
        }

        internal void AdvancePresentation(float unscaledDeltaTime)
        {
            if (!completionRequested || readyForRestart) return;
            presentationElapsed += Mathf.Max(0f, unscaledDeltaTime);
            float fadeElapsed = presentationElapsed - Mathf.Max(0f, completionDelay);
            if (fadeElapsed < 0f) return;
            float duration = Mathf.Max(0f, fadeDuration);
            canvasGroup.alpha = duration <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / duration);
            readyForRestart = canvasGroup.alpha >= 1f;
        }

        private static bool RestartIsHeld() =>
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
            || (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed));

        private static bool RestartWasPressed() =>
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            || (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame
                || Keyboard.current.numpadEnterKey.wasPressedThisFrame));

        private void RestartCurrentScene()
        {
            if (!readyForRestart || restartInProgress) return;
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            if (buildIndex < 0)
            {
                Debug.LogWarning("Add the current scene to the active Build Profile Scene List and enable it before using Restart.", this);
                return;
            }
            restartInProgress = true;
            SceneManager.LoadScene(buildIndex);
        }

        private void OnValidate()
        {
            completionDelay = Mathf.Max(0f, completionDelay);
            fadeDuration = Mathf.Max(0f, fadeDuration);
        }
    }
}
