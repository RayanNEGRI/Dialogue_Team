using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Subtegral.DialogueSystem.DataContainers;

namespace Subtegral.DialogueSystem.Editor
{
    public class StoryGraphView : GraphView
    {
        public readonly Vector2 DefaultNodeSize = new Vector2(380, 240);
        public readonly Vector2 DefaultCommentBlockSize = new Vector2(300, 200);

        public DialogueNode EntryPointNode;
        public Blackboard Blackboard = new Blackboard();
        public List<ExposedProperty> ExposedProperties { get; private set; } = new List<ExposedProperty>();

        private NodeSearchWindow _searchWindow;

        public const string ChoiceLabelFieldName = "ChoiceLabelField";
        public const string ChoiceCondFieldName = "ChoiceCondField";

        private const string ChoicesHeaderName = "ChoicesHeader";

        public StoryGraphView(StoryGraph editorWindow)
        {
            var graphStyle = Resources.Load<StyleSheet>("NarrativeGraph");
            if (graphStyle != null) styleSheets.Add(graphStyle);

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            AddElement(GetEntryPointNodeInstance());
            AddSearchWindow(editorWindow);
        }

        private void AddSearchWindow(EditorWindow editorWindow)
        {
            _searchWindow = ScriptableObject.CreateInstance<NodeSearchWindow>();
            _searchWindow.Configure(editorWindow, this);

            nodeCreationRequest = context =>
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (startPort == port) return;
                if (startPort.node == port.node) return;
                if (startPort.direction == port.direction) return;
                compatible.Add(port);
            });
            return compatible;
        }

        public Group CreateCommentBlock(Rect rect, CommentBlockData data = null)
        {
            if (data == null)
                data = new CommentBlockData();

            var group = new Group
            {
                autoUpdateGeometry = true,
                title = data.Title
            };

            AddElement(group);
            group.SetPosition(rect);
            return group;
        }

        public void ClearBlackBoardAndExposedProperties()
        {
            ExposedProperties.Clear();
            Blackboard.Clear();
        }

        public void AddPropertyToBlackBoard(ExposedProperty property, bool loadMode = false)
        {
            var localName = property.PropertyName;
            var localValue = property.PropertyValue;

            if (!loadMode)
            {
                while (ExposedProperties.Any(x => x.PropertyName == localName))
                    localName = $"{localName}(1)";
            }

            var item = ExposedProperty.CreateInstance();
            item.PropertyName = localName;
            item.PropertyValue = localValue;
            ExposedProperties.Add(item);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;

            var field = new BlackboardField { text = localName, typeText = "string" };
            container.Add(field);

            var valueField = new TextField("Value:") { value = localValue };
            valueField.RegisterValueChangedCallback(evt =>
            {
                var idx = ExposedProperties.FindIndex(x => x.PropertyName == item.PropertyName);
                if (idx >= 0) ExposedProperties[idx].PropertyValue = evt.newValue ?? "";
            });

            container.Add(new BlackboardRow(field, valueField));
            Blackboard.Add(container);
        }

        public DialogueNode CreateNode(string title, Vector2 position, DialogueNodeType type)
        {
            if (type == DialogueNodeType.End)
                type = DialogueNodeType.Dialogue;

            var node = new DialogueNode
            {
                GUID = Guid.NewGuid().ToString(),
                NodeType = type,
                title = type == DialogueNodeType.Branch ? "BRANCH" : "DIALOGUE",
                DebugLabel = "",
                DialogueText = "",
                ConditionExpression = ""
            };

            var nodeStyle = Resources.Load<StyleSheet>("Node");
            if (nodeStyle != null) node.styleSheets.Add(nodeStyle);

            var inputPort = GetPortInstance(node, Direction.Input, Port.Capacity.Multi);
            inputPort.portName = "Input";
            node.inputContainer.Add(inputPort);

            var debugField = new TextField("Debug Label");
            debugField.SetValueWithoutNotify(node.DebugLabel);
            debugField.RegisterValueChangedCallback(e => node.DebugLabel = e.newValue ?? "");
            node.mainContainer.Add(debugField);

            if (type == DialogueNodeType.Dialogue)
                BuildDialogueNodeUI(node);
            else
                BuildBranchNodeUI(node);

            node.SetPosition(new Rect(position, DefaultNodeSize));
            node.RefreshExpandedState();
            node.RefreshPorts();

            AddElement(node);
            return node;
        }

        private void BuildDialogueNodeUI(DialogueNode node)
        {
            var guids = AssetDatabase.FindAssets("t:BDD_Dialogue");
            UnityEngine.Object bddObj = null;
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                bddObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            }

            var dialogueKeys = AssetKeyProvider.TryGetKeysFromSerializedArrayStringField(bddObj, "Entries", "key");

            if (dialogueKeys.Count > 0)
            {
                var options = dialogueKeys;
                var defaultValue = options[0];

                if (options.Contains(node.DialogueText))
                    defaultValue = node.DialogueText;
                else if (!string.IsNullOrEmpty(node.DialogueText))
                {
                    options.Insert(0, node.DialogueText);
                    defaultValue = node.DialogueText;
                }

                var popup = new PopupField<string>("Clé Dialogue", options, defaultValue);
                popup.RegisterValueChangedCallback(evt => node.DialogueText = evt.newValue ?? "");
                node.DialogueText = defaultValue;
                node.mainContainer.Add(popup);
            }
            else
            {
                var dialogueField = new TextField("Dialogue Text") { multiline = true };
                dialogueField.SetValueWithoutNotify(node.DialogueText);
                dialogueField.RegisterValueChangedCallback(e => node.DialogueText = e.newValue ?? "");
                node.mainContainer.Add(dialogueField);
            }

            var addChoice = new Button(() =>
            {
                var portId = Guid.NewGuid().ToString();
                node.Ports[portId] = new DialogueNode.ChoicePortData(portId, "", "");
                AddChoicePort(node, portId, deletable: true);
                EnsureChoicesHeaderVisibility(node);
            })
            { text = "Add Choice" };

            node.titleButtonContainer.Add(addChoice);

            EnsureChoicesHeaderVisibility(node);
        }

        private void BuildBranchNodeUI(DialogueNode node)
        {
            var condField = new TextField("Branch Condition");
            condField.SetValueWithoutNotify(node.ConditionExpression ?? "");
            condField.RegisterValueChangedCallback(e => node.ConditionExpression = e.newValue ?? "");
            node.mainContainer.Add(condField);

            EnsureFixedBranchPort(node, "true", "True");
            EnsureFixedBranchPort(node, "false", "False");
        }

        private void EnsureFixedBranchPort(DialogueNode node, string portId, string label)
        {
            if (!node.Ports.ContainsKey(portId))
                node.Ports[portId] = new DialogueNode.ChoicePortData(portId, label, "");

            node.Ports[portId].Label = label;
            AddChoicePort(node, portId, deletable: false);
        }

        private void EnsureChoicesHeaderVisibility(DialogueNode node)
        {
            if (node == null) return;
            if (node.NodeType != DialogueNodeType.Dialogue) return;

            var hasAnyChoices = node.outputContainer.Children().OfType<Port>()
                .Any(p => p != null && p.direction == Direction.Output && p.userData is string s && !string.IsNullOrEmpty(s));

            var header = node.outputContainer.Q<VisualElement>(ChoicesHeaderName);

            if (!hasAnyChoices)
            {
                if (header != null)
                {
                    node.outputContainer.Remove(header);
                    node.RefreshPorts();
                    node.RefreshExpandedState();
                }
                return;
            }

            if (header != null) return;

            header = new VisualElement { name = ChoicesHeaderName };
            header.pickingMode = PickingMode.Ignore;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.FlexStart;
            header.style.marginTop = 6;
            header.style.marginBottom = 4;
            header.style.paddingLeft = 6;
            header.style.paddingRight = 6;

            var l = new Label("Label");
            l.style.minWidth = 160;
            l.style.flexGrow = 1;
            l.style.opacity = 0.85f;

            var c = new Label("Condition");
            c.style.minWidth = 160;
            c.style.flexGrow = 1;
            c.style.opacity = 0.85f;

            var sps = new VisualElement();
            sps.style.width = 28;

            header.Add(l);
            header.Add(c);
            header.Add(sps);

            node.outputContainer.Insert(0, header);
            node.RefreshPorts();
            node.RefreshExpandedState();
        }

        public void AddChoicePort(DialogueNode node, string portId, bool deletable)
        {
            if (!node.Ports.ContainsKey(portId))
                node.Ports[portId] = new DialogueNode.ChoicePortData(portId, "", "");

            var port = GetPortInstance(node, Direction.Output, Port.Capacity.Single);
            port.userData = portId;
            port.portName = deletable ? "" : (node.Ports[portId].Label ?? "");

            var typeLabel = port.contentContainer.Q<Label>("type");
            if (typeLabel != null) port.contentContainer.Remove(typeLabel);

            var nameLabel = port.contentContainer.Q<Label>("portName");
            if (nameLabel != null) nameLabel.style.display = DisplayStyle.None;

            port.style.width = Length.Percent(100);
            port.style.marginBottom = 6;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.flexGrow = 1;
            row.style.width = Length.Percent(100);

            if (deletable)
            {
                VisualElement labelFieldElement;

                var guidsUI = AssetDatabase.FindAssets("t:BDD_UI");
                UnityEngine.Object bddUIObj = null;
                if (guidsUI != null && guidsUI.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guidsUI[0]);
                    bddUIObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }

                var uiKeys = AssetKeyProvider.TryGetKeysFromSerializedArrayStringField(bddUIObj, "Entries", "key");

                if (uiKeys.Count > 0)
                {
                    var saved = node.Ports[portId].Label ?? "";
                    if (!string.IsNullOrWhiteSpace(saved) && !uiKeys.Contains(saved))
                        uiKeys.Insert(0, saved);

                    var initial = !string.IsNullOrWhiteSpace(saved) ? saved : uiKeys[0];

                    var popup = new PopupField<string>(uiKeys, initial);
                    popup.name = ChoiceLabelFieldName;
                    popup.userData = portId;
                    popup.style.minWidth = 160;
                    popup.style.flexGrow = 1;
                    popup.style.marginRight = 10;

                    popup.RegisterValueChangedCallback(evt =>
                    {
                        var id = popup.userData as string;
                        if (!string.IsNullOrEmpty(id) && node.Ports.ContainsKey(id))
                            node.Ports[id].Label = evt.newValue ?? "";

                        port.portName = "";
                    });

                    popup.SetValueWithoutNotify(initial);
                    labelFieldElement = popup;
                }
                else
                {
                    var labelField = new TextField { value = node.Ports[portId].Label ?? "" };
                    labelField.name = ChoiceLabelFieldName;
                    labelField.userData = portId;
                    labelField.style.minWidth = 160;
                    labelField.style.flexGrow = 1;
                    labelField.style.marginRight = 10;

                    labelField.RegisterValueChangedCallback(e =>
                    {
                        var id = labelField.userData as string;
                        if (!string.IsNullOrEmpty(id) && node.Ports.ContainsKey(id))
                            node.Ports[id].Label = e.newValue ?? "";

                        port.portName = "";
                    });

                    labelFieldElement = labelField;
                }

                var condField = new TextField { value = node.Ports[portId].Condition ?? "" };
                condField.name = ChoiceCondFieldName;
                condField.userData = portId;
                condField.style.minWidth = 160;
                condField.style.flexGrow = 1;
                condField.style.marginRight = 10;

                condField.RegisterValueChangedCallback(e =>
                {
                    var id = condField.userData as string;
                    if (!string.IsNullOrEmpty(id) && node.Ports.ContainsKey(id))
                        node.Ports[id].Condition = e.newValue ?? "";
                });

                var delete = new Button(() => RemovePort(node, portId)) { text = "✕" };
                delete.style.width = 26;
                delete.style.height = 18;
                delete.style.marginLeft = 2;

                row.Add(labelFieldElement);
                row.Add(condField);
                row.Add(delete);

                port.contentContainer.style.flexDirection = FlexDirection.Row;
                port.contentContainer.style.alignItems = Align.Center;
                port.contentContainer.style.justifyContent = Justify.SpaceBetween;
                port.contentContainer.style.flexGrow = 1;

                port.contentContainer.Add(row);

                var conn = port.contentContainer.Q<VisualElement>("connector");
                if (conn != null)
                {
                    port.contentContainer.Remove(conn);
                    port.contentContainer.Add(conn);
                }
            }
            else
            {
                var tag = new Label(node.Ports[portId].Label ?? "");
                tag.style.flexGrow = 1;
                tag.style.marginRight = 10;
                tag.style.opacity = 0.95f;

                row.Add(tag);

                port.contentContainer.style.flexDirection = FlexDirection.Row;
                port.contentContainer.style.alignItems = Align.Center;
                port.contentContainer.style.justifyContent = Justify.SpaceBetween;
                port.contentContainer.style.flexGrow = 1;

                port.contentContainer.Add(row);

                var conn = port.contentContainer.Q<VisualElement>("connector");
                if (conn != null)
                {
                    port.contentContainer.Remove(conn);
                    port.contentContainer.Add(conn);
                }
            }

            node.outputContainer.Add(port);

            node.RefreshPorts();
            node.RefreshExpandedState();

            EnsureChoicesHeaderVisibility(node);
        }

        private void RemovePort(DialogueNode node, string portId)
        {
            var edgesToRemove = edges.ToList().Where(e =>
                e.output != null &&
                e.output.node == node &&
                (e.output.userData as string) == portId
            ).ToList();

            foreach (var edge in edgesToRemove)
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                RemoveElement(edge);
            }

            var port = node.outputContainer.Children().OfType<Port>()
                .FirstOrDefault(p => (p.userData as string) == portId);

            if (port != null)
                node.outputContainer.Remove(port);

            node.Ports.Remove(portId);

            node.RefreshPorts();
            node.RefreshExpandedState();

            EnsureChoicesHeaderVisibility(node);
        }

        private Port GetPortInstance(DialogueNode node, Direction direction, Port.Capacity capacity)
        {
            return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(float));
        }

        private DialogueNode GetEntryPointNodeInstance()
        {
            var node = new DialogueNode
            {
                title = "START",
                GUID = Guid.NewGuid().ToString(),
                EntyPoint = true,
                NodeType = DialogueNodeType.Dialogue
            };

            var outPort = GetPortInstance(node, Direction.Output, Port.Capacity.Single);
            outPort.portName = "Next";
            outPort.userData = "start";
            node.outputContainer.Add(outPort);

            node.capabilities &= ~Capabilities.Movable;
            node.capabilities &= ~Capabilities.Deletable;

            node.SetPosition(new Rect(100, 200, 140, 110));
            node.RefreshExpandedState();
            node.RefreshPorts();

            EntryPointNode = node;
            return node;
        }

        public void RefreshNodeFields(DialogueNode node)
        {
            foreach (var tf in node.mainContainer.Query<TextField>().ToList())
            {
                if (tf.label == "Debug Label") tf.SetValueWithoutNotify(node.DebugLabel ?? "");
                else if (tf.label == "Branch Condition") tf.SetValueWithoutNotify(node.ConditionExpression ?? "");
            }

            var popup = node.mainContainer.Q<PopupField<string>>();
            if (popup != null)
            {
                var desired = node.DialogueText ?? "";
                if (!string.IsNullOrEmpty(desired) && !popup.choices.Contains(desired))
                    popup.choices.Insert(0, desired);

                if (string.IsNullOrEmpty(desired) && popup.choices.Count > 0)
                    desired = popup.choices[0];

                popup.SetValueWithoutNotify(desired);
                node.DialogueText = desired;
            }
            else
            {
                foreach (var textF in node.mainContainer.Query<TextField>().ToList())
                {
                    if (textF.label == "Dialogue Text") textF.SetValueWithoutNotify(node.DialogueText ?? "");
                }
            }

            EnsureChoicesHeaderVisibility(node);
        }

        private static class AssetKeyProvider
        {
            public static List<string> TryGetKeysFromSerializedArrayStringField(UnityEngine.Object assetObj, string arrayName, string keyField)
            {
                try
                {
                    if (assetObj == null) return new List<string>();
                    var so = new SerializedObject(assetObj);
                    var arr = so.FindProperty(arrayName);
                    if (arr == null || !arr.isArray) return new List<string>();

                    var keys = new List<string>(arr.arraySize);
                    for (var i = 0; i < arr.arraySize; i++)
                    {
                        var element = arr.GetArrayElementAtIndex(i);
                        if (element == null) continue;
                        var kp = element.FindPropertyRelative(keyField);
                        if (kp == null || kp.propertyType != SerializedPropertyType.String) continue;
                        var s = kp.stringValue;
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        keys.Add(s);
                    }

                    keys = keys.Distinct().ToList();
                    keys.Sort(StringComparer.Ordinal);
                    return keys;
                }
                catch
                {
                    return new List<string>();
                }
            }
        }
    }
}