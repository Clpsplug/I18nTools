using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    using StringHashKey = UInt32;

    /// <summary>
    /// Supported language configuration interface
    /// </summary>
    public interface ISupportedLanguage
    {
        /// <summary>
        /// Get all the 'code' keys from supported languages.
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<int, string> GetLanguageCodes();

        /// <summary>
        /// Gets the dictionary with code key and displayable (user-friendly) string.
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<string, string> GetCodeDisplayPairs();

        /// <summary>
        /// Gets the code associated with language ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        string GetCodeFromId(int id);

        /// <summary>
        /// Gets displayable (user-friendly) string from language ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        string GetDisplayFromId(int id);

        /// <summary>
        /// How many supported languages are there?
        /// </summary>
        /// <returns></returns>
        int Count();
    }

    /// <summary>
    /// Supported language configuration
    /// </summary>
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

        /// <inheritdoc cref="ISupportedLanguage.GetLanguageCodes"/>
        public IReadOnlyDictionary<int, string> GetLanguageCodes()
        {
            return _langDict;
        }

        /// <inheritdoc cref="ISupportedLanguage.GetCodeDisplayPairs"/>
        public IReadOnlyDictionary<string, string> GetCodeDisplayPairs()
        {
            return _display;
        }

        /// <inheritdoc cref="ISupportedLanguage.GetCodeFromId"/>
        public string GetCodeFromId(int id)
        {
            return _langs[id];
        }

        /// <inheritdoc cref="ISupportedLanguage.GetDisplayFromId"/>
        public string GetDisplayFromId(int id)
        {
            return _display[_langDict[id]];
        }

        /// <inheritdoc cref="ISupportedLanguage.Count"/>
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

        /// <summary>
        /// Supported language entry
        /// </summary>
        [Serializable]
        public class SupportedLanguageInfo
        {
            /// <summary>
            /// Language ID, must be unique
            /// </summary>
            public int id;

            /// <summary>
            /// Displayable (user-friendly) language name
            /// </summary>
            public string display;

            /// <summary>
            /// Key to use in string resource files
            /// </summary>
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
        private readonly string key;

        private readonly StringHashKey hash;

        private readonly bool _isSoughtByHash;

        /// <summary>
        /// <see cref="I18nString"/> constructor, intentionally hidden
        /// </summary>
        /// <param name="key"></param>
        /// <seealso cref="I18nString.For(string)"/>
        private I18nString(string key)
        {
            this.key = key;
            hash = 0;
            _isSoughtByHash = false;
        }

        /// <summary>
        /// <see cref="I18nString"/> constructor, intentionally hidden
        /// </summary>
        /// <param name="hash">FNV-1a hash of string key</param>
        /// <seealso cref="I18nString.For(StringHashKey)"/>;
        private I18nString(StringHashKey hash)
        {
            key = null;
            this.hash = hash;
            _isSoughtByHash = true;
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
        /// Get an instance of the localized string for the key.
        /// All localizations can be retrieved from the return.
        /// </summary>
        /// <param name="hash">FNV-1a hash of the string</param>
        /// <returns></returns>
        public static I18nString For(StringHashKey hash)
        {
            return new I18nString(hash);
        }

        /// <summary>
        /// Gets <see cref="I18nString"/>s included within a certain element.
        /// This is useful when you want to randomly retrieve a string within a set.
        /// </summary>
        /// <returns></returns>
        public List<I18nString> GetChildren()
        {
            if (!_isSoughtByHash)
            {
                return I18nStringRepository.GetInstance().GetChildrenKeysForKey(key).Select(k => For($"{key}.{k}"))
                    .ToList();
            }

            var originalKey = I18nStringRepository.GetInstance().GetLocalizedStringData(hash).OriginalKey;
            return I18nStringRepository.GetInstance().GetChildrenKeysForKey(originalKey)
                .Select(k => For($"{originalKey}.{k}"))
                .ToList();
        }

        public string GetStringForLanguage(int langID)
        {
            return !_isSoughtByHash && string.IsNullOrEmpty(key)
                ? "No localization key specified!!!!!"
                : I18nStringRepository.GetInstance().GetLocalizedStringData(hash).LocalizationStrings[I18nStringRepository.SupportedLanguage.GetCodeFromId(langID)];
        }
        
        /// <summary>
        /// Attempt to pull string entry from the repository.
        /// </summary>
        /// <param name="valueDict"></param>
        /// <returns></returns>
        public string GetString(Dictionary<string, object> valueDict = null)
        {
            return !_isSoughtByHash && string.IsNullOrEmpty(key)
                ? "No localization key specified!!!!!"
                : I18nStringRepository.GetInstance().GetStringForCurrentLanguage(hash, valueDict);
        }

        /// <summary>
        /// Retrieve the localized text for the current language
        /// which is defined by <see cref="I18nStringRepository"/>.
        /// </summary>
        /// <returns></returns>
        public string GetStringByStringKey(Dictionary<string, object> valueDict = null)
        {
            return key == ""
                ? "No localization key specified!!!!!"
                : I18nStringRepository.GetInstance().GetStringForCurrentLanguage(key, valueDict);
        }

        public override string ToString()
        {
            return _isSoughtByHash ? GetString() : GetStringByStringKey();
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

        /// <summary>
        /// Localized string data as a tree structure, meant to be used for <see cref="GetChildrenKeysForKey"/>.
        /// </summary>
        private readonly List<LocalizedStringData> _data;

        /// <summary>
        /// Hashed version of the localized string. This is to be accessed first.
        /// </summary>
        private readonly Dictionary<StringHashKey, FlatLocalizedStringData> _hashedData;

        // TODO: Support different string definition names
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
            SupportedLanguage = SupportedLanguageLoader.GetInstance().SupportedLanguage;

            var config = Resources.Load<I18nStringConfig>("I18n/I18nStringConfig");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<I18nStringConfig>();
            }

            Path = config.StringSourcePath;
            var parser = new I18nStringParser(Path);
            _data = parser.Parse(SupportedLanguage);
            _hashedData = new Dictionary<StringHashKey, FlatLocalizedStringData>();
            parser.ParseForHashedString(SupportedLanguage, _hashedData);
        }

        /// <summary>
        /// Change the language pulled from this library
        /// </summary>
        /// <param name="id"></param>
        /// <remarks>
        /// Existing <see cref="I18nStaticLabelBase"/> components won't auto-update
        /// until you call <see cref="I18nStaticLabelBase.ReloadText"/>.
        /// </remarks>
        public void ChangeLanguage(int id)
        {
            _currentLanguageId = id;
        }


        public FlatLocalizedStringData GetLocalizedStringData(StringHashKey hash)
        {
            try
            {
                return _hashedData[hash];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves the string for the key and the language, replacing keys with <see cref="valueDict"/>.
        /// This method uses integer hash instead of string key.
        /// </summary>
        /// <param name="hash">Integer hash of string key. Use <see cref="StringExtension.Fnv1aHash"/>.</param>
        /// <param name="valueDict"></param>
        /// <returns></returns>
        public string GetStringForCurrentLanguage(StringHashKey hash, Dictionary<string, object> valueDict = null)
        {
            try
            {
                var stringData = _hashedData[hash];
                return PerformRequiredReplacement(
                    stringData.LocalizationStrings[SupportedLanguage.GetLanguageCodes()[_currentLanguageId]],
                    valueDict
                );
            }
            catch (KeyNotFoundException)
            {
                return $"String not localized for hash {hash}!!!";
            }
        }

        /// <summary>
        /// Retrieves the string for the key and the language, replacing keys with <see cref="valueDict"/>.
        /// </summary>
        /// <param name="key">key for the i18n string.</param>
        /// <param name="valueDict">
        /// If the string has replacement tokens ({token}),
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

        /// <summary>
        /// Used for string viewer. Not to be used for runtime.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public LocalizedStringData GetLocalizedStringFromKey(string key)
        {
            var explodedKey = key.Split('.');
            var rootLS = _data.First(ls => ls.Key == explodedKey.First());
            return FindStringRecursive(rootLS, explodedKey.Skip(1).ToArray());
        }

        private static string PerformRequiredReplacement(string origin, Dictionary<string, object> valueDict)
        {
            var unescapedNewline = origin.Replace("\\n", "\n");
            return valueDict == null ? unescapedNewline : unescapedNewline.FormatFromDictionary(valueDict);
        }

        /// <summary>
        /// Finds children key within the given string key.
        /// If there are none, this will return an empty <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
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

    /// <summary>
    /// <see cref="LocalizedStringData"/> that does not have the tree structure.
    /// The key for this class is hashed into an integer,
    /// making the search lightning faster than using <see cref="LocalizedStringData"/>.
    /// </summary>
    public class FlatLocalizedStringData
    {
        /// <summary>
        /// Original string key for debug purposes & child element finding purposes.
        /// </summary>
        public string OriginalKey;

        /// <summary>
        /// Same as <see cref="LocalizedStringData"/>,
        /// this holds text for each language.
        /// </summary>
        public Dictionary<string, string> LocalizationStrings { get; internal set; }

        /// <summary>
        /// Return 'substitute' language when unsupported string comes in for whatever reason.
        /// This should not fire to be honest.
        /// </summary>
        /// <returns></returns>
        public string GetSubstituteString()
        {
            return LocalizationStrings.TryGetValue("en", out var text)
                ? text
                : "This text is not localized, and attempt to get substitute string failed!";
        }
    }

    /// <summary>
    /// A localization string that is referenced by a file, not json.
    /// Useful for big blob of text.
    /// </summary>
    public class FileRefStringData
    {
        public string Key;
        
        public Dictionary<string, string> LocalizationStrings { get; internal set; }

        public string GetSubstituteString()
        {
            return LocalizationStrings.TryGetValue("en", out var text)
                ?text
                : "This text is not localized, attempt to get substitute string failed!";
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

        /// <summary>
        /// FNV-1a hash function
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static StringHashKey Fnv1aHash(this string input)
        {
            const StringHashKey fnvPrime = 16777619;
            const StringHashKey offsetBasis = 2166136261;

            var hash = offsetBasis;
            foreach (var c in input)
            {
                hash ^= c;
                hash *= fnvPrime;
            }

            return hash;
        }
    }
}