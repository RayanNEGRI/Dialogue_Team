using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements; // Nécessaire pour PopupField
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

        public StoryGraphView(StoryGraph editorWindow)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("NarrativeGraph"));
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
                if (startPort != port && startPort.node != port.node)
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
            var field = new BlackboardField { text = localName, typeText = "string" };
            container.Add(field);

            var valueField = new TextField("Value:") { value = localValue };
            valueField.RegisterValueChangedCallback(evt =>
            {
                var idx = ExposedProperties.FindIndex(x => x.PropertyName == item.PropertyName);
                ExposedProperties[idx].PropertyValue = evt.newValue;
            });

            container.Add(new BlackboardRow(field, valueField));
            Blackboard.Add(container);
        }

        public DialogueNode CreateNode(string title, Vector2 position, DialogueNodeType type)
        {
            var node = new DialogueNode
            {
                GUID = Guid.NewGuid().ToString(),
                NodeType = type,
                title = type.ToString().ToUpper(),
                DebugLabel = "",
                DialogueText = "",
                ConditionExpression = ""
            };

            node.styleSheets.Add(Resources.Load<StyleSheet>("Node"));

            var inputPort = GetPortInstance(node, Direction.Input, Port.Capacity.Multi);
            inputPort.portName = "Input";
            node.inputContainer.Add(inputPort);

            var debugField = new TextField("Debug Label");
            debugField.SetValueWithoutNotify(node.DebugLabel);
            debugField.RegisterValueChangedCallback(e => node.DebugLabel = e.newValue);
            node.mainContainer.Add(debugField);

            if (type == DialogueNodeType.Dialogue)
                BuildDialogueNodeUI(node);
            else if (type == DialogueNodeType.Branch)
                BuildBranchNodeUI(node);
            else if (type == DialogueNodeType.End)
                BuildEndNodeUI(node);

            node.SetPosition(new Rect(position, DefaultNodeSize));
            node.RefreshExpandedState();
            node.RefreshPorts();

            AddElement(node);
            return node;
        }

        private void BuildDialogueNodeUI(DialogueNode node)
        {
            // 1. Recherche de la BDD Dialogue
            var guids = AssetDatabase.FindAssets("t:BDD_Dialogue");
            BDD_Dialogue bdd = null;
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                bdd = AssetDatabase.LoadAssetAtPath<BDD_Dialogue>(path);
            }

            // 2. Si BDD trouvée et non vide -> Menu Déroulant
            if (bdd != null && bdd.Entries != null && bdd.Entries.Count > 0)
            {
                List<string> displayOptions = bdd.Entries.Select(x => x.key).ToList();
                string defaultValue = displayOptions[0];

                if (displayOptions.Contains(node.DialogueText))
                {
                    defaultValue = node.DialogueText;
                }
                else if (!string.IsNullOrEmpty(node.DialogueText))
                {
                    displayOptions.Insert(0, node.DialogueText);
                    defaultValue = node.DialogueText;
                }

                var popup = new PopupField<string>("Clé Dialogue", displayOptions, defaultValue);

                popup.RegisterValueChangedCallback(evt =>
                {
                    node.DialogueText = evt.newValue;
                    node.title = evt.newValue;
                });

                // Initialisation des valeurs
                node.DialogueText = defaultValue;
                if (string.IsNullOrEmpty(node.title) || node.title == "DIALOGUE")
                    node.title = defaultValue;

                node.mainContainer.Add(popup);
            }
            else
            {
                // 3. Fallback : Si pas de BDD, on met le TextField classique
                var dialogueField = new TextField("Dialogue Text") { multiline = true };
                dialogueField.SetValueWithoutNotify(node.DialogueText);
                dialogueField.RegisterValueChangedCallback(e => node.DialogueText = e.newValue);
                node.mainContainer.Add(dialogueField);
            }

            var addChoice = new Button(() =>
            {
                var portId = Guid.NewGuid().ToString();
                // --- MODIFICATION ICI : Chaîne vide au lieu de "Choix" ---
                node.Ports[portId] = new DialogueNode.ChoicePortData(portId, "", "");
                AddChoicePort(node, portId, deletable: true);
            })
            { text = "Add Choice" };

            node.titleButtonContainer.Add(addChoice);
        }

        private void BuildBranchNodeUI(DialogueNode node)
        {
            var condField = new TextField("Branch Condition");
            condField.SetValueWithoutNotify(node.ConditionExpression);
            condField.RegisterValueChangedCallback(e => node.ConditionExpression = e.newValue);
            node.mainContainer.Add(condField);

            EnsureFixedBranchPort(node, "true", "True");
            EnsureFixedBranchPort(node, "false", "False");
        }

        private void EnsureFixedBranchPort(DialogueNode node, string portId, string label)
        {
            if (!node.Ports.ContainsKey(portId))
                node.Ports[portId] = new DialogueNode.ChoicePortData(portId, label, "");

            AddChoicePort(node, portId, deletable: false);
        }

        private void BuildEndNodeUI(DialogueNode node)
        {
            var label = new Label("🏁 END");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginTop = 12;
            node.mainContainer.Add(label);
        }

        public void AddChoicePort(DialogueNode node, string portId, bool deletable)
        {
            if (!node.Ports.ContainsKey(portId))
                // --- MODIFICATION ICI : Chaîne vide au lieu de "Choix" ---
                node.Ports[portId] = new DialogueNode.ChoicePortData(portId, "", "");

            var data = node.Ports[portId];

            var port = GetPortInstance(node, Direction.Output, Port.Capacity.Single);
            port.userData = portId;

            var typeLabel = port.contentContainer.Q<Label>("type");
            if (typeLabel != null) port.contentContainer.Remove(typeLabel);

            // --- GESTION BDD_UI ---

            // 1. Recherche de la BDD UI
            var guids = AssetDatabase.FindAssets("t:BDD_UI");
            BDD_UI bddUI = null;
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                bddUI = AssetDatabase.LoadAssetAtPath<BDD_UI>(path);
            }

            port.contentContainer.Add(new Label("Label"));

            // 2. Si BDD_UI existe et a des entrées -> MENU DÉROULANT
            if (bddUI != null && bddUI.Entries != null && bddUI.Entries.Count > 0)
            {
                List<string> displayOptions = bddUI.Entries.Select(x => x.key).ToList();
                string defaultValue = displayOptions[0]; // Défaut = 1er de la liste

                // Logique de Load / Récupération robuste
                if (displayOptions.Contains(data.Label))
                {
                    defaultValue = data.Label;
                }
                else if (!string.IsNullOrEmpty(data.Label))
                {
                    // Si la valeur sauvegardée n'existe plus dans la BDD, on l'ajoute temporairement
                    displayOptions.Insert(0, data.Label);
                    defaultValue = data.Label;
                }

                // Application de la valeur si c'était vide (pour que data.Label prenne la valeur de la BDD)
                if (string.IsNullOrEmpty(data.Label))
                {
                    data.Label = defaultValue;
                }
                // Mise à jour visuelle du nom du port
                port.portName = defaultValue;

                var popup = new PopupField<string>(displayOptions, defaultValue);
                popup.name = ChoiceLabelFieldName;
                popup.userData = portId;

                popup.RegisterValueChangedCallback(evt =>
                {
                    data.Label = evt.newValue;
                    port.portName = evt.newValue;
                });

                port.contentContainer.Add(popup);
            }
            else
            {
                // 3. SINON (Fallback) -> TEXTFIELD CLASSIQUE
                var labelField = new TextField { value = data.Label };
                labelField.name = ChoiceLabelFieldName;
                labelField.userData = portId;
                labelField.RegisterValueChangedCallback(e =>
                {
                    data.Label = e.newValue;
                    port.portName = e.newValue;
                });

                // Si pas de BDD, on met à jour le portName avec ce qu'on a
                port.portName = data.Label;

                port.contentContainer.Add(labelField);
            }

            var condField = new TextField { value = data.Condition };
            condField.name = ChoiceCondFieldName;
            condField.userData = portId;
            condField.RegisterValueChangedCallback(e =>
            {
                data.Condition = e.newValue;
            });

            port.contentContainer.Add(new Label("Cond"));
            port.contentContainer.Add(condField);

            if (deletable)
            {
                var delete = new Button(() => RemovePort(node, portId)) { text = "X" };
                port.contentContainer.Add(delete);
            }

            node.outputContainer.Add(port);

            node.RefreshPorts();
            node.RefreshExpandedState();
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
                if (tf.label == "Debug Label") tf.SetValueWithoutNotify(node.DebugLabel);
                else if (tf.label == "Branch Condition") tf.SetValueWithoutNotify(node.ConditionExpression);
            }

            var popup = node.mainContainer.Q<PopupField<string>>();
            if (popup != null)
            {
                if (popup.choices.Contains(node.DialogueText))
                {
                    popup.value = node.DialogueText;
                }
                else
                {
                    if (!string.IsNullOrEmpty(node.DialogueText))
                    {
                        popup.choices.Insert(0, node.DialogueText);
                        popup.value = node.DialogueText;
                    }
                }
            }
            else
            {
                var tf = node.mainContainer.Q<TextField>("Dialogue Text");
                if (tf != null) tf.SetValueWithoutNotify(node.DialogueText);

                foreach (var textF in node.mainContainer.Query<TextField>().ToList())
                {
                    if (textF.label == "Dialogue Text") textF.SetValueWithoutNotify(node.DialogueText);
                }
            }
        }
    }
}
