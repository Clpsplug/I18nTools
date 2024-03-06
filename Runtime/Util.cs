using System;

namespace Clpsplug.I18n.Runtime
{
    public static class LanguageEnumExtension
    {
        public static SupportedLanguage ToSupportedLanguage(this int i)
        {
            if (i is < 0 or >= (int)SupportedLanguage.Max)
            {
                throw new ArgumentOutOfRangeException(nameof(i), $"Unknown supported language ID {i}");
            }
            return (SupportedLanguage)i;
        }
    }
}