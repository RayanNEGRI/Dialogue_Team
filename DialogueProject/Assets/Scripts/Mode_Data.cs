using UnityEngine;
using System.Collections.Generic;

public enum ModeType
{
    Bulle,  // par defaut
    Popup,
    Panel
}

[CreateAssetMenu(fileName = "Mode_Data", menuName = "Data/Mode_Data")]
public class Mode_Data : ScriptableObject
{

    [System.Serializable]
    public class ModeEntry
    {
        public ModeType mode;

    }

    public List<ModeEntry> Mode = new List<ModeEntry>();
}