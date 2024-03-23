using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DG.DemiEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
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

    public class I18nStringParser
    {
        private readonly string _inputPath;

        public I18nStringParser(string inputPath)
        {
            _inputPath = inputPath;
        }

        public List<LocalizedStringData> Parse(ISupportedLanguage withSupportedLanguage)
        {
            var categoryTextAsset = Resources.Load<TextAsset>(_inputPath);
            if (categoryTextAsset == null)
            {
                throw new StringNotFoundException();
            }

            var obj = JArray.Parse(categoryTextAsset.text);
            return obj.Select(token => RecursiveFindStrings((JObject)token, withSupportedLanguage)).ToList();
        }

        private LocalizedStringData RecursiveFindStrings(JObject obj, ISupportedLanguage sl)
        {
            string key;
            Dictionary<string, string> langData;
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

            if (key.IsNullOrEmpty() ||
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
                    $"A 'TitleCase' key ({key}) was found. This causes trouble with i18n key class generation. 'camelCase' is recommended.");
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

            langData = sl.GetLanguageCodes().Values
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
            if (langData.All(kv => kv.Value.IsNullOrEmpty()))
            {
                langData = new Dictionary<string, string>();
            }
            else if (langData.Any(kv => kv.Value.IsNullOrEmpty()))
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