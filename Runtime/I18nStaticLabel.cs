using TMPro;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    [RequireComponent(typeof(TextMeshPro))]
    public class I18nStaticLabel : I18nStaticLabelBase
    {
        private TextMeshPro _text;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            _rectTransform = GetComponent<RectTransform>();
        }


        public override TMP_Text Text => _text;
        public override RectTransform RectTransform => _rectTransform;
    }
}