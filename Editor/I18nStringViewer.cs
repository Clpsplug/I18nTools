using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Clpsplug.I18n.Editor.Component;
using Clpsplug.I18n.Runtime;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Clpsplug.I18n.Editor
{
    public class I18nStringViewer : EditorWindow
    {
        private string _stringPath;
        private bool _isStringPathValid;

        private List<LocalizedStringData> _data = new List<LocalizedStringData>();

        [SerializeField] private TreeViewState state;
        private StringTreeView _treeView;

        private const string EditorPrefKey = "com.expoding-cable.i18n-viewer";


        [MenuItem("Tools/ClpsPLUG/I18n/I18n String Viewer")]
        private static void Open(MenuCommand command)
        {
            var window = GetWindow<I18nStringViewer>();
            window.titleContent = new GUIContent("I18n String Viewer");
        }

        private void OnEnable()
        {
            FromSavedState(JsonUtility.FromJson<ViewerValueState>(
                EditorPrefs.GetString(
                    EditorPrefKey,
                    JsonUtility.ToJson(new ToolValueState())
                )
            ));
            _treeView = new StringTreeView(state ?? new TreeViewState());
        }

        private ViewerValueState ToSavedState()
        {
            return new ViewerValueState
            {
                stringPath = _stringPath,
            };
        }

        private void FromSavedState(ViewerValueState data)
        {
            _stringPath = data.stringPath;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                richText = true,
            };
            GUILayout.Label("<size=20><b>Internationalization String Viewer</b></size>", style);
            GUILayout.Label("Make sure you give a correct path to the string definition first.");
            EditorGUIUtility.labelWidth = 300f;
            _stringPath = EditorGUILayout.TextField(
                new GUIContent(
                    "Path to i18n string source, Assets/Resources/",
                    "Enter the path that comes after the Resources folder. Does NOT start with /."),
                _stringPath
            );
            CheckPath();
            if (!_isStringPathValid)
            {
                var fullPath = $"{_stringPath}" + (_stringPath.EndsWith(".json") ? "" : ".json");
                EditorGUILayout.HelpBox(
                    $"Such a string asset (Assets/Resources/{fullPath}) is not found!\n" +
                    "Double check the path - especially if you haven't accidentally prepended Assets/Resources/."
                    ,
                    MessageType.Error
                );
            }
            else
            {
                EditorPrefs.SetString(EditorPrefKey, JsonUtility.ToJson(ToSavedState()));
                OnLoadAsset();
                _treeView?.LoadData(_data);
                _treeView?.Reload();
            }

            EditorGUI.BeginDisabledGroup(!_isStringPathValid);
            if (GUILayout.Button("Reload resource"))
            {
                OnLoadAsset();
                _treeView?.LoadData(_data);
                _treeView?.Reload();
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            if (_treeView != null)
            {
                var treeViewRect = EditorGUILayout.GetControlRect(
                    false,
                    GUILayout.ExpandWidth(true)
                );
                var heightRatio = _isStringPathValid ? 5 : 8;
                treeViewRect.y =
                    EditorGUI.GetPropertyHeight(SerializedPropertyType.String, new GUIContent())
                    * heightRatio;
                treeViewRect.height =
                    position.height
                    - EditorGUI.GetPropertyHeight(SerializedPropertyType.String, new GUIContent())
                    * heightRatio
                    - EditorGUI.GetPropertyHeight(SerializedPropertyType.String, new GUIContent());
                _treeView.OnGUI(treeViewRect);
            }

            EditorGUI.BeginDisabledGroup(_treeView?.GetSelection().Count != 1);
            if (GUILayout.Button("Copy full key of selected element"))
            {
                var treeItemId = _treeView!.GetSelection().First();
                GUIUtility.systemCopyBuffer = _treeView.GetFullKey(treeItemId);
                Debug.Log($"Copied: {GUIUtility.systemCopyBuffer}");
            }

            EditorGUI.EndDisabledGroup();
        }


        private void OnLoadAsset()
        {
            var textAsset = Resources.Load<TextAsset>(_stringPath);
            if (textAsset == null)
            {
                throw new StringNotFoundException();
            }


            try
            {
                _data = JsonConvert.DeserializeObject<List<LocalizedStringData>>(textAsset.text);
            }
            catch (ArgumentNullException ane)
            {
                _data = new List<LocalizedStringData>();
                throw new OperationCanceledException("Cancelled because i18n resources are broken.", ane);
            }
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

        [Serializable]
        public class ViewerValueState
        {
            public string stringPath;
        }
    }
}