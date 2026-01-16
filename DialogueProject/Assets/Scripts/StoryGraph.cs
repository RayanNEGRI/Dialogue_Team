using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Subtegral.DialogueSystem.Editor
{
    public class StoryGraph : EditorWindow
    {
        private string _fileName = "New Narrative";
        private StoryGraphView _graphView;

        [MenuItem("Graph/Narrative Graph")]
        public static void CreateGraphViewWindow()
        {
            var window = GetWindow<StoryGraph>();
            window.titleContent = new GUIContent("Narrative Graph");
        }

        private void OnEnable()
        {
            ConstructGraphView();
            GenerateToolbar();
            GenerateMiniMap();
            GenerateBlackBoard();
        }

        private void OnDisable()
        {
            if (_graphView != null)
                rootVisualElement.Remove(_graphView);
        }

        private void ConstructGraphView()
        {
            _graphView = new StoryGraphView(this) { name = "Narrative Graph" };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }

        private void GenerateToolbar()
        {
            var toolbar = new Toolbar();

            var fileNameTextField = new TextField("File Name:");
            fileNameTextField.SetValueWithoutNotify(_fileName);
            fileNameTextField.RegisterValueChangedCallback(evt => _fileName = evt.newValue?.Trim());
            toolbar.Add(fileNameTextField);

            toolbar.Add(new Button(() => RequestDataOperation(true)) { text = "Save Data" });
            toolbar.Add(new Button(() => RequestDataOperation(false)) { text = "Load Data" });

            rootVisualElement.Add(toolbar);
        }

        private void RequestDataOperation(bool save)
        {
            if (string.IsNullOrWhiteSpace(_fileName))
            {
                EditorUtility.DisplayDialog("Invalid File name", "Please enter a valid filename.", "OK");
                return;
            }

            var saveUtility = GraphSaveUtility.GetInstance(_graphView);
            if (save) saveUtility.SaveGraph(_fileName);
            else saveUtility.LoadNarrative(_fileName);
        }

        private void GenerateMiniMap()
        {
            var miniMap = new MiniMap { anchored = true };
            var cords = _graphView.contentViewContainer.WorldToLocal(new Vector2(maxSize.x - 10, 30));
            miniMap.SetPosition(new Rect(cords.x, cords.y, 220, 160));
            _graphView.Add(miniMap);
        }

        private void GenerateBlackBoard()
        {
            var blackboard = new Blackboard(_graphView);
            blackboard.Add(new BlackboardSection { title = "Exposed Variables" });

            blackboard.addItemRequested = _ =>
            {
                _graphView.AddPropertyToBlackBoard(DataContainers.ExposedProperty.CreateInstance(), false);
            };

            blackboard.editTextRequested = (_, element, newValue) =>
            {
                newValue = (newValue ?? "").Trim();
                if (string.IsNullOrEmpty(newValue))
                {
                    EditorUtility.DisplayDialog("Error", "Property name cannot be empty.", "OK");
                    return;
                }

                var oldPropertyName = ((BlackboardField)element).text;
                if (_graphView.ExposedProperties.Any(x => x.PropertyName == newValue))
                {
                    EditorUtility.DisplayDialog("Error", "This property name already exists, please choose another one.", "OK");
                    return;
                }

                var targetIndex = _graphView.ExposedProperties.FindIndex(x => x.PropertyName == oldPropertyName);
                if (targetIndex < 0) return;

                _graphView.ExposedProperties[targetIndex].PropertyName = newValue;
                ((BlackboardField)element).text = newValue;
            };

            blackboard.SetPosition(new Rect(10, 30, 260, 340));
            _graphView.Add(blackboard);
            _graphView.Blackboard = blackboard;
        }
    }
}