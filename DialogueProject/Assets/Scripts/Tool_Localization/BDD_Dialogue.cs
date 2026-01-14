using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BDD_Dialogue", menuName = "Data/BDD_Dialogue")]
public class BDD_Dialogue : ScriptableObject
{
    [System.Serializable]
    public class TranslationData
    {
        public string languageCode; // ex: "en", "fr", "es"
        public string text;
    }

    [System.Serializable]
    public class DialogueEntry
    {
        public string key;
        public List<TranslationData> translations = new List<TranslationData>();
    }

    public List<DialogueEntry> Entries = new List<DialogueEntry>();
}
