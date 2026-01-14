using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;

public class LocalizationManagerWindow : EditorWindow
{
    private StringTableCollection selectedCollection;
    private TextAsset csvFile;
    private BDD_Dialogue targetBDD; // Pour la sauvegarde vers ScriptableObject
    private Vector2 scrollPosition;

    private int selectedLanguageIndex = 0;
    private string[] availableSystemLanguages;
    private int selectedRemoveLanguageIndex = 0;

    [MenuItem("Tools/Ultimate Localization Manager")]
    public static void ShowWindow() => GetWindow<LocalizationManagerWindow>("Loc Manager");

    private void OnEnable()
    {
        availableSystemLanguages = System.Enum.GetNames(typeof(SystemLanguage))
            .Where(x => x != "Unknown")
            .ToArray();

        selectedLanguageIndex = System.Array.IndexOf(availableSystemLanguages, "English");
        if (selectedLanguageIndex < 0) selectedLanguageIndex = 0;
    }

    private void OnGUI()
    {
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("Gestionnaire de Localisation Pro", titleStyle);
        GUILayout.Space(10);

        DrawCollectionSelector();
        if (selectedCollection == null) return;

        DrawSeparator();

        // 2. SECTION IMPORT / EXPORT
        DrawDataManagement();

        DrawSeparator();

        // 3. GESTION DES LANGUES
        GUILayout.BeginHorizontal();
        DrawLanguageAdder();
        GUILayout.Space(20);
        DrawLanguageRemover();
        GUILayout.EndHorizontal();

        DrawSeparator();

        EditorGUILayout.HelpBox("L'automatisation est active : Les clés sont générées selon le texte FR (consonnes, max 10 chars).", MessageType.Info);

        // 4. TABLEAU DES TRADUCTIONS
        DrawFullTable();
    }

    private void DrawCollectionSelector()
    {
        GUILayout.BeginHorizontal("box");
        if (selectedCollection == null)
        {
            var guids = AssetDatabase.FindAssets("t:StringTableCollection");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                selectedCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            }
        }
        selectedCollection = (StringTableCollection)EditorGUILayout.ObjectField("Table Cible", selectedCollection, typeof(StringTableCollection), false);
        if (GUILayout.Button("Ouvrir", GUILayout.Width(60))) AssetDatabase.OpenAsset(selectedCollection);
        GUILayout.EndHorizontal();
    }

    private void DrawDataManagement()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Gestion des Données (CSV & ScriptableObject)", EditorStyles.boldLabel);

        // Import CSV
        GUILayout.BeginHorizontal();
        csvFile = (TextAsset)EditorGUILayout.ObjectField("Fichier .csv", csvFile, typeof(TextAsset), false);
        if (GUILayout.Button("Importer CSV", GUILayout.Width(100)))
        {
            if (csvFile != null) SyncCSVDirectly();
            else Debug.LogError("Glissez un fichier .csv !");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Export/Sauvegarde ScriptableObject MULTI-LANGUE
        GUILayout.BeginHorizontal();
        targetBDD = (BDD_Dialogue)EditorGUILayout.ObjectField("BDD Dialogue", targetBDD, typeof(BDD_Dialogue), false);
        if (GUILayout.Button("Sauvegarder SO", GUILayout.Width(100)))
        {
            if (targetBDD != null) SaveToScriptableObject();
            else Debug.LogError("Assignez un asset BDD_Dialogue pour sauvegarder !");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // --- OPTION : EXPORT CSV (FORMAT COMPATIBLE IMPORT) ---
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("Exporter toute la Table en CSV", GUILayout.Height(25)))
        {
            ExportToCSV();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void ExportToCSV()
    {
        var allLocales = LocalizationEditorSettings.GetLocales();
        if (allLocales == null || allLocales.Count == 0) return;

        string path = EditorUtility.SaveFilePanel("Exporter la table en CSV", "", "BDD_Dialogue_Export.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        StringBuilder sb = new StringBuilder();

        // 1. Entête : ID, Key, PUIS TOUTES LES LANGUES
        List<string> header = new List<string> { "ID", "Key" };
        foreach (var locale in allLocales) header.Add(locale.Identifier.Code.ToUpper());
        sb.AppendLine(string.Join(",", header));

        // 2. Contenu
        foreach (var sharedEntry in selectedCollection.SharedData.Entries)
        {
            List<string> row = new List<string>();
            row.Add(sharedEntry.Id.ToString());
            row.Add(sharedEntry.Key);

            foreach (var locale in allLocales)
            {
                var table = selectedCollection.GetTable(locale.Identifier) as StringTable;
                var entry = table?.GetEntry(sharedEntry.Id);
                // On remplace les virgules par des points-virgules pour le CSV
                string val = entry != null ? entry.Value.Replace(",", ";").Replace("\n", " ") : "";
                row.Add(val);
            }
            sb.AppendLine(string.Join(",", row));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>Export CSV terminé : {path}</color>");
    }

    private void SaveToScriptableObject()
    {
        var allLocales = LocalizationEditorSettings.GetLocales();
        if (allLocales == null || allLocales.Count == 0)
        {
            Debug.LogError("Aucune langue configurée dans le projet.");
            return;
        }

        Undo.RecordObject(targetBDD, "Save Full Localization to BDD");
        targetBDD.Entries.Clear();

        foreach (var sharedEntry in selectedCollection.SharedData.Entries)
        {
            BDD_Dialogue.DialogueEntry newSOEntry = new BDD_Dialogue.DialogueEntry();
            newSOEntry.key = sharedEntry.Key;
            newSOEntry.translations = new List<BDD_Dialogue.TranslationData>();

            foreach (var locale in allLocales)
            {
                var table = selectedCollection.GetTable(locale.Identifier) as StringTable;
                if (table != null)
                {
                    var entry = table.GetEntry(sharedEntry.Id);
                    newSOEntry.translations.Add(new BDD_Dialogue.TranslationData()
                    {
                        languageCode = locale.Identifier.Code,
                        text = (entry != null) ? entry.Value : ""
                    });
                }
            }
            targetBDD.Entries.Add(newSOEntry);
        }

        EditorUtility.SetDirty(targetBDD);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green>Sauvegarde ScriptableObject réussie !</color>");
    }

    private void SyncCSVDirectly()
    {
        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return;

        // 1. On lit les entêtes pour savoir quelle colonne correspond à quelle langue
        string[] headers = lines[0].Split(',');
        Dictionary<int, Locale> columnMap = new Dictionary<int, Locale>();
        var allLocales = LocalizationEditorSettings.GetLocales();

        for (int i = 0; i < headers.Length; i++)
        {
            string h = headers[i].Trim().ToUpper();
            if (h == "ID" || h == "KEY") continue;

            var locale = allLocales.FirstOrDefault(l => l.Identifier.Code.ToUpper() == h || l.name.ToUpper() == h);
            if (locale != null) columnMap.Add(i, locale);
        }

        Undo.RecordObject(selectedCollection.SharedData, "Sync CSV Keys");

        // 2. Importation
        int count = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split(',');
            if (cells.Length < 2) continue;

            string key = cells[1].Trim();
            if (string.IsNullOrEmpty(key)) continue;

            if (selectedCollection.SharedData.GetEntry(key) == null)
                selectedCollection.SharedData.AddKey(key);

            var sharedEntry = selectedCollection.SharedData.GetEntry(key);

            foreach (var col in columnMap)
            {
                if (col.Key >= cells.Length) continue;

                var table = selectedCollection.GetTable(col.Value.Identifier) as StringTable;
                if (table != null)
                {
                    Undo.RecordObject(table, "Sync CSV Value");
                    string textValue = cells[col.Key].Trim().Replace(";", ","); // Restaure les virgules
                    var entry = table.GetEntry(sharedEntry.Id);
                    if (entry == null) table.AddEntry(key, textValue);
                    else entry.Value = textValue;
                    EditorUtility.SetDirty(table);
                }
            }
            count++;
        }

        EditorUtility.SetDirty(selectedCollection.SharedData);
        AssetDatabase.SaveAssets();
        Debug.Log($"Synchronisation réussie : {count} entrées importées pour {columnMap.Count} langues.");
    }

    private void DrawFullTable()
    {
        var allLocales = LocalizationEditorSettings.GetLocales();
        if (allLocales == null || allLocales.Count == 0) return;

        var projectLocale = allLocales.FirstOrDefault(x => x.Identifier.Code.StartsWith("fr")) ?? allLocales.FirstOrDefault();
        var sortedLocales = allLocales.OrderBy(x => x == projectLocale ? 0 : 1).ToList();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var entries = selectedCollection.SharedData.Entries.ToList();
        long idToRemove = -1;

        foreach (var sharedEntry in entries)
        {
            if (sharedEntry == null) continue;

            GUILayout.BeginVertical("helpBox");
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(150));
            GUI.enabled = false;
            GUILayout.Label($"ID: {sharedEntry.Id}", EditorStyles.miniLabel);
            GUI.enabled = true;
            EditorGUILayout.SelectableLabel(sharedEntry.Key, EditorStyles.boldLabel, GUILayout.Height(20));

            if (GUILayout.Button("Supprimer", EditorStyles.miniButton, GUILayout.Width(80)))
                idToRemove = sharedEntry.Id;

            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            foreach (var locale in sortedLocales)
            {
                var stringTable = selectedCollection.GetTable(locale.Identifier) as StringTable;
                GUILayout.BeginHorizontal();
                bool isFR = (locale == projectLocale);
                GUILayout.Label(locale.Identifier.Code.ToUpper(), isFR ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(40));

                if (stringTable != null)
                {
                    var entry = stringTable.GetEntry(sharedEntry.Id);
                    string currentText = entry?.Value ?? "";
                    string newText = EditorGUILayout.TextArea(currentText, GUILayout.Height(20));

                    if (newText != currentText)
                    {
                        Undo.RecordObject(stringTable, "Edit Translation");
                        if (entry == null) stringTable.AddEntry(sharedEntry.Key, newText);
                        else entry.Value = newText;

                        if (isFR && !string.IsNullOrEmpty(newText))
                            GenerateSmartKey(sharedEntry, newText);

                        EditorUtility.SetDirty(stringTable);
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        if (idToRemove != -1)
        {
            RemoveKey(idToRemove);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndScrollView();
    }

    private void GenerateSmartKey(SharedTableData.SharedTableEntry sharedEntry, string content)
    {
        string finalKey = CalculateSmartKeyString(content);
        if (sharedEntry.Key != finalKey)
        {
            var existing = selectedCollection.SharedData.GetEntry(finalKey);
            if (existing != null && existing.Id != sharedEntry.Id) return;

            Undo.RecordObject(selectedCollection.SharedData, "Auto Rename Key");
            selectedCollection.SharedData.RenameKey(sharedEntry.Key, finalKey);
            EditorUtility.SetDirty(selectedCollection.SharedData);
        }
    }

    private string CalculateSmartKeyString(string content)
    {
        string cleaned = RemoveAccents(content).ToLower();
        string consonnes = Regex.Replace(cleaned, "[aeiouyhàâéèêëîïôûùç ]", "");
        if (consonnes.Length > 10) consonnes = consonnes.Substring(0, 10);
        if (string.IsNullOrEmpty(consonnes)) consonnes = "dlg";

        int index = 1;
        string finalKey = $"{consonnes}_{index}";
        while (selectedCollection.SharedData.GetEntry(finalKey) != null) { index++; finalKey = $"{consonnes}_{index}"; }
        return finalKey;
    }

    private string RemoveAccents(string text) => new string(text.Normalize(NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());

    private void DrawLanguageAdder()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(300));
        GUILayout.Label("Ajouter une Langue", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        selectedLanguageIndex = EditorGUILayout.Popup(selectedLanguageIndex, availableSystemLanguages);
        if (GUILayout.Button("+")) CreateAndAddLocale(availableSystemLanguages[selectedLanguageIndex]);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawLanguageRemover()
    {
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        GUILayout.Label("Supprimer une Langue", EditorStyles.boldLabel);
        var active = LocalizationEditorSettings.GetLocales();
        if (active.Count > 0)
        {
            GUILayout.BeginHorizontal();
            string[] names = active.Select(x => x.name).ToArray();
            selectedRemoveLanguageIndex = EditorGUILayout.Popup(selectedRemoveLanguageIndex, names);
            if (GUILayout.Button("—", GUILayout.Width(30))) RemoveLocale(active[selectedRemoveLanguageIndex]);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void CreateAndAddLocale(string lang)
    {
        LocaleIdentifier id = new LocaleIdentifier((SystemLanguage)System.Enum.Parse(typeof(SystemLanguage), lang));
        if (LocalizationEditorSettings.GetLocales().Any(x => x.Identifier == id)) return;
        var newLocale = Locale.CreateLocale(id);
        AssetDatabase.CreateAsset(newLocale, AssetDatabase.GenerateUniqueAssetPath($"Assets/Localization/Locales/{lang}.asset"));
        LocalizationEditorSettings.AddLocale(newLocale);
        if (selectedCollection != null) selectedCollection.AddNewTable(id);
        AssetDatabase.SaveAssets();
    }

    private void RemoveLocale(Locale l)
    {
        if (EditorUtility.DisplayDialog("Supprimer", $"Supprimer {l.name} ?", "Oui", "Non"))
        {
            string path = AssetDatabase.GetAssetPath(l);
            if (selectedCollection != null) selectedCollection.RemoveTable(selectedCollection.GetTable(l.Identifier));
            LocalizationEditorSettings.RemoveLocale(l);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }
    }

    private void RemoveKey(long id)
    {
        if (EditorUtility.DisplayDialog("Supprimer", "Supprimer cette clé ?", "Oui", "Non"))
        {
            selectedCollection.SharedData.RemoveKey(id);
            foreach (var l in LocalizationEditorSettings.GetLocales())
            {
                var t = selectedCollection.GetTable(l.Identifier) as StringTable;
                if (t != null) t.RemoveEntry(id);
            }
            AssetDatabase.SaveAssets();
        }
    }

    private void DrawSeparator()
    {
        GUILayout.Space(10);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        GUILayout.Space(10);
    }
}
