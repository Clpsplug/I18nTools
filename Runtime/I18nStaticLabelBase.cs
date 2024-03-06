using TMPro;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    public abstract class I18nStaticLabelBase : MonoBehaviour
    {
        [SerializeField]
        [
            Tooltip(
                "Use I18n string viewer for quick assign. " +
                "Tools > ClpsPLUG > I18n > I18n String Viewer"
            ),
        ]
        protected string key;

        private void Start()
        {
            Text.text = I18nString.For(key);
        }

        public abstract TMP_Text Text { get; }
        public abstract RectTransform RectTransform { get; }

        public void ReloadText()
        {
            Text.text = I18nString.For(key);
        }
    }
}