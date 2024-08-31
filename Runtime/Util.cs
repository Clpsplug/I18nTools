using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    /// <summary>
    /// Gets the supported language configuration from the config file.
    /// </summary>
    public class SupportedLanguageLoader
    {
        private static SupportedLanguageLoader _self;

        private static readonly object InitLock = new object();

        /// <summary>
        /// Retrieves the current instance of this loader.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "ConvertIfStatementToNullCoalescingAssignment")]
        [SuppressMessage("ReSharper", "InvertIf")]
        public static SupportedLanguageLoader GetInstance()
        {
            if (_self == null)
            {
                lock (InitLock)
                {
                    if (_self == null)
                    {
                        _self = new SupportedLanguageLoader();
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

        private SupportedLanguageLoader()
        {
            var supportedLanguageTextAsset = Resources.Load<TextAsset>("I18n/SupportedLanguages");
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (supportedLanguageTextAsset == null)
            {
                SupportedLanguage = Runtime.SupportedLanguage.DefaultSupportedLanguage();
            }
            else
            {
                SupportedLanguage =
                    JsonConvert.DeserializeObject<SupportedLanguage>(supportedLanguageTextAsset.text);
            }
        }

        /// <summary>
        /// Get supported language configuration
        /// </summary>
        public ISupportedLanguage SupportedLanguage { get; }
    }

    internal enum ParseMode
    {
        ByString,
        ByHash,
    }

    /// <summary>
    /// Parser of the I18n string resource.
    /// Usually is not of much use user-side.
    /// </summary>
    public class I18nStringParser
    {
        private readonly string _inputPath;

        public I18nStringParser(string inputPath)
        {
            _inputPath = inputPath;
        }

        public string GetResourceHash()
        {
            var asset = Resources.Load<TextAsset>(_inputPath);
            if (asset == null)
            {
                throw new StringNotFoundException();
            }

            // Yes I know MD5 is weak but we're not dealing with cryptography here.
            var md5 = MD5.Create();
            var bs = md5.ComputeHash(asset.bytes);
            md5.Clear();
            return BitConverter.ToString(bs).ToLower().Replace("-", "");
        }

        /// <summary>
        /// Perform a parse and get the string data.
        /// </summary>
        /// <param name="withSupportedLanguage"></param>
        /// <returns></returns>
        /// <exception cref="StringNotFoundException"></exception>
        public List<LocalizedStringData> Parse(ISupportedLanguage withSupportedLanguage)
        {
            var categoryTextAsset = Resources.Load<TextAsset>(_inputPath);
            if (categoryTextAsset == null)
            {
                throw new StringNotFoundException();
            }

            var obj = JArray.Parse(categoryTextAsset.text);
            return obj.Select(token =>
                    RecursiveFindStrings((JObject)token, withSupportedLanguage))
                .ToList();
        }

        /// <summary>
        /// Perform a parse but with integer hash.
        /// </summary>
        /// <param name="withSupportedLanguage"></param>
        /// <param name="outDict"></param>
        public void ParseForHashedString(ISupportedLanguage withSupportedLanguage,
            Dictionary<uint, FlatLocalizedStringData> outDict)
        {
            var categoryTextAsset = Resources.Load<TextAsset>(_inputPath);
            if (categoryTextAsset == null)
            {
                throw new StringNotFoundException();
            }

            var obj = JArray.Parse(categoryTextAsset.text);
            foreach (var token in obj)
            {
                RecursiveFindStrings((JObject)token, withSupportedLanguage, "", outDict);
            }
        }

        private LocalizedStringData RecursiveFindStrings(JObject obj, ISupportedLanguage sl)
        {
            string key;
            var children = new List<LocalizedStringData>();
            bool excludeNewline;
            if (obj.TryGetValue("key", out var keyToken))
            {
                key = (string)keyToken;
            }
            else
            {
                throw new InvalidOperationException("Key-less string was found...");
            }

            if (string.IsNullOrEmpty(key) ||
                Regex.IsMatch(
                    key!.Replace('-', '_').Replace('.', '_'),
                    @"[^\p{L}\p{N}_]")
               )
            {
                throw new InvalidDataException(
                    $"'{key}' is not a valid key. A key cannot be null, have non-alphanumeric characters except for -(dash), .(period), and _(underscore).");
            }

            var textInfo = new CultureInfo("en-US", false).TextInfo;
            if (key == textInfo.ToTitleCase(key))
            {
                Debug.LogWarning(
                    $"A 'TitleCase' key ({key}) was found. This causes trouble with i18n key class generation. 'camelCase' or 'snake-case' is recommended.");
            }

            if (obj.TryGetValue("strings", out var stringToken))
            {
                foreach (var child in (JArray)stringToken)
                {
                    children.Add(RecursiveFindStrings((JObject)child, sl));
                }
            }
            else
            {
                children = null;
            }

            if (obj.TryGetValue("exclude_newline", out var newlineToken))
            {
                excludeNewline = (bool)newlineToken;
            }
            else
            {
                excludeNewline = false;
            }

            var langData = sl.GetLanguageCodes().Values
                .ToDictionary(code => code, code =>
                {
                    if (obj.TryGetValue(code, out var text))
                    {
                        return (string)text;
                    }

                    if (obj.TryGetValue(code + "_long", out var textList))
                    {
                        return string.Join(excludeNewline ? "" : "\n", ((JArray)textList).ToList());
                    }

                    return "";
                });
            if (langData.All(kv => string.IsNullOrEmpty(kv.Value)))
            {
                langData = new Dictionary<string, string>();
            }
            else if (langData.Any(kv => string.IsNullOrEmpty(kv.Value)))
            {
                throw new MalformedStringResourceException($"Key {key} has not been fully translated!");
            }

            return new LocalizedStringData
            {
                Key = key,
                LocalizationStrings = langData,
                Children = children,
            };
        }

        private void RecursiveFindStrings(JObject obj, ISupportedLanguage sl, string rootNamespace,
            Dictionary<uint, FlatLocalizedStringData> outDict)
        {
            string key;
            uint hashedKey;
            bool excludeNewline;
            if (obj.TryGetValue("key", out var keyToken))
            {
                key = (string)keyToken;
                hashedKey = (rootNamespace + key).Fnv1aHash();
            }
            else
            {
                throw new InvalidOperationException("Key-less string was found...");
            }

            if (string.IsNullOrEmpty(key) ||
                Regex.IsMatch(
                    key!.Replace('-', '_').Replace('.', '_'),
                    @"[^\p{L}\p{N}_]")
               )
            {
                throw new InvalidDataException(
                    $"'{key}' is not a valid key. A key cannot be null, have non-alphanumeric characters except for -(dash), .(period), and _(underscore).");
            }

            var textInfo = new CultureInfo("en-US", false).TextInfo;
            if (key == textInfo.ToTitleCase(key))
            {
                Debug.LogWarning(
                    $"A 'TitleCase' key ({key}) was found. This causes trouble with i18n key class generation. 'camelCase' or 'snake-case' is recommended.");
            }

            if (obj.TryGetValue("exclude_newline", out var newlineToken))
            {
                excludeNewline = (bool)newlineToken;
            }
            else
            {
                excludeNewline = false;
            }

            var langData = sl.GetLanguageCodes().Values
                .ToDictionary(code => code, code =>
                {
                    if (obj.TryGetValue(code, out var text))
                    {
                        return (string)text;
                    }

                    if (obj.TryGetValue(code + "_long", out var textList))
                    {
                        return string.Join(excludeNewline ? "" : "\n", ((JArray)textList).ToList());
                    }

                    return "";
                });

            if (langData.All(kv => string.IsNullOrEmpty(kv.Value)))
            {
                langData = new Dictionary<string, string>();
            }
            else if (langData.Any(kv => string.IsNullOrEmpty(kv.Value)))
            {
                throw new MalformedStringResourceException($"Key {key} has not been fully translated!");
            }

            /* Since this string data is 'flat', we can immediately add it to our dictionary. */
            outDict.Add(hashedKey, new FlatLocalizedStringData
            {
                OriginalKey = string.IsNullOrEmpty(rootNamespace) ? $"{key}" : $"{rootNamespace}.{key}",
                LocalizationStrings = langData,
            });

            if (!obj.TryGetValue("strings", out var stringToken)) return;
            foreach (var child in (JArray)stringToken)
            {
                RecursiveFindStrings((JObject)child, sl,
                    string.IsNullOrEmpty(rootNamespace) ? key + "." : rootNamespace + key + ".", outDict);
            }
        }
    }

    public class StringNotFoundException : Exception
    {
        public override string Message => "Specified string was not found as a TextAsset.";
    }

    public class MalformedStringResourceException : Exception
    {
        public MalformedStringResourceException(string message) : base(message)
        { }
    }
}