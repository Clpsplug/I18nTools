using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Clpsplug.I18n.Editor.Generator;
using Clpsplug.I18n.Runtime;
using UnityEditor;
using UnityEditor.iOS;
using UnityEngine;

namespace Clpsplug.I18n.Editor
{
    public class I18nToolsWindow : EditorWindow
    {
        private string _stringPath;
        private string _namespace;
        private string _outputLocation;
        private bool _includeNumericAndSymbolsInChar;
        private bool _includeAlphaInChar;
        private string _usedChars;
        private bool _isStringPathValid;
        private bool _outputDirExists;
        private bool _outputFileExists;
        private bool _isOutputPathFile;
        private bool _isOutputPathCs;
        private bool _isGeneratingClass;
        private bool _isGeneratingChars;
        private const int IndentIncrement = 4;

        private const string EditorPrefKey = "com.expoding-cable.i18n-window";

        private readonly Regex regex = new Regex("// String resource file hash: ([0-9a-f]+)");

        [MenuItem("Tools/ClpsPLUG/I18n/I18n String Tools")]
        private static void Open(MenuCommand menuCommand)
        {
            var window = GetWindow<I18nToolsWindow>();
            window.titleContent = new GUIContent("I18n String Tools");
        }

        private void OnEnable()
        {
            FromSavedState(JsonUtility.FromJson<ToolValueState>(
                EditorPrefs.GetString(
                    EditorPrefKey,
                    JsonUtility.ToJson(new ToolValueState())
                )
            ));
        }

        private ToolValueState ToSavedState()
        {
            return new ToolValueState
            {
                stringPath = _stringPath,
                namespaceForClass = _namespace,
                location = _outputLocation,
                includeNumericInChar = _includeNumericAndSymbolsInChar,
            };
        }

        private void FromSavedState(ToolValueState data)
        {
            _stringPath = data.stringPath;
            _namespace = data.namespaceForClass;
            _outputLocation = data.location;
            _includeNumericAndSymbolsInChar = data.includeNumericInChar;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                richText = true,
            };
            var config = Resources.Load<I18nStringConfig>("I18n/I18nStringConfig") ??
                         CreateInstance<I18nStringConfig>();
            GUILayout.Label("<size=20><b>Internationalization Tools</b></size>", style);
            GUILayout.Label($"Your string source path is set to: Assets/Resources/{config.StringSourcePath}");
            _stringPath = config.StringSourcePath;
            EditorGUIUtility.labelWidth = 300f;
            if (!_isStringPathValid)
            {
                EditorGUILayout.HelpBox(
                    "String asset not found at the specified location!\n" +
                    "Double check the file name - especially if you have created I18nStringConfig.\n" +
                    "Check that the name of the config file is I18nStringConfig and " +
                    "it exists under Assets/Resources/I18n as well.",
                    MessageType.Error
                );
            }

            EditorGUILayout.Space();

            GUILayout.Label("<size=20>Create 'I18nKeys' Class</size>", style);
            GUILayout.Label("Generates a class that defines keys of i18n string.");
            _namespace = EditorGUILayout.TextField(
                new GUIContent("Class Namespace", "If you need the class namespaced, enter it here."),
                _namespace
            );
            _outputLocation = EditorGUILayout.TextField(
                new GUIContent(
                    "Output file path, Assets/",
                    "Enter the path to output the class. Path will be prepended with Assets/."
                ),
                _outputLocation
            );
            if (!_outputDirExists)
            {
                EditorGUILayout.HelpBox(
                    "The output directory doesn't exist and will be created.",
                    MessageType.Info
                );
            }

