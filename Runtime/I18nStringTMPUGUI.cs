using TMPro;

namespace Clpsplug.I18n.Runtime
{
    public class I18nStringTMPUGUI : TextMeshProUGUI
    {
        private I18nString str;

        public override string text
        {
            get => base.text;
            set
            {
                if (m_text != null && value != null && m_text.Length == value.Length && m_text == value)
                    return;
                str = I18nString.For(value);
                m_text = I18nString.For(value).GetStringByStringKey();
                m_havePropertiesChanged = true;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }

        public void ReloadText()
        {
            // This component takes I18nString key as text.
            text = str?.Key;
        }
    }
}