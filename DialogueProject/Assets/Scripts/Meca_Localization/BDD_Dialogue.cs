using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "BDD_Dialogue", menuName = "Data/BDD_Dialogue")]
public class BDD_Dialogue : ScriptableObject
{
    [System.Serializable]
    public class TranslationData
    {
        public string languageCode; // ex: "en", "fr", "es"

        [TextArea(3, 10)] 
        public string text;
    }

    [System.Serializable]
    public class DialogueEntry
    {
        public string key;
        public List<TranslationData> translations = new List<TranslationData>();

        public string GetText(string langCode)
        {
            var trad = translations.FirstOrDefault(x => x.languageCode == langCode);
            return trad != null ? trad.text : "";
        }
    }

    public List<DialogueEntry> Entries = new List<DialogueEntry>();

    public DialogueEntry GetEntry(string key)
    {
        return Entries.FirstOrDefault(x => x.key == key);
    }
}
