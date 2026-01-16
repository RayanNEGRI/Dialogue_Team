using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using Subtegral.DialogueSystem.DataContainers;

namespace Subtegral.DialogueSystem.Editor
{
    public class DialogueNode : Node
    {
        public string GUID;
        public bool EntyPoint = false;

        public DialogueNodeType NodeType = DialogueNodeType.Dialogue;

        public string DebugLabel = "";
        public string DialogueText = "";
        public string ConditionExpression = "";

        public Dictionary<string, ChoicePortData> Ports = new Dictionary<string, ChoicePortData>();

        public class ChoicePortData
        {
            public string PortId;
            public string Label;
            public string Condition;

            public ChoicePortData(string portId, string label, string condition)
            {
                PortId = portId;
                Label = label;
                Condition = condition;
            }
        }
    }
}
