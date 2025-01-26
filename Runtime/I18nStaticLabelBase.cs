using System;
using TMPro;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    using StringHashKey = UInt32;

    public abstract class I18nStaticLabelBase : MonoBehaviour
    {
        [SerializeField]
        [
            Tooltip(
                "Use I18n string viewer for quick assignment. " +
                "Tools > ClpsPLUG > I18n > I18n String Viewer"
            ),
        ]
        protected string key;

        private StringHashKey _hash;

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

        public void SetKey(string key)
        {
            this.key = key;
            _hash = key.Fnv1aHash();
        }

        public void SetKey(StringHashKey key)
        {
            _hash = key;
        }
    }
}