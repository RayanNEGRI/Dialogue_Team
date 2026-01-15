using Subtegral.DialogueSystem.DataContainers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

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
            SaveLinks(asset);
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
                if (node == null || node.EntyPoint) continue;

                var nodeType = node.NodeType == DialogueNodeType.End ? DialogueNodeType.Dialogue : node.NodeType;

                asset.DialogueNodeData.Add(new DialogueNodeData
                {
                    NodeGUID = node.GUID,
                    NodeType = nodeType,
                    DebugLabel = node.DebugLabel ?? "",
                    DialogueText = node.DialogueText ?? "",
                    ConditionExpression = node.ConditionExpression ?? "",
                    Position = node.GetPosition().position
                });
            }
        }

        private void SaveLinks(DialogueContainer asset)
        {
            asset.NodeLinks.Clear();

            foreach (var node in Nodes)
            {
                if (node == null) continue;

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

                    node.Ports.TryGetValue(portId, out var data);

                    var label = data?.Label ?? "";
                    var cond = data?.Condition ?? "";

                    if (string.IsNullOrWhiteSpace(label))
                    {
                        var popup = port.contentContainer.Q<PopupField<string>>(StoryGraphView.ChoiceLabelFieldName);
                        if (popup != null) label = popup.value ?? "";

                        if (string.IsNullOrWhiteSpace(label))
                        {
                            var tf = port.contentContainer.Q<TextField>(StoryGraphView.ChoiceLabelFieldName);
                            if (tf != null) label = tf.value ?? "";
                        }
                    }

                    if (string.IsNullOrWhiteSpace(cond))
                    {
                        var ctf = port.contentContainer.Q<TextField>(StoryGraphView.ChoiceCondFieldName);
                        if (ctf != null) cond = ctf.value ?? "";
                    }

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
            asset.ExposedProperties.AddRange(_graphView.ExposedProperties ?? new List<ExposedProperty>());
        }

        private void SaveCommentBlocks(DialogueContainer asset)
        {
            asset.CommentBlockData.Clear();

            foreach (var block in CommentBlocks)
            {
                if (block == null) continue;
                var nodes = block.containedElements.OfType<DialogueNode>().Select(n => n.GUID).ToList();
                asset.CommentBlockData.Add(new CommentBlockData
                {
                    Title = block.title ?? "Comment Block",
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
            var entry = Nodes.FirstOrDefault(n => n != null && n.EntyPoint);
            if (entry != null)
            {
                var first = _container.NodeLinks.FirstOrDefault(l => l != null && string.Equals(l.PortId, "start", StringComparison.OrdinalIgnoreCase));
                if (first != null) entry.GUID = first.BaseNodeGUID;
            }

            foreach (var edge in Edges.ToList())
                _graphView.RemoveElement(edge);

            foreach (var node in Nodes.ToList())
            {
                if (node == null || node.EntyPoint) continue;
                _graphView.RemoveElement(node);
            }

            foreach (var group in CommentBlocks.ToList())
                _graphView.RemoveElement(group);
        }

        private void GenerateNodes()
        {
            foreach (var data in _container.DialogueNodeData ?? new List<DialogueNodeData>())
            {
                if (data == null || string.IsNullOrEmpty(data.NodeGUID)) continue;

                var t = data.NodeType == DialogueNodeType.End ? DialogueNodeType.Dialogue : data.NodeType;

                var node = _graphView.CreateNode(t.ToString(), data.Position, t);

                node.GUID = data.NodeGUID;
                node.DebugLabel = data.DebugLabel ?? "";
                node.DialogueText = data.DialogueText ?? "";
                node.ConditionExpression = data.ConditionExpression ?? "";

                _graphView.RefreshNodeFields(node);

                var links = (_container.NodeLinks ?? new List<NodeLinkData>())
                    .Where(l => l != null && l.BaseNodeGUID == data.NodeGUID && !string.Equals(l.PortId, "start", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (node.NodeType == DialogueNodeType.Branch)
                {
                    foreach (var link in links)
                    {
                        var portId = link.PortId ?? "";
                        if (string.IsNullOrEmpty(portId)) continue;
                        if (!node.Ports.ContainsKey(portId)) continue;

                        node.Ports[portId].Label = portId.Equals("true", StringComparison.OrdinalIgnoreCase) ? "True" : "False";
                        node.Ports[portId].Condition = "";
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
                }

                _graphView.RefreshNodeFields(node);
            }
        }

        private void ConnectNodes()
        {
            foreach (var link in _container.NodeLinks ?? new List<NodeLinkData>())
            {
                if (link == null) continue;
                if (string.IsNullOrEmpty(link.TargetNodeGUID)) continue;

                var baseNode = Nodes.FirstOrDefault(n => n != null && n.GUID == link.BaseNodeGUID);
                var targetNode = Nodes.FirstOrDefault(n => n != null && n.GUID == link.TargetNodeGUID);
                if (baseNode == null || targetNode == null) continue;

                Port outputPort;

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
            if (_container.ExposedProperties == null) return;
            foreach (var p in _container.ExposedProperties)
                _graphView.AddPropertyToBlackBoard(p, true);
        }

        private void GenerateCommentBlocks()
        {
            if (_container.CommentBlockData == null) return;

            foreach (var data in _container.CommentBlockData)
            {
                if (data == null) continue;
                var group = _graphView.CreateCommentBlock(new Rect(data.Position, _graphView.DefaultCommentBlockSize), data);
                group.AddElements(Nodes.Where(n => n != null && data.ChildNodes.Contains(n.GUID)));
            }
        }
    }
}