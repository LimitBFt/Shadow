using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowPath.Stars
{
    [DisallowMultipleComponent]
    public class StarCollectionManager : MonoBehaviour
    {
        [SerializeField, Min(0)] private int requiredLargeStars;

        private readonly HashSet<StarCollectible> collectedStars = new HashSet<StarCollectible>();

        public long Score { get; private set; }
        public int CollectedSmallStars { get; private set; }
        public int CollectedLargeStars { get; private set; }
        public int RequiredLargeStars => Mathf.Max(0, requiredLargeStars);
        public bool HasRequiredLargeStars => CollectedLargeStars >= RequiredLargeStars;

        internal bool TryRecordPickup(StarCollectible star)
        {
            if (!isActiveAndEnabled || star == null || star.gameObject.scene != gameObject.scene
                || !collectedStars.Add(star)) return false;

            Score += star.ScoreValue;
            if (star.Type == StarCollectible.StarType.Large) CollectedLargeStars++;
            else CollectedSmallStars++;
            return true;
        }

        public static StarCollectionManager FindUniqueInScene(Scene scene)
        {
            StarCollectionManager result = null;
            foreach (StarCollectionManager manager in FindObjectsByType<StarCollectionManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (manager.gameObject.scene != scene) continue;
                if (result != null) return null;
                result = manager;
            }
            return result;
        }
    }
}
