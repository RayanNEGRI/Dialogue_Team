using UnityEngine;
using System.Collections.Generic;

public enum HumeurType
{
    Neutre,  // par défaut
    Triste,
    Joyeux,
    Colere,
    Fatigue
}

[CreateAssetMenu(fileName = "Speaker_Data", menuName = "Data/Speaker_Data")]
public class Speaker_Data : ScriptableObject
{

    [System.Serializable]
    public class SpeakerEntry
    {
        public string speakerName;
        public HumeurType humeur;
        public AnimationClip anime;
        public AudioClip audio;
    }

    public List<SpeakerEntry> Speakers = new List<SpeakerEntry>();
}