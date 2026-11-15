using TMPro;
using UnityEngine;

namespace ShadowPath.Stars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class StarHUD : MonoBehaviour
    {
        private const float GoalHintDuration = 2f;

        [SerializeField] private StarCollectionManager collectionManager;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text starCountLabel;
        [SerializeField] private TMP_Text goalHintLabel;

        private long displayedScore;
        private int displayedLargeStars;
        private int displayedRequiredStars;
        private float goalHintUntil;

        private void OnEnable()
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            if (goalHintLabel != null) goalHintLabel.enabled = false;
            displayedScore = -1;
            displayedLargeStars = displayedRequiredStars = -1;
            RefreshProgress();
        }

        private void Update()
        {
            RefreshProgress();
            if (goalHintLabel != null && goalHintLabel.enabled && Time.unscaledTime >= goalHintUntil)
                goalHintLabel.enabled = false;
        }

        private void RefreshProgress()
        {
            long score = collectionManager != null ? collectionManager.Score : 0;
            int largeStars = collectionManager != null ? collectionManager.CollectedLargeStars : 0;
            int requiredStars = collectionManager != null ? collectionManager.RequiredLargeStars : 0;

            if (score != displayedScore)
            {
                displayedScore = score;
                if (scoreLabel != null) scoreLabel.text = $"SCORE {score:D6}";
            }
            if (largeStars != displayedLargeStars || requiredStars != displayedRequiredStars)
            {
                displayedLargeStars = largeStars;
                displayedRequiredStars = requiredStars;
                if (starCountLabel != null) starCountLabel.text = $"STARS {largeStars} / {requiredStars}";
            }
        }

        internal bool ShowCollectMoreStars()
        {
            if (!isActiveAndEnabled || goalHintLabel == null || !goalHintLabel.gameObject.activeInHierarchy) return false;
            goalHintUntil = Time.unscaledTime + GoalHintDuration;
            goalHintLabel.text = "COLLECT MORE STARS";
            goalHintLabel.enabled = true;
            return true;
        }

        internal void HideGoalHint()
        {
            if (goalHintLabel != null) goalHintLabel.enabled = false;
        }
    }
}
