using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    public interface ISupportedLanguage
    {
        /// <summary>
        /// Return language ID as integer as key and language code as value.
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<int, string> GetLanguageCodes();

        IReadOnlyDictionary<string, string> GetCodeDisplayPairs();

        string GetCodeFromId(int id);

        string GetDisplayFromId(int id);

        int Count();
    }

    [Serializable]
    public class SupportedLanguage : ISupportedLanguage
    {
        private readonly List<string> _langs;
        private readonly Dictionary<int, string> _langDict;
        private readonly Dictionary<string, string> _display;

        [JsonConstructor]
        public SupportedLanguage(List<SupportedLanguageInfo> langs)
        {
            _langs = langs.Select(l => l.code).ToList();
            _langDict = langs.ToDictionary(l => l.id, l => l.code);
            _display = langs.ToDictionary(l => l.code, l => l.display);
        }

        public IReadOnlyDictionary<int, string> GetLanguageCodes()
        {
            return _langDict;
        }

        public IReadOnlyDictionary<string, string> GetCodeDisplayPairs()
        {
            return _display;
        }

        public string GetCodeFromId(int id)
        {
            return _langs[id];
        }

        public string GetDisplayFromId(int id)
        {
            return _display[_langDict[id]];
        }

        public int Count() => _langs.Count;
        
        public static SupportedLanguage DefaultSupportedLanguage()
        {
            return new SupportedLanguage(
                new List<SupportedLanguageInfo>
                {
                    new SupportedLanguageInfo { id = 0, code = "ja", display = "日本語" },
                    new SupportedLanguageInfo { id = 1, code = "en", display = "English" },
                }
            );
        }

        [Serializable]
        public class SupportedLanguageInfo
        {
            public int id;
            public string display;
            public string code;
        }
    }

    /// <summary>
    /// Localized String. To be used in the code.
    /// </summary>
    public class I18nString
    {
        /// <summary>
        /// <para>
        /// Unique string to point to the localized string. Can be namespaced with periods.
        /// </para>
        /// Either set by code or by inspector.
        /// </summary>
        public string key;

        /// <summary>
        /// <see cref="I18nString"/> constructor, intentionally hidden
        /// </summary>
        /// <param name="key"></param>
        /// <seealso cref="For"/>
        private I18nString(string key)
        {
            this.key = key;
        }

        /// <summary>
        /// Get an instance of the localized string for the key.
        /// All localizations can be retrieved from the return.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static I18nString For(string key)
        {
            return new I18nString(key);
        }

        /// <summary>
        /// Gets <see cref="I18nString"/>s included within a certain element.
        /// This is useful when you want to randomly retrieve a string within a set.
        /// </summary>
        /// <returns></returns>
        public List<I18nString> GetChildren()
        {
            return I18nStringRepository.GetInstance().GetChildrenKeysForKey(key).Select(k => For($"{key}.{k}"))
                .ToList();
        }

        /// <summary>
        /// Retrieve the localized text for the current language
        /// which is defined by <see cref="I18nStringRepository"/>.
        /// </summary>
        /// <returns></returns>
        public string GetString(Dictionary<string, object> valueDict = null)
        {
            return key == ""
                ? "No localization key specified!!!!!"
                : I18nStringRepository.GetInstance().GetStringForCurrentLanguage(key, valueDict);
        }

        public override string ToString()
        {
            return GetString();
        }

        /// <summary>
        /// Implicit operator to string type, no support for replacement tokens.
        /// </summary>
        /// <param name="ls"></param>
        /// <returns></returns>
        public static implicit operator string(I18nString ls) => ls.GetString();
    }

    /// <summary>
    /// Provider of the localized string.
    /// Intentionally made as a Unity singleton to prevent clogging up the code.
    /// It'll be used only here and its state will never change.
    /// If used outside here, then it's time to think about it
    /// </summary>
    public class I18nStringRepository
    {
        private static I18nStringRepository _self;

        private int _currentLanguageId;

        public static ISupportedLanguage SupportedLanguage { get; private set; }

        private readonly List<LocalizedStringData> _data;

        public static string Path { get; set; } = "strings";

        private static readonly object InitLock = new object();

        /// <summary>
        /// Retrieves the current instance of this repository.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "ConvertIfStatementToNullCoalescingAssignment")]
        [SuppressMessage("ReSharper", "InvertIf")]
        public static I18nStringRepository GetInstance()
        {
            if (_self == null)
            {
                lock (InitLock)
                {
                    if (_self == null)
                    {
                        _self = new I18nStringRepository();
                    }
                }
            }

            return _self;
        }

        /// <summary>
        /// Required method when 'skipping domain load' is enabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitOnPlayMode()
        {
            _self = null;
        }

        /// <summary>
        /// <see cref="I18nStringRepository"/> Constructor
        /// </summary>
        /// <exception cref="Exception">when Resources/Strings/strings.json cannot be loaded</exception>
        /// <exception cref="InvalidDataException">When deserialization somehow breaks</exception>
        private I18nStringRepository()
        {
            var supportedLanguageLoader = new SupportedLanguageLoader();
            SupportedLanguage = supportedLanguageLoader.LoadSupportedLanguage();
            var parser = new I18nStringParser(Path);
            _data = parser.Parse(SupportedLanguage);
        }

        public void ChangeLanguage(int id)
        {
            _currentLanguageId = id;
        }

        /// <summary>
        /// Retrieves the string for the key and the language, replacing keys with <see cref="valueDict"/>.
        /// </summary>
        /// <param name="key">key for the i18n string.</param>
        /// <param name="valueDict">
        /// If the string has replacement tokens ({token},)
        /// specify the substitutes with 'tokenKey'-'value' dictionary.
        /// </param>
        /// <returns>Localized string, but in case of non localized string, an error string will be returned.</returns>
        public string GetStringForCurrentLanguage(string key, Dictionary<string, object> valueDict = null)
        {
            try
            {
                var explodedKey = key.Split('.');
                var rootLS = _data.First(ls => ls.Key == explodedKey.First());
                var stringData = FindStringRecursive(rootLS, explodedKey.Skip(1).ToArray());

                return PerformRequiredReplacement(
                    stringData.LocalizationStrings[SupportedLanguage.GetLanguageCodes()[_currentLanguageId]],
                    valueDict
                );
            }
            catch (InvalidOperationException)
            {
                return $"String {key} not localized!!!!!";
            }
        }

        private static string PerformRequiredReplacement(string origin, Dictionary<string, object> valueDict)
        {
            var unescapedNewline = origin.Replace("\\n", "\n");
            return valueDict == null ? unescapedNewline : unescapedNewline.FormatFromDictionary(valueDict);
        }

        public IEnumerable<string> GetChildrenKeysForKey(string key)
        {
            try
            {
                var explodedKey = key.Split('.');
                var rootLS = _data.First(ls => ls.Key == explodedKey.First());
                var stringData = FindStringRecursive(rootLS, explodedKey.Skip(1).ToArray());
                return stringData.Children.Select(c => c.Key).ToList();
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException($"Given key {key} could not be found.", nameof(key));
            }
        }

        /// <summary>
        /// Finds the <see cref="I18nString"/> element in the l10n system
        /// with the given <see cref="keyArray"/>.
        /// </summary>
        /// <param name="parent">
        /// Specify the "root" element;
        /// the method merely starts search from here, so it doesn't have to be 'root' root.
        /// </param>
        /// <param name="keyArray">
        /// key specification. Each array element specifies a child element;
        /// e.g., ["root", "child", "string"] represents an element within an elem with "child" within an elem "root" key.
        /// </param>
        /// <exception cref="InvalidOperationException">If no element with given <see cref="keyArray"/> exists.</exception>
        /// <returns></returns>
        private static LocalizedStringData FindStringRecursive(LocalizedStringData parent,
            IReadOnlyCollection<string> keyArray)
        {
            return keyArray.Count switch
            {
                0 =>
                    // If root element is called, just return the parent.
                    parent,
                1 =>
                    // It's trying to find the last element, DO NOT RECURSIVE-CALL THIS ANYMORE.
                    parent.Children.First(ls => ls.Key == keyArray.First()),
                _ => FindStringRecursive(parent.Children.First(ls => ls.Key == keyArray.First()),
                    keyArray.Skip(1).ToArray()),
            };
        }
    }

    /// <summary>
    /// Internal data for localized strings. Not to be used directly.
    /// </summary>
    public class LocalizedStringData
    {
        /// <summary>
        /// String "Key," which is used to refer to this localized string.
        /// </summary>
        public string Key { get; internal set; }

        /// <summary>
        /// Dictionary of text for each language
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> LocalizationStrings { get; internal set; }

        /// <summary>
        /// To refer to the children <see cref="LocalizedStringData"/> contained here,
        /// concatenate (or implode) the keys from parent to child with a period.
        /// </summary>
        public List<LocalizedStringData> Children { get; internal set; }

        /// <summary>
        /// Return 'substitute' language when unsupported string comes in for whatever reason.
        /// This should not fire to be honest.
        /// </summary>
        /// <returns></returns>
        public string GetSubstituteString()
        {
            return LocalizationStrings.TryGetValue("en", out var text)
                ? text
                : $"{Key} not localized, attempt to get substitute string also failed!";
        }
    }

    public static class StringExtension
    {
        /// <summary>
        /// Implementation of the replacement token
        /// </summary>
        /// <param name="formatString"></param>
        /// <param name="valueDict"></param>
        /// <returns></returns>
        public static string FormatFromDictionary(this string formatString, Dictionary<string, object> valueDict)
        {
            var i = 0;
            var newFormatString = new StringBuilder(formatString);
            var keyToInt = new Dictionary<string, int>();
            foreach (var tuple in valueDict)
            {
                newFormatString = newFormatString.Replace("{" + tuple.Key + "}", "{" + i + "}");
                keyToInt.Add(tuple.Key, i);
                i++;
            }

            return string.Format(newFormatString.ToString(),
                valueDict
                    .OrderBy(x => keyToInt[x.Key])
                    .Select(x => x.Value).ToArray());
        }
    }
}