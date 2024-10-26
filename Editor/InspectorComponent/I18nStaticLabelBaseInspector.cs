using System;
using System.Collections.Generic;
using System.Text;
using Clpsplug.I18n.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Clpsplug.I18n.Editor.InspectorComponent
{
    [CustomEditor(typeof(I18nStaticLabelBase), true)]
    [CanEditMultipleObjects]
    public class I18nStaticLabelBaseInspector : UnityEditor.Editor
    {
        private SerializedProperty key;

        private I18nStringRepository _stringRepository;
        private ISupportedLanguage _language;

        private bool _isRepositoryAvailable;

        private void OnEnable()
        {
            try
            {
                _language = SupportedLanguageLoader.GetInstance().SupportedLanguage;
                _stringRepository = I18nStringRepository.GetInstance();
                _isRepositoryAvailable = true;
            }
            catch (ArgumentNullException)
            {
                _isRepositoryAvailable = false;
            }

            key = serializedObject.FindProperty("key");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(key);
            serializedObject.ApplyModifiedProperties();
            if (!_isRepositoryAvailable)
            {
                EditorGUILayout.HelpBox(
                    "[ERROR] I18n string JSON file couldn't be parsed; key hints are disabled!",
                    MessageType.Error,
                    true
                );
                return;
            }

            if (key.stringValue == string.Empty)
            {
                EditorGUILayout.HelpBox(
                    "[WARN] No key specified. This will result in an error message displayed.",
                    MessageType.Warning,
                    true
                );
                return;
            }

            try
            {
                var str = _stringRepository.GetLocalizedStringFromKey(key.stringValue);
                var strBuilder = new StringBuilder();
                strBuilder.AppendLine("This key will appear as:");
                foreach (var code in _language.GetLanguageCodes().Values)
                {
                    strBuilder.AppendLine($"{code}: {str.LocalizationStrings[code]}");
                }

                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                if (GUILayout.Button("Copy text for primary language to TMP component"))
                {
                    TMP_Text tmpCmp = ((I18nStaticLabelBase)target).GetComponent<TextMeshPro>();
                    if (tmpCmp == null)
                    {
                        tmpCmp = ((I18nStaticLabelBase)target).GetComponent<TextMeshProUGUI>();
                    }

                    tmpCmp.SetText(str.LocalizationStrings[_language.GetLanguageCodes()[0]]);
                }

                EditorGUI.EndDisabledGroup();

                EditorGUILayout.HelpBox(strBuilder.ToString(), MessageType.Info, true);
            }
            catch (KeyNotFoundException)
            {
                EditorGUILayout.HelpBox(
                    $"[WARN] {key.stringValue} does not have any string associated with it.\n" +
                    "Could it be a parent I18n string key?",
                    MessageType.Warning, true
                );
            }
            catch (InvalidOperationException)
            {
                EditorGUILayout.HelpBox(
                    $"[WARN] {key.stringValue} is not a valid I18n string key. Check spelling.",
                    MessageType.Warning, true);
            }
        }
    }
}