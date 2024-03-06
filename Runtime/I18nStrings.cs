using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace Clpsplug.I18n.Runtime
{
    public enum SupportedLanguage
    {
        Japanese,
        English,
        Max,
    }

    /// <summary>
    /// Localized String. To be used in the code.
    /// </summary>
    [Serializable]
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
            return key == "" ? "No localization key specified!!!!!" : I18nStringRepository.GetInstance().GetStringForCurrentLanguage(key, valueDict);
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

        private SupportedLanguage _currentLanguage;

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
            _currentLanguage = SupportedLanguage.English;

            var categoryTextAsset = Resources.Load<TextAsset>(Path);
            if (categoryTextAsset == null)
            {
                throw new Exception(
                    "Failed to load strings (strings.json,) this shouldn't happen");
            }

            var strings = JsonConvert.DeserializeObject<List<LocalizedStringData>>(categoryTextAsset.text);
            _data = strings;
        }

        public void ChangeLanguage(SupportedLanguage l)
        {
            _currentLanguage = l;
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

                return _currentLanguage switch
                {
                    SupportedLanguage.Japanese =>
                        PerformRequiredReplacement(stringData.LocalizationStrings["ja"], valueDict),
                    SupportedLanguage.English => PerformRequiredReplacement(stringData.LocalizationStrings["en"],
                        valueDict),
                    _ => PerformRequiredReplacement(stringData.GetSubstituteString(), valueDict),
                };
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
        private static LocalizedStringData FindStringRecursive(LocalizedStringData parent, IReadOnlyCollection<string> keyArray)
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
    [Serializable]
    public class LocalizedStringData
    {
        /// <summary>
        /// String "Key," which is used to refer to this localized string.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Dictionary of text for each language
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> LocalizationStrings { get; }

        /// <summary>
        /// To refer to the children <see cref="LocalizedStringData"/> contained here,
        /// concatenate (or implode) the keys from parent to child with a period.
        /// </summary>
        public List<LocalizedStringData> Children { get; }

        /// <summary>
        /// Used to deserialize from JSON files.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ja">If non-null, <see cref="en"/> OR <see cref="en_long"/> must also be non-null. This attribute will have precedence over <see cref="ja_long"/>.</param>
        /// <param name="ja_long">If non-null, <see cref="en"/> OR <see cref="en_long"/> must also be non-null</param>
        /// <param name="en">If non-null, <see cref="ja"/> OR <see cref="ja_long"/> must also be non-null. This attribute will have precedence over <see cref="en_long"/>.</param>
        /// <param name="en_long">If non-null, <see cref="ja"/> OR <see cref="ja_long"/> must also be non-null</param>
        /// <param name="strings">children <see cref="LocalizedStringData"/> array</param>
        /// <param name="exclude_newline">If true, newlines (\n) will not be appended for each element in _long key. If _long key is not used, this is simply no-op.</param>
        /// <exception cref="InvalidDataException">If malformed because:
        /// 1. <see cref="key"/> is null, 
        /// 2. <see cref="ja"/> and <see cref="en"/> aren't paired, or
        /// 3. none of <see cref="ja"/>, <see cref="en"/>, <see cref="strings"/> are present.
        /// </exception>
        /// <remarks>
        /// The "_long" variant of each language is for longer strings of text.
        /// Unlike the non-"_long" version, it can split up a string into an array of string within the JSON file
        /// for better readability of the l10n JSON file.
        /// </remarks>
        [JsonConstructor, Preserve]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public LocalizedStringData(
            string key,
            string ja,
            List<string> ja_long,
            string en,
            List<string> en_long,
            List<LocalizedStringData> strings,
            bool exclude_newline = false
        )
        {
            Key = key;

            if (Key == null)
            {
                throw new InvalidDataException(
                    "Key-less string is not allowed",
                    new ArgumentNullException(nameof(Key))
                );
            }

            if (
                Regex.IsMatch(
                    Key.Replace('-', '_').Replace('.', '_'),
                    @"[^\p{L}\p{N}_]")
            )
            {
                throw new InvalidDataException(
                    $"'{Key}' is not a valid key. A key cannot have non-alphanumeric characters except for -(dash), .(period), and _(underscore).");
            }

            var textInfo = new CultureInfo("en-US", false).TextInfo;
            if (Key == textInfo.ToTitleCase(Key))
            {
                Debug.LogWarning(
                    $"A 'TitleCase' key ({Key}) was found. This causes trouble with i18n key class generation. 'camelCase' is recommended.");
            }

            var localizationStrings = new Dictionary<string, string>();
            var localizationSourceSet =
                new List<(string localeKey, string oneLinerSource, List<string> multiLinerSource)>
                {
                    ("ja", ja, ja_long),
                    ("en", en, en_long),
                };
            foreach (var lss in localizationSourceSet)
            {
                string str;
                if (lss.oneLinerSource != null)
                {
                    str = lss.oneLinerSource;
                }
                else if (lss.multiLinerSource is { Count: > 0 })
                {
                    str = lss.multiLinerSource.Aggregate((c, n) => c + (!exclude_newline ? "\n" : "") + n);
                }
                else
                {
                    str = null;
                }

                if (str != null)
                {
                    localizationStrings.TryAdd(lss.localeKey, str);
                }
            }

            if (localizationSourceSet.Any(lss => !localizationStrings.ContainsKey(lss.localeKey)) &&
                strings == null)
            {
                throw new InvalidDataException(
                    $"Localized string for key {key} seems to be malformed",
                    new ArgumentNullException(
                        $"{nameof(strings)}, {nameof(ja)}, {nameof(en)}",
                        $"object must have 'strings,' OR both of 'ja' and 'en.' {key} seems to be missing those."
                    )
                );
            }

            LocalizationStrings = localizationStrings;
            Children = strings;
        }

        /// <summary>
        /// Return 'substitute' language when unsupported string comes in for whatever reason.
        /// This should not fire to be honest.
        /// </summary>
        /// <returns></returns>
        public string GetSubstituteString()
        {
            return LocalizationStrings.TryGetValue("en", out var text) ? text : $"{Key} not localized, attempt to get substitute string also failed!";
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

    public static class SupportedLanguageExtension
    {
        public static string AsKey(this SupportedLanguage l)
        {
            return l switch
            {
                SupportedLanguage.Japanese => "ja",
                SupportedLanguage.English => "en",
                SupportedLanguage.Max => throw new ArgumentException(),
                _ => throw new ArgumentOutOfRangeException(nameof(l), $"Got {(int)l} as language key but no such key exists"),
            };
        }

        public static string AsDisplayedLanguage(this SupportedLanguage l)
        {
            return l switch
            {
                SupportedLanguage.Japanese => "日本語",
                SupportedLanguage.English => "English",
                SupportedLanguage.Max => throw new ArgumentException(),
                _ => throw new ArgumentOutOfRangeException(nameof(l), $"Got {(int)l} as language key but no such key exists"),
            };
        }
    }
}