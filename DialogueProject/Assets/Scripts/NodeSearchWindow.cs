using Subtegral.DialogueSystem.DataContainers;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Subtegral.DialogueSystem.Editor
{
    public class NodeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private EditorWindow _window;
        private StoryGraphView _graphView;
        private Texture2D _indentationIcon;

        public void Configure(EditorWindow window, StoryGraphView graphView)
        {
            _window = window;
            _graphView = graphView;

            _indentationIcon = new Texture2D(1, 1);
            _indentationIcon.SetPixel(0, 0, new Color(0, 0, 0, 0));
            _indentationIcon.Apply();
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            return new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create"), 0),

                new SearchTreeGroupEntry(new GUIContent("Nodes"), 1),
                new SearchTreeEntry(new GUIContent("Dialogue Node", _indentationIcon)) { level = 2, userData = DialogueNodeType.Dialogue },
                new SearchTreeEntry(new GUIContent("Branch Node", _indentationIcon)) { level = 2, userData = DialogueNodeType.Branch },
                new SearchTreeEntry(new GUIContent("End Node", _indentationIcon)) { level = 2, userData = DialogueNodeType.End },

                new SearchTreeGroupEntry(new GUIContent("Other"), 1),
                new SearchTreeEntry(new GUIContent("Comment Block", _indentationIcon)) { level = 2, userData = "Comment" },
            };
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var mousePosition = _window.rootVisualElement.ChangeCoordinatesTo(
                _window.rootVisualElement.parent,
                context.screenMousePosition - _window.position.position
            );

            var graphMousePosition = _graphView.contentViewContainer.WorldToLocal(mousePosition);

            if (entry.userData is DialogueNodeType type)
            {
                _graphView.CreateNode(type.ToString(), graphMousePosition, type);
                return true;
            }

            if (entry.userData is string s && s == "Comment")
            {
                _graphView.CreateCommentBlock(new Rect(graphMousePosition, _graphView.DefaultCommentBlockSize));
                return true;
            }

            return false;
        }
    }
}