            if (_outputFileExists)
            {
                EditorGUILayout.HelpBox(
                    "The output path exists. Will overwrite the existing code. Check if this is intended.",
                    MessageType.Info
                );
                if (_isStringPathValid)
                {
                    try
                    {
                        // Read file and compare hash
                        var hash = new I18nStringParser(_stringPath).GetHash();
                        using var sr = new StreamReader(Path.Join(Application.dataPath, _outputLocation));
                        var text = sr.ReadToEnd();
                        var match = regex.Match(text);
                        if (!match.Success || match.Groups[1].Value != hash)
                        {
                            EditorGUILayout.HelpBox(
                                "There appears to be a modification to the string resource. " +
                                "Update your key file so that you can refer to keys from the code.",
                                MessageType.Warning
                            );
                        }
                    }
                    catch (StringNotFoundException)
                    {
                        EditorGUILayout.HelpBox(
                            "There was an error trying to read the string resource: does it exist?",
                            MessageType.Error
                        );
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "The output path does NOT exist. This will create a new file. Check if this is intended.",
                    MessageType.Warning
                );
            }

            if (!_isOutputPathFile)
            {
                EditorGUILayout.HelpBox(
                    "The output path is a existing directory! Please pick another path.",
                    MessageType.Error
                );
            }

            if (!_isOutputPathCs)
            {
                EditorGUILayout.HelpBox(
                    "The output will be a C# file. \n" +
                    "It is strongly recommended that you append '.cs' to the path.",
                    MessageType.Warning
                );
            }

            EditorGUI.BeginDisabledGroup(_isGeneratingClass || _isGeneratingChars);
            if (GUILayout.Button("Generate Internationalization string class"))
            {
                EditorPrefs.SetString(EditorPrefKey, JsonUtility.ToJson(ToSavedState()));
                _isGeneratingClass = true;
                var generator = new I18nGenerator(_stringPath, _namespace, _outputLocation, IndentIncrement);
                try
                {
                    generator.OnGenerate();
                    _isGeneratingClass = false;
                    Debug.Log(
                        "Generation complete! You might need to un-focus and focus this Unity window for Unity to compile it."
                    );
                }
                catch (Exception e)
                {
                    _isGeneratingClass = false;
                    Debug.LogException(e);
                }
            }

            EditorGUI.EndDisabledGroup();

            if (_isGeneratingClass)
            {
                GUILayout.Label("Generating class...");
            }

            EditorGUILayout.Space();

            GUILayout.Label("<size=20>Get a set of chars used in strings</size>", style);
            GUILayout.Label("Gets a set of characters used in the strings file.");
            GUILayout.Label("Useful for TextMeshPro atlas generation.");
            EditorGUI.BeginDisabledGroup(_isGeneratingChars || _isGeneratingClass);

            _includeNumericAndSymbolsInChar = EditorGUILayout.Toggle(
                new GUIContent(
                    "Include numeric (and related) chars",
                    "If ticked, character used will come with the numeric-related characters ([0-9.,+-] and more)."
                ), _includeNumericAndSymbolsInChar
            );

            _includeAlphaInChar = EditorGUILayout.Toggle(
                new GUIContent(
                    "Include all alphabets",
                    "If ticked, all alphabets (lower and uppercase) will be included."),
                _includeAlphaInChar
            );

            if (GUILayout.Button("Get characters used in the strings asset"))
            {
                _isGeneratingChars = true;
                _usedChars = "";
                var sl = SupportedLanguageLoader.GetInstance().SupportedLanguage;
                EditorPrefs.SetString(EditorPrefKey, JsonUtility.ToJson(ToSavedState()));
                var generator = new I18nGenerator(_stringPath, _namespace, _outputLocation, IndentIncrement);
                try
                {
                    var result = generator.OnGetChars();
                    _isGeneratingChars = false;
                    var builder = new StringBuilder();
                    var r = result.Where(c => c != '\n').ToHashSet();
                    if (_includeNumericAndSymbolsInChar)
                    {
                        var numericAndSymbols = new List<char>();
                        numericAndSymbols.AddRange(Enumerable.Range(32, 64 - 32 + 1).Select(i => (char)i));
                        numericAndSymbols.AddRange(Enumerable.Range(91, 96 - 91 + 1).Select(i => (char)i));
                        numericAndSymbols.AddRange(Enumerable.Range(123, 126 - 123 + 1).Select(i => (char)i));
                        numericAndSymbols.Add((char)160);
                        var str = string.Concat(numericAndSymbols);
                        builder.Append(str);
                        // Prevent duplicate
                        r = r.Where(c => !str.Contains(c)).ToHashSet();
                    }

                    if (_includeAlphaInChar)
                    {
                        var alphas = new List<char>();
                        alphas.AddRange(Enumerable.Range('A', 26).Select(i => (char)i));
                        alphas.AddRange(Enumerable.Range('a', 26).Select(i => (char)i));
                        var str = string.Concat(alphas);
                        builder.Append(str);
                        r = r.Where(c => !str.Contains(c)).ToHashSet();
                    }

                    var langStr = string.Empty;
                    for (var i = 0; i < sl.Count(); i++)
                    {
                        // Required for displaying supported languages
                        langStr = string.Concat(langStr, sl.GetDisplayFromId(i));
                    }

                    langStr = string.Concat(langStr.Where(c => !builder.ToString().Contains(c)));
                    builder.Append(langStr);
                    r = r.Where(c => !langStr.Contains(c)).ToHashSet();

                    builder.Append(string.Concat(r));
                    builder.Append("()_"); // TextMeshPro requires these three
                    _usedChars = builder.ToString();
                }
                catch (Exception e)
                {
                    _isGeneratingChars = false;
                    Debug.LogException(e);
                }
            }

            EditorGUI.EndDisabledGroup();

            if (_isGeneratingChars)
            {
                GUILayout.Label(
                    "Extracting characters..."
                );
            }

            if (!string.IsNullOrEmpty(_usedChars))
            {
                GUILayout.Label(
                    "Below is the result of the generation which is selectable.\n" +
                    "Click it, select all (Ctrl-A or Cmd-A) and copy the text."
                );
                EditorGUILayout.SelectableLabel(_usedChars);
                EditorGUILayout.HelpBox(
                    "The following characters are implicitly added for TextMeshPro compatibility: ()_",
                    MessageType.Info
                );
            }

            CheckPath();
            CheckOutput(Path.Join(Application.dataPath, _outputLocation));
        }

        private void CheckPath()
        {
            if (File.Exists(Path.Join(Application.dataPath, "Resources", _stringPath)))
            {
                _isStringPathValid = true;
                return;
            }

            if (File.Exists(Path.Join(Path.Join(Application.dataPath, "Resources"), _stringPath + ".json")))
            {
                _isStringPathValid = true;
                return;
            }

            _isStringPathValid = false;
        }

        private void CheckOutput(string path)
        {
            _outputDirExists = Directory.Exists(Directory.GetParent(path)?.FullName);
            try
            {
                var attr = File.GetAttributes(path);
                _isOutputPathFile = (attr & FileAttributes.Directory) == 0;
                _outputFileExists = true;
            }
            catch (FileNotFoundException)
            {
                _isOutputPathFile = true;
                _outputFileExists = false;
            }
            catch (DirectoryNotFoundException)
            {
                _isOutputPathFile = true;
                _outputFileExists = false;
            }

            _isOutputPathCs = path.EndsWith(".cs");
        }
    }

    [Serializable]
    public class ToolValueState
    {
        public string stringPath;
        public string namespaceForClass;
        public string location;
        public bool includeNumericInChar;
    }

    public static class EnumerableExtension
    {
        public static void ForEach<T>(this IEnumerable<T> list, Action<T> action)
        {
            foreach (var item in list)
            {
                action(item);
            }
        }
    }
}