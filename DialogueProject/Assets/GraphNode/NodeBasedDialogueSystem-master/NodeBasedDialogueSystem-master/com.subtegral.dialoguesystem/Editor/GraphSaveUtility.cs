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
    public class GraphSaveUtility
    {
        private StoryGraphView _graphView;
        private DialogueContainer _container;

        private List<Edge> Edges => _graphView.edges.ToList();
        private List<DialogueNode> Nodes => _graphView.nodes.ToList().OfType<DialogueNode>().ToList();
        private List<Group> CommentBlocks => _graphView.graphElements.ToList().OfType<Group>().ToList();

        public static GraphSaveUtility GetInstance(StoryGraphView graphView)
        {
            return new GraphSaveUtility { _graphView = graphView };
        }

        public void SaveGraph(string fileName)
        {
            var asset = ScriptableObject.CreateInstance<DialogueContainer>();

            SaveNodes(asset);
            SaveLinksFromUI(asset);
            SaveExposedProperties(asset);
            SaveCommentBlocks(asset);

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var path = $"Assets/Resources/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueContainer>(path);

            if (existing == null)
                AssetDatabase.CreateAsset(asset, path);
            else
            {
                existing.DialogueNodeData = asset.DialogueNodeData;
                existing.NodeLinks = asset.NodeLinks;
                existing.ExposedProperties = asset.ExposedProperties;
                existing.CommentBlockData = asset.CommentBlockData;
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
        }

        private void SaveNodes(DialogueContainer asset)
        {
            asset.DialogueNodeData.Clear();

            foreach (var node in Nodes)
            {
                if (node.EntyPoint) continue;

                asset.DialogueNodeData.Add(new DialogueNodeData
                {
                    NodeGUID = node.GUID,
                    NodeType = node.NodeType,
                    DebugLabel = node.DebugLabel ?? "",
                    DialogueText = node.DialogueText ?? "",
                    ConditionExpression = node.ConditionExpression ?? "",
                    Position = node.GetPosition().position
                });
            }
        }

        private void SaveLinksFromUI(DialogueContainer asset)
        {
            asset.NodeLinks.Clear();

            foreach (var node in Nodes)
            {
                if (node.EntyPoint)
                {
                    var startPort = node.outputContainer.Children().OfType<Port>().FirstOrDefault();
                    var edge = Edges.FirstOrDefault(e => e.output == startPort);

                    asset.NodeLinks.Add(new NodeLinkData
                    {
                        BaseNodeGUID = node.GUID,
                        PortId = "start",
                        PortLabel = "Next",
                        ConditionExpression = "",
                        PortName = "Next",
                        TargetNodeGUID = edge != null ? ((DialogueNode)edge.input.node).GUID : null
                    });

                    continue;
                }

                foreach (var port in node.outputContainer.Children().OfType<Port>())
                {
                    var portId = port.userData as string;
                    if (string.IsNullOrEmpty(portId)) continue;

                    TextField labelField = null;
                    TextField condField = null;

                    foreach (var tf in port.contentContainer.Query<TextField>().ToList())
                    {
                        if ((tf.userData as string) != portId) continue;

                        if (tf.name == StoryGraphView.ChoiceLabelFieldName) labelField = tf;
                        else if (tf.name == StoryGraphView.ChoiceCondFieldName) condField = tf;
                    }

                    var label = labelField != null ? labelField.value : "";
                    var cond = condField != null ? condField.value : "";

                    var edge = Edges.FirstOrDefault(e => e.output == port);

                    asset.NodeLinks.Add(new NodeLinkData
                    {
                        BaseNodeGUID = node.GUID,
                        PortId = portId,
                        PortLabel = label ?? "",
                        ConditionExpression = cond ?? "",
                        PortName = label ?? "",
                        TargetNodeGUID = edge != null ? ((DialogueNode)edge.input.node).GUID : null
                    });
                }
            }
        }

        private void SaveExposedProperties(DialogueContainer asset)
        {
            asset.ExposedProperties.Clear();
            asset.ExposedProperties.AddRange(_graphView.ExposedProperties);
        }

        private void SaveCommentBlocks(DialogueContainer asset)
        {
            asset.CommentBlockData.Clear();

            foreach (var block in CommentBlocks)
            {
                var nodes = block.containedElements.OfType<DialogueNode>().Select(n => n.GUID).ToList();
                asset.CommentBlockData.Add(new CommentBlockData
                {
                    Title = block.title,
                    Position = block.GetPosition().position,
                    ChildNodes = nodes
                });
            }
        }

        public void LoadNarrative(string fileName)
        {
            _container = Resources.Load<DialogueContainer>(fileName);
            if (_container == null)
            {
                EditorUtility.DisplayDialog("File Not Found", "Target Narrative Data does not exist!", "OK");
                return;
            }

            ClearGraph();
            GenerateNodes();
            ConnectNodes();
            AddExposedProperties();
            GenerateCommentBlocks();
        }

        private void ClearGraph()
        {
            var entry = Nodes.First(n => n.EntyPoint);

            var first = _container.NodeLinks.FirstOrDefault();
            if (first != null) entry.GUID = first.BaseNodeGUID;

            foreach (var node in Nodes.ToList())
            {
                if (node.EntyPoint) continue;

                foreach (var edge in Edges.Where(e => e.input.node == node || e.output.node == node).ToList())
                    _graphView.RemoveElement(edge);

                _graphView.RemoveElement(node);
            }

            foreach (var group in CommentBlocks.ToList())
                _graphView.RemoveElement(group);
        }

        private void GenerateNodes()
        {
            foreach (var data in _container.DialogueNodeData)
            {
                var node = _graphView.CreateNode(data.NodeType.ToString(), data.Position, data.NodeType);

                node.GUID = data.NodeGUID;
                node.DebugLabel = data.DebugLabel ?? "";
                node.DialogueText = data.DialogueText ?? "";
                node.ConditionExpression = data.ConditionExpression ?? "";

                _graphView.RefreshNodeFields(node);

                var links = _container.NodeLinks.Where(l => l.BaseNodeGUID == data.NodeGUID).ToList();

                if (node.NodeType == DialogueNodeType.Branch)
                {
                    foreach (var link in links)
                    {
                        var portId = link.PortId;
                        if (string.IsNullOrEmpty(portId)) continue;
                        if (!node.Ports.ContainsKey(portId)) continue;

                        node.Ports[portId].Label = link.PortLabel ?? "";
                        node.Ports[portId].Condition = link.ConditionExpression ?? "";

                        ForcePortUI(node, portId, link.PortLabel ?? "", link.ConditionExpression ?? "");
                    }

                    continue;
                }

                foreach (var link in links)
                {
                    var portId = string.IsNullOrEmpty(link.PortId) ? Guid.NewGuid().ToString() : link.PortId;

                    if (!node.Ports.ContainsKey(portId))
                        node.Ports[portId] = new DialogueNode.ChoicePortData(
                            portId,
                            link.PortLabel ?? "",
                            link.ConditionExpression ?? ""
                        );

                    _graphView.AddChoicePort(node, portId, true);
                    ForcePortUI(node, portId, link.PortLabel ?? "", link.ConditionExpression ?? "");
                }
            }
        }

        private void ForcePortUI(DialogueNode node, string portId, string label, string cond)
        {
            var port = node.outputContainer.Children().OfType<Port>()
                .FirstOrDefault(p => (p.userData as string) == portId);

            if (port == null) return;

            foreach (var tf in port.contentContainer.Query<TextField>().ToList())
            {
                if ((tf.userData as string) != portId) continue;

                if (tf.name == StoryGraphView.ChoiceLabelFieldName) tf.SetValueWithoutNotify(label);
                else if (tf.name == StoryGraphView.ChoiceCondFieldName) tf.SetValueWithoutNotify(cond);
            }

            port.portName = label;
        }

        private void ConnectNodes()
        {
            foreach (var link in _container.NodeLinks)
            {
                if (string.IsNullOrEmpty(link.TargetNodeGUID)) continue;

                var baseNode = Nodes.FirstOrDefault(n => n.GUID == link.BaseNodeGUID);
                var targetNode = Nodes.FirstOrDefault(n => n.GUID == link.TargetNodeGUID);
                if (baseNode == null || targetNode == null) continue;

                Port outputPort = null;

                if (baseNode.EntyPoint)
                {
                    outputPort = baseNode.outputContainer.Children().OfType<Port>().FirstOrDefault();
                }
                else
                {
                    outputPort = baseNode.outputContainer.Children().OfType<Port>()
                        .FirstOrDefault(p => (p.userData as string) == link.PortId);
                }

                var inputPort = targetNode.inputContainer.Children().OfType<Port>().FirstOrDefault();
                if (outputPort == null || inputPort == null) continue;

                var edge = new Edge { output = outputPort, input = inputPort };
                edge.output.Connect(edge);
                edge.input.Connect(edge);
                _graphView.Add(edge);
            }
        }

        private void AddExposedProperties()
        {
            _graphView.ClearBlackBoardAndExposedProperties();
            foreach (var p in _container.ExposedProperties)
                _graphView.AddPropertyToBlackBoard(p, true);
        }

        private void GenerateCommentBlocks()
        {
            foreach (var data in _container.CommentBlockData)
            {
                var group = _graphView.CreateCommentBlock(new Rect(data.Position, _graphView.DefaultCommentBlockSize), data);
                group.AddElements(Nodes.Where(n => data.ChildNodes.Contains(n.GUID)));
            }
        }
    }
}