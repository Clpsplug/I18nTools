using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Clpsplug.I18n.Editor.Generator;
using Clpsplug.I18n.Runtime;
using UnityEditor;
using UnityEngine;

namespace Clpsplug.I18n.Editor
{
    public class I18nToolsWindow : EditorWindow
    {
        private string _stringPath;
        private string _namespace;
        private string _outputLocation;
        private bool _includeNumericInChar;
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
                includeNumericInChar = _includeNumericInChar,
            };
        }

        private void FromSavedState(ToolValueState data)
        {
            _stringPath = data.stringPath;
            _namespace = data.namespaceForClass;
            _outputLocation = data.location;
            _includeNumericInChar = data.includeNumericInChar;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                richText = true,
            };
            GUILayout.Label("<size=20><b>Internationalization Tools</b></size>", style);
            GUILayout.Label("Make sure you give a correct path to the string definition first.");
            EditorGUIUtility.labelWidth = 300f;
            _stringPath = EditorGUILayout.TextField(
                new GUIContent(
                    "Path to i18n string source, Assets/Resources/",
                    "Enter the path that comes after the Resources folder. Does NOT start with /."),
                _stringPath
            );
            if (!_isStringPathValid)
            {
                EditorGUILayout.HelpBox(
                    "Such a string asset is not found!\n" +
                    "Double check the path - especially if you haven't accidentally prepended Assets/Resources/."
                    ,
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

            _includeNumericInChar = EditorGUILayout.Toggle(
                new GUIContent(
                    "Include numeric (and related) chars",
                    "If ticked, character used will come with the numeric-related characters ([0-9.,+-])."
                ), _includeNumericInChar
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
                    if (_includeNumericInChar)
                    {
                        const string numeric = "0123456789+-.,";
                        builder.Append(numeric);
                        // Prevent duplicate
                        r = r.Where(c => !numeric.Contains(c)).ToHashSet();
                    }

                    for (var i = 0; i < sl.Count(); i++)
                    {
                        // Required for displaying supported languages
                        builder.Append(sl.GetDisplayFromId(i));
                    }

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