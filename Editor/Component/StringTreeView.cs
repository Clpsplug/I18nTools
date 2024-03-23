using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Clpsplug.I18n.Runtime;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Clpsplug.I18n.Editor.Component
{
    internal class StringTreeView : TreeView
    {
        private List<LocalizedStringData> _data = new List<LocalizedStringData>();
        private static ISupportedLanguage _sl;

        public StringTreeView(TreeViewState state) : base(state, CreateHeader())
        {
            Reload();
        }

        private static MultiColumnHeader CreateHeader()
        {
            var baseColumn = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Key"),
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Full Key"),
                },
            };
            var columns = new List<MultiColumnHeaderState.Column>(baseColumn);
            _sl ??= SupportedLanguageLoader.GetInstance().SupportedLanguage;
            columns.AddRange(
                _sl.GetCodeDisplayPairs()
                    .Select(l => new MultiColumnHeaderState.Column
                        {
                            headerContent = new GUIContent(l.Value),
                        }
                    )
            );
            var state = new MultiColumnHeaderState(columns.ToArray());
            var header = new MultiColumnHeader(state);
            header.ResizeToFit();
            return header;
        }

        public void LoadData(List<LocalizedStringData> data)
        {
            _data = data;
            _sl = SupportedLanguageLoader.GetInstance().SupportedLanguage;
        }

        public string GetFullKey(int itemId)
        {
            var item = GetRows().FirstOrDefault(i => i.id == itemId);
            return item switch
            {
                StringTreeViewItem s => s.fullKey,
                _ => "",
            };
        }

        protected override TreeViewItem BuildRoot()
        {
            var id = 0;
            var root = new TreeViewItem { id = ++id, depth = -1, displayName = "Root" };
            var items = new List<TreeViewItem>();
            var top = new TreeViewItem { id = ++id, depth = 0, displayName = "I18n string resources" };
            items.Add(top);
            items.AddRange(RecursiveAddView(root, _data, "", 0, ref id));
            SetupParentsAndChildrenFromDepths(root, items);
            return root;
        }

        private static IEnumerable<TreeViewItem> RecursiveAddView(TreeViewItem parent, List<LocalizedStringData> data,
            string parentSoFar, int depth,
            ref int id)
        {
            var items = new List<TreeViewItem>();
            foreach (var ls in data)
            {
                // Still at node
                // Check if there is any stuff to add for node string
                depth++;
                var item = new StringTreeViewItem
                {
                    id = ++id, depth = depth, displayName = ls.Key, fullKey = $"{parentSoFar}{ls.Key}",
                    localizedStrings = ls.LocalizationStrings,
                };
                parent.AddChild(item);
                items.Add(item);
                if (ls.Children != null)
                {
                    items.AddRange(RecursiveAddView(parent, ls.Children, $"{parentSoFar}{ls.Key}.", depth, ref id));
                }

                depth--;
            }

            return items;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            switch (args.item)
            {
                case StringTreeViewItem item:
                {
                    // The string item
                    for (var i = 0; i < args.GetNumVisibleColumns(); i++)
                    {
                        var rect = args.GetCellRect(i);
                        var columnIndex = args.GetColumn(i);

                        // Intentionally using columnIndex here,
                        // because columns can be hidden.
                        switch (columnIndex)
                        {
                            case 0:
                                // The first element must be indented
                                rect.xMin += GetContentIndent(item);
                                EditorGUI.LabelField(rect, item.displayName);
                                break;
                            case 1:
                                EditorGUI.SelectableLabel(rect, item.fullKey);
                                break;
                            default:
                                var l = columnIndex - 2;
                                EditorGUI.LabelField(rect,
                                    item.localizedStrings.GetValueOrDefault(_sl.GetCodeFromId(l), "")
                                        .Replace("\n", " "));
                                break;
                        }
                    }

                    break;
                }
                default:
                    base.RowGUI(args);
                    break;
            }
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class StringTreeViewItem : TreeViewItem
    {
        public string fullKey;
        public Dictionary<string, string> localizedStrings = new Dictionary<string, string>();
    }


    internal static class EnumTool
    {
        public static ReadOnlySpan<T> Enumerate<T>() where T : Enum
        {
            return ((T[])Enum
                    .GetValues(typeof(T)))
                .AsSpan();
        }
    }
}