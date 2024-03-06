using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Clpsplug.I18n.Runtime
{
    public static class I18nTool
    {
        /// <summary>
        /// Notifies a language switch within the game to all the static labels currently in scene.
        /// This is an expensive invocation, so watch how you call this method.
        /// </summary>
        public static void NotifyLanguageSwitchToStaticLabels()
        {
            var staticLabels =
                Object.FindObjectsByType<I18nStaticLabelBase>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (var label in staticLabels)
            {
                try
                {
                    label.ReloadText();
                }
                catch (NullReferenceException)
                {
                    Debug.LogError(
                        $"This label ({label.gameObject.name}) hasn't recognized its text yet - has this label ever become active as an GameObject yet? If you're seeing this error, that's probably the case.",
                        label
                    );
                }
            }
        }
    }
}