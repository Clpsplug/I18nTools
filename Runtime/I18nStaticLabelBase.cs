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

        private uint _hash;

        protected virtual void Awake()
        {
            _hash = key.Fnv1aHash();
        }

        private void Start()
        {
            Text.text = I18nString.For(_hash);
        }

        public abstract TMP_Text Text { get; }
        public abstract RectTransform RectTransform { get; }

        public void ReloadText()
        {
            Text.text = I18nString.For(_hash);
        }
    }
}