using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BDD_Dialogue", menuName = "Data/BDD_Dialogue")]
public class BDD_Dialogue : ScriptableObject
{
    [System.Serializable]
    public class DialogueEntry 
    {
        public int id;
        public string key;
        public string fr;
        public string en;
    }

    public List<DialogueEntry> Entries = new List<DialogueEntry>();

    [Header("Import Settings")]
    public TextAsset CsvFileToImport;

    private void OnValidate()
    {
        if (CsvFileToImport == null) return;
        UpdateListFromCSV();
    }

    [ContextMenu("Force Update CSV")]
    private void UpdateListFromCSV()
    {
        Entries.Clear();
        string[] lines = CsvFileToImport.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cells = line.Split(',');
            if (cells.Length >= 4)
            {
                DialogueEntry newEntry = new DialogueEntry();
                int.TryParse(cells[0], out newEntry.id);
                newEntry.key = cells[1].Trim();
                newEntry.fr = cells[2].Trim();
                newEntry.en = cells[3].Trim();
                Entries.Add(newEntry);
            }
        }
    }
    public string GetTextByKey(string searchKey, Language lang)
    {
        foreach (var entry in Entries)
        {
            if (entry.key == searchKey)
            {
                if (lang == Language.French)
                {
                    return entry.fr;
                }
                else
                {
                    return entry.en;
                }
            }
        }

        Debug.LogWarning($"La clé '{searchKey}' est introuvable dans la BDD !");
        return "? " + searchKey + " ?";
    }
}
