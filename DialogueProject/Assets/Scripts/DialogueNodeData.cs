using System;
using UnityEngine;

namespace Subtegral.DialogueSystem.DataContainers
{
    [Serializable]
    public class DialogueNodeData
    {
        public string NodeGUID;
        
        public DialogueNodeType NodeType = DialogueNodeType.Dialogue;
        
        public string DebugLabel;
        public string DialogueText;
        public string ConditionExpression;
        
        public Vector2 Position;

        public ScriptableObject Speaker; // BDD_Speaker

        public string MoodKey;
    }
}

