using UnityEngine;

namespace ShadowPath.Interaction
{
    public class ObjectHighlight : MonoBehaviour
    {
        [SerializeField] private Outline outline;

        private bool warnedMissingOutline;

        public void SetHighlighted(bool highlighted)
        {
            if (outline == null)
            {
                WarnMissingOutline();
                return;
            }

            outline.enabled = highlighted;
        }

        private void Awake()
        {
            if (outline == null)
            {
                outline = GetComponent<Outline>();
            }

            SetHighlighted(false);
        }

        private void WarnMissingOutline()
        {
            if (warnedMissingOutline)
            {
                return;
            }

            warnedMissingOutline = true;
            Debug.LogWarning("ObjectHighlight requires an Outline reference to show selection feedback.", this);
        }
    }
}
