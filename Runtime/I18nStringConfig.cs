using UnityEngine;

namespace Clpsplug.I18n.Runtime
{
    [CreateAssetMenu(menuName = "ClpsPLUG/I18n/I18n string configuration", fileName = "I18nStringConfig")]
    public class I18nStringConfig : ScriptableObject
    {
        [SerializeField,
         Tooltip(
             "Path to i18n string source FOLLOWING 'Assets/Resources/'. " +
             "The '.json' extension is not required. " +
             "You usually do not have to change this value, " +
             "but if your game needs that file name, you may change this value to something else."
         )]
        private string stringSourcePath = "strings";

        [SerializeField,
         Tooltip(
             "Path to the unicode token substitution definition file." +
             "If left blank, no substitution will be used."
         )]
        private string unicodeMappingDefinitionPath = "";

        // ReSharper disable InconsistentNaming
        public string StringSourcePath => stringSourcePath;

        public string UnicodeMappingDefinitionPath => unicodeMappingDefinitionPath;
        // ReSharper restore InconsistentNaming
    }
}