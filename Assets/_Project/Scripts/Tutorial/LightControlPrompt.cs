using TMPro;
using UnityEngine;

namespace ShadowPath.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LightControlPrompt : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, TextArea(2, 4)] private string promptText = "RIGHT STICK\nMOVE LIGHT";

        private void OnEnable() => ApplyText();
        private void Reset() => ApplyText();
        private void OnValidate() => ApplyText();

        private void ApplyText()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (label != null) label.text = promptText;
        }
    }
}
