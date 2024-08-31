using TMPro;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class I18nStaticLabelUGUI : I18nStaticLabelBase
    {
        private TextMeshProUGUI _text;
        private RectTransform _rectTransform;

        protected override void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _rectTransform = GetComponent<RectTransform>();
            base.Awake();
        }

        public override TMP_Text Text => _text;
        public override RectTransform RectTransform => _rectTransform;
    }
}