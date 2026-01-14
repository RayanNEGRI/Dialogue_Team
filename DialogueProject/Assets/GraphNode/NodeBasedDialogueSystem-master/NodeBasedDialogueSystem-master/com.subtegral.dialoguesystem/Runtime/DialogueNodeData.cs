using System;
using UnityEngine;

namespace Subtegral.DialogueSystem.DataContainers
{
    [Serializable]
    public class DialogueNodeData
    {
        public string NodeGUID;
        public string DialogueText;
        public Vector2 Position;

        public ScriptableObject Speaker; // BDD_Speaker

        public string MoodKey;
    }
}

