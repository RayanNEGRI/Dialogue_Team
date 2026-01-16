using System;

namespace Subtegral.DialogueSystem.DataContainers
{
    [Serializable]
    public class NodeLinkData
    {
        public string BaseNodeGUID;
        public string TargetNodeGUID;
        public string PortName;
        public string PortId;
        public string PortLabel;
        public string ConditionExpression;
    }
}