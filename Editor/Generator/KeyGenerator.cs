using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Clpsplug.I18n.Runtime;
using UnityEngine;

namespace Clpsplug.I18n.Editor.Generator
{
    internal class I18nGenerator
    {
        private readonly string _stringPath;
        private readonly string _namespace;
        private readonly string _outputLocation;
        private readonly int _indentIncrement;

        public I18nGenerator(string stringPath, string ns, string outputLocation, int indentIncrement)
        {
            _stringPath = stringPath;
            _namespace = ns;
            _outputLocation = outputLocation;
            _indentIncrement = indentIncrement;
        }

        public void OnGenerate()
        {
            List<LocalizedStringData> data;
            var parser = new I18nStringParser(_stringPath);
            try
            {
                var sl = SupportedLanguageLoader.GetInstance().SupportedLanguage;
                data = parser.Parse(sl);
            }
            catch (ArgumentNullException ane)
            {
                throw new OperationCanceledException("Cancelled because i18n resources are broken.", ane);
            }

            var location = Path.Join(Application.dataPath, _outputLocation);
            // First, make the top-level I18n key by generating a secondary partial class at specified location.
            if (!Directory.Exists(Directory.GetParent(location)?.FullName))
            {
                var fullName = Directory.GetParent(location)?.FullName;
                if (fullName != null)
                {
                    Directory.CreateDirectory(fullName);
                }
            }

            var sb = new StringBuilder();
            using var sw = new StreamWriter(location);
            sb.AppendLine("// Auto-generated by I18n Class Generator.");
            sb.AppendLine("// Any changes will be lost.");
            sb.AppendLine($"// String resource file hash: {parser.GetHash()}");
            sb.AppendLine("// ReSharper disable InconsistentNaming");
            sb.AppendLine("// ReSharper disable UnusedMember.Global\n"); // \n intentional
            var indentCount = 0;
            if (!string.IsNullOrEmpty(_namespace))
            {
                sb.AppendLine($"namespace {_namespace}\n{{");
                indentCount += _indentIncrement;
            }

            sb.AppendLine(
                $"{Indent(indentCount)}/// <summary>"
            );
            sb.AppendLine(
                $"{Indent(indentCount)}/// Members in this class can be supplied into <see cref=\"ExplodingCable.I18n.Runtime.LocalizedString.For\"/> parameter."
            );
            sb.AppendLine(
                $"{Indent(indentCount)}/// </summary>"
            );

            sb.AppendLine(
                $"{Indent(indentCount)}public static class I18nKeys\n{Indent(indentCount)}{{"
            );
            indentCount += _indentIncrement;
            sb.Append(RecursiveGenerateKeyClasses(data, indentCount));
            indentCount -= _indentIncrement;
            sb.AppendLine($"{Indent(indentCount)}}}");

            if (!string.IsNullOrEmpty(_namespace))
            {
                sb.AppendLine("}");
            }

            sw.Write(sb.ToString());
        }

        private string RecursiveGenerateKeyClasses(
            List<LocalizedStringData> input, int currentIndent,
            string parentSoFar = "")
        {
            var sb = new StringBuilder();
            foreach (var entry in input)
            {
                var textInfo = new CultureInfo("en-US", false).TextInfo;
                var saneKey = entry.Key.Replace('-', '_').Replace('.', '_');
                // Keys cannot have illegal characters for classes and member names. In such cases, crash the process.
                if (Regex.IsMatch(saneKey, @"[^\p{L}\p{N}_]"))
                {
                    throw new InvalidDataException(
                        $"There are keys ({entry.Key} is one of them) in the Internationalization file that are not suited for this operation.");
                }

                var titleKey = textInfo.ToTitleCase(saneKey);
                // If the data has child string, then call this again on children
                if (entry.Children != null)
                {
                    if (entry.LocalizationStrings.Count != 0)
                    {
                        sb.AppendLine($"{Indent(currentIndent)}/// <summary>");
                        foreach (var k in entry.LocalizationStrings.Keys)
                        {
                            var str = entry.LocalizationStrings[k].Replace("\n", " ");
                            if (str.Length > 30)
                            {
                                str = string.Concat(str.Take(30)) + "...";
                            }

                            // Because rich text tags borks XML, we replace them with html entities
                            str = str.Replace("<", "&lt;").Replace(">", "&gt;");
                            sb.AppendLine($"{Indent(currentIndent)}/// {k}: {str}<br/>");
                        }

                        sb.AppendLine($"{Indent(currentIndent)}/// </summary>");
                        sb.AppendLine(
                            $"{Indent(currentIndent)}public const string {saneKey} = \"{parentSoFar}{entry.Key}\";\n"
                        );
                    }

                    sb.AppendLine($"{Indent(currentIndent)}public static class {titleKey}\n{Indent(currentIndent)}{{");
                    currentIndent += _indentIncrement;
                    sb.Append(
                        RecursiveGenerateKeyClasses(
                            entry.Children,
                            currentIndent,
                            $"{parentSoFar}{entry.Key}."
                        )
                    );
                    currentIndent -= _indentIncrement;
                    sb.AppendLine($"{Indent(currentIndent)}}}\n"); // \n intentional
                }
                else
                {
                    // If we are at the leaf, create the leaf const members.
                    sb.AppendLine($"{Indent(currentIndent)}/// <summary>");
                    foreach (var k in entry.LocalizationStrings.Keys)
                    {
                        var str = entry.LocalizationStrings[k].Replace("\n", " ");
                        if (str.Length > 30)
                        {
                            str = string.Concat(str.Take(30)) + "...";
                        }

                        // Because rich text tags borks XML, we replace them with html entities
                        str = str.Replace("<", "&lt;").Replace(">", "&gt;");
                        sb.AppendLine($"{Indent(currentIndent)}/// {k}: {str}<br/>");
                    }

                    sb.AppendLine($"{Indent(currentIndent)}/// </summary>");
                    sb.AppendLine(
                        $"{Indent(currentIndent)}public const string {saneKey} = \"{parentSoFar}{entry.Key}\";"
                    );
                }
            }

            return sb.ToString();
        }

        private static string Indent(int count)
        {
            return new string(' ', count);
        }

        public IEnumerable<char> OnGetChars()
        {
            var sl = SupportedLanguageLoader.GetInstance().SupportedLanguage;
            var data = new I18nStringParser(_stringPath).Parse(sl);
            return RecursiveFindChars(data);
        }

        private static IEnumerable<char> RecursiveFindChars(List<LocalizedStringData> data)
        {
            var hashset = new HashSet<char>();
            foreach (var entry in data)
            {
                foreach (var langKey in entry.LocalizationStrings.Keys)
                {
                    entry.LocalizationStrings[langKey]
                        .ForEach(c => hashset.Add(c));
                }

                if (entry.Children != null)
                {
                    var result = RecursiveFindChars(entry.Children);
                    result.ForEach(c => hashset.Add(c));
                }
            }

            return hashset;
        }
    }
}