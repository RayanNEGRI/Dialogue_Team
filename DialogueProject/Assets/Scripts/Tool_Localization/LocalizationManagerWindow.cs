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
using System.Globalization;

public class LocalizationManagerWindow : EditorWindow
{
    // --- ENUM MODES ---
    private enum LocMode
    {
        Dialogues,
        InterfaceUI
    }

    // --- DONNEES PERSISTANTES ---
    [SerializeField] private StringTableCollection dialogueCollection;
    [SerializeField] private BDD_Dialogue dialogueBDD;

    [SerializeField] private StringTableCollection uiCollection;
    [SerializeField] private BDD_UI uiBDD; 

    // --- VARIABLES DE TRAVAIL ---
    private LocMode currentMode = LocMode.Dialogues;
    private StringTableCollection selectedCollection;

    // --- UI STATE ---
    private int selectedLanguageIndex = 0;
    private string[] availableSystemLanguages;
    private int selectedAddLanguageIndex = 0;
    private int selectedRemoveLanguageIndex = 0;

    // --- NAVIGATION ---
    private long selectedKeyId = -1;
    private Vector2 leftScrollPos;
    private Vector2 rightScrollPos;
    private string searchFilter = "";

    // --- CACHE TEMPORAIRE ---
    private string tempEditingText = "";
    private long lastEditedKeyId = -1;
    private TextAsset csvFile;

    [MenuItem("Tools/Localisation Tool")]
    public static void ShowWindow() => GetWindow<LocalizationManagerWindow>("Loc Manager");

    private void OnEnable()
    {
        availableSystemLanguages = System.Enum.GetNames(typeof(SystemLanguage))
            .Where(x => x != "Unknown")
            .OrderBy(x => x)
            .ToArray();

        selectedAddLanguageIndex = System.Array.IndexOf(availableSystemLanguages, "English");
        if (selectedAddLanguageIndex < 0) selectedAddLanguageIndex = 0;
    }

    private void OnGUI()
    {
        // 0. BARRE D'ONGLETS (TABS)
        GUILayout.Space(10);
        GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton);
        tabStyle.fontStyle = FontStyle.Bold;
        tabStyle.fixedHeight = 30;

        LocMode oldMode = currentMode;
        currentMode = (LocMode)GUILayout.Toolbar((int)currentMode, new string[] { "🗣️ DIALOGUES", "🖥️ INTERFACE (UI)" }, tabStyle);

        if (oldMode != currentMode)
        {
            selectedKeyId = -1;
            lastEditedKeyId = -1;
            tempEditingText = "";
            searchFilter = "";
            GUI.FocusControl(null);
        }

        // ASSIGNATION COLLECTION
        if (currentMode == LocMode.Dialogues) selectedCollection = dialogueCollection;
        else selectedCollection = uiCollection;

        DrawHeader();

        if (selectedCollection == null)
        {
            EditorGUILayout.HelpBox($"Veuillez assigner une Table Collection pour le mode {currentMode}.", MessageType.Info);
            return;
        }

        DrawSeparator();

        GUILayout.BeginHorizontal();
        {
            DrawLeftPanel();
            DrawRightPanel();
        }
        GUILayout.EndHorizontal();
    }

    // --- SECTIONS UI ---

    private void DrawHeader()
    {
        var activeLocales = LocalizationEditorSettings.GetLocales();

        GUILayout.BeginVertical("box");

        // 1. CONFIGURATION
        GUILayout.Label($"1. CONFIGURATION ({currentMode.ToString().ToUpper()})", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        GUILayout.BeginHorizontal();
        if (currentMode == LocMode.Dialogues)
        {
            dialogueCollection = (StringTableCollection)EditorGUILayout.ObjectField("Table Dialogues", dialogueCollection, typeof(StringTableCollection), false);
        }
        else
        {
            uiCollection = (StringTableCollection)EditorGUILayout.ObjectField("Table Interface UI", uiCollection, typeof(StringTableCollection), false);
        }

        if (GUILayout.Button("Ouvrir", GUILayout.Width(60)) && selectedCollection != null) AssetDatabase.OpenAsset(selectedCollection);
        GUILayout.EndHorizontal();

        // BDD Field
        if (currentMode == LocMode.Dialogues)
        {
            dialogueBDD = (BDD_Dialogue)EditorGUILayout.ObjectField("BDD Save (Dialogues)", dialogueBDD, typeof(BDD_Dialogue), false);
        }
        else
        {
            uiBDD = (BDD_UI)EditorGUILayout.ObjectField("BDD Save (UI)", uiBDD, typeof(BDD_UI), false);
        }

        if (EditorGUI.EndChangeCheck())
        {
            if (currentMode == LocMode.Dialogues) selectedCollection = dialogueCollection;
            else selectedCollection = uiCollection;
        }

        GUILayout.Space(10);

        // 2. ACTIONS
        GUILayout.Label("2. ACTIONS", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Sauvegarder SO", EditorStyles.miniButton, GUILayout.Height(25))) SaveToScriptableObject();
            if (GUILayout.Button("Export CSV", EditorStyles.miniButton, GUILayout.Height(25))) ExportToCSV();
            if (GUILayout.Button("Sync CSV (Base FR)", EditorStyles.miniButton, GUILayout.Height(25)))
            {
                if (csvFile == null) Debug.LogError("Assignez un CSV dans le champ ci-dessous !");
                else SyncCSVDirectly();
            }
        }
        GUILayout.EndHorizontal();

        csvFile = (TextAsset)EditorGUILayout.ObjectField("Fichier CSV (Import)", csvFile, typeof(TextAsset), false);

        GUILayout.Space(10);
        DrawSeparator();
        GUILayout.Space(10);

        // 3. GESTION DES LANGUES
        GUILayout.Label("3. GESTION DES LANGUES", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        {
            GUILayout.BeginVertical("helpBox");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ajouter :", GUILayout.Width(60));
            selectedAddLanguageIndex = EditorGUILayout.Popup(selectedAddLanguageIndex, availableSystemLanguages);
            if (GUILayout.Button("+", GUILayout.Width(25))) CreateAndAddLocale(availableSystemLanguages[selectedAddLanguageIndex]);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            GUILayout.BeginVertical("helpBox");
            if (activeLocales != null && activeLocales.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Supprimer :", GUILayout.Width(70));
                string[] activeNames = activeLocales.Select(x => x.name).ToArray();

                if (selectedRemoveLanguageIndex >= activeNames.Length) selectedRemoveLanguageIndex = 0;

                selectedRemoveLanguageIndex = EditorGUILayout.Popup(selectedRemoveLanguageIndex, activeNames);
                if (GUILayout.Button("-", GUILayout.Width(25))) RemoveLocale(activeLocales[selectedRemoveLanguageIndex]);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Aucune langue active.");
            }
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        DrawSeparator();
        GUILayout.Space(10);

        // 4. CHOIX DE LA LANGUE D'ÉDITION
        if (activeLocales != null && activeLocales.Count > 0)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("4. LANGUE D'ÉDITION (Affichage) :", EditorStyles.boldLabel, GUILayout.Width(220));

            string[] names = activeLocales.Select(x => $"{x.name} ({x.Identifier.Code.ToUpper()})").ToArray();

            if (selectedLanguageIndex >= names.Length) selectedLanguageIndex = 0;

            int newIndex = EditorGUILayout.Popup(selectedLanguageIndex, names);
            if (newIndex != selectedLanguageIndex)
            {
                selectedLanguageIndex = newIndex;
                GUI.FocusControl(null);
                lastEditedKeyId = -1;
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Erreur: Aucune langue configurée !", MessageType.Error);
        }

        GUILayout.EndVertical();
    }

    private void DrawLeftPanel()
    {
        GUILayout.BeginVertical("helpBox", GUILayout.Width(250), GUILayout.ExpandHeight(true));

        GUILayout.Label($"LISTE CLÉS ({currentMode})", EditorStyles.miniBoldLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label("🔍", GUILayout.Width(20));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        GUILayout.EndHorizontal();

        leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos);

        if (selectedCollection != null)
        {
            var entries = selectedCollection.SharedData.Entries
                .Where(x => string.IsNullOrEmpty(searchFilter) || x.Key.ToLower().Contains(searchFilter.ToLower()))
                .OrderBy(x => x.Key)
                .ToList();

            foreach (var entry in entries)
            {
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
                btnStyle.alignment = TextAnchor.MiddleLeft;

                if (entry.Id == selectedKeyId)
                {
                    btnStyle.normal.textColor = Color.cyan;
                    btnStyle.fontStyle = FontStyle.Bold;
                }

                if (GUILayout.Button(entry.Key, btnStyle, GUILayout.Height(24)))
                {
                    selectedKeyId = entry.Id;
                    lastEditedKeyId = -1;
                    GUI.FocusControl(null);
                }
            }
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ Nouvelle Clé", GUILayout.Height(30)))
        {
            CreateNewKeyDialog();
        }

        GUILayout.EndVertical();
    }

    private void DrawRightPanel()
    {
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedKeyId == -1)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("Sélectionnez une clé à gauche.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            return;
        }

        var allLocales = LocalizationEditorSettings.GetLocales();
        if (allLocales.Count == 0) return;

        if (selectedLanguageIndex >= allLocales.Count) selectedLanguageIndex = 0;
        var targetLocale = allLocales[selectedLanguageIndex];
        var refLocale = allLocales.FirstOrDefault(x => x.Identifier.Code.StartsWith("fr")) ?? allLocales[0];

        var targetTable = selectedCollection.GetTable(targetLocale.Identifier) as StringTable;
        var refTable = selectedCollection.GetTable(refLocale.Identifier) as StringTable;
        var sharedEntry = selectedCollection.SharedData.GetEntry(selectedKeyId);

        if (sharedEntry == null)
        {
            selectedKeyId = -1;
            GUILayout.EndVertical();
            return;
        }

        rightScrollPos = EditorGUILayout.BeginScrollView(rightScrollPos);

        GUILayout.Space(10);
        GUILayout.Label($"ÉDITION : {sharedEntry.Key}", EditorStyles.largeLabel);
        GUILayout.Label($"ID Unique : {sharedEntry.Id}", EditorStyles.miniLabel);
        GUILayout.Space(20);

        // --- ZONE REFERENCE ---
        GUILayout.Label($"Référence ({refLocale.name})", EditorStyles.boldLabel);
        string refText = "";
        if (refTable != null)
        {
            var refE = refTable.GetEntry(selectedKeyId);
            refText = refE != null ? refE.Value : "";
        }

        bool isEditingRef = (targetLocale == refLocale);
        GUI.enabled = isEditingRef;
        string newRefText = EditorGUILayout.TextArea(refText, GUILayout.Height(60));
        GUI.enabled = true;

        if (isEditingRef && newRefText != refText)
        {
            if (refTable.GetEntry(selectedKeyId) == null) refTable.AddEntry(selectedKeyId, newRefText);
            else refTable.GetEntry(selectedKeyId).Value = newRefText;
            EditorUtility.SetDirty(refTable);
        }

        GUILayout.Space(20);
        DrawSeparator();
        GUILayout.Space(20);

        // --- ZONE TRADUCTION CIBLE ---
        if (!isEditingRef)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Traduction ({targetLocale.name})", EditorStyles.boldLabel);

            bool existsInTable = false;
            if (targetTable != null) existsInTable = (targetTable.GetEntry(selectedKeyId) != null);

            bool toggle = EditorGUILayout.ToggleLeft("Valider / Activer en Jeu", existsInTable);
            GUILayout.EndHorizontal();

            if (lastEditedKeyId != selectedKeyId)
            {
                if (existsInTable && targetTable != null)
                {
                    var e = targetTable.GetEntry(selectedKeyId);
                    tempEditingText = e != null ? e.Value : "";
                }
                else
                {
                    tempEditingText = GetDraftFromBDD(sharedEntry.Key, targetLocale.Identifier.Code);
                }
                lastEditedKeyId = selectedKeyId;
            }

            string newText = EditorGUILayout.TextArea(tempEditingText, GUILayout.Height(100));

            if (newText != tempEditingText)
            {
                tempEditingText = newText;
                if (toggle && targetTable != null)
                {
                    if (existsInTable)
                    {
                        targetTable.GetEntry(selectedKeyId).Value = newText;
                        EditorUtility.SetDirty(targetTable);
                    }
                }
                else
                {
                    SaveDraftToBDD(sharedEntry.Key, targetLocale.Identifier.Code, newText);
                }
            }

            if (toggle != existsInTable)
            {
                if (toggle)
                {
                    Undo.RecordObject(targetTable, "Activate Translation");
                    targetTable.AddEntry(selectedKeyId, tempEditingText);
                    EditorUtility.SetDirty(targetTable);
                }
                else
                {
                    Undo.RecordObject(targetTable, "Deactivate Translation");
                    targetTable.RemoveEntry(selectedKeyId);
                    EditorUtility.SetDirty(targetTable);
                    SaveDraftToBDD(sharedEntry.Key, targetLocale.Identifier.Code, tempEditingText);
                }
            }

            if (!toggle)
            {
                if (string.IsNullOrEmpty(newText))
                {
                    EditorGUILayout.HelpBox($"Traduction manquante. Le jeu affichera le Français par défaut.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Traduction en brouillon (Sauvegardée dans SO {currentMode}).", MessageType.Info);
                }
            }
        }

        GUILayout.Space(30);

        // --- DANGER ---
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("Supprimer cette Clé Définitivement", GUILayout.Height(30)))
        {
            RemoveKey(selectedKeyId);
            selectedKeyId = -1;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // --- LOGIQUE METIER ---

    private void SaveDraftToBDD(string key, string langCode, string text)
    {
        if (currentMode == LocMode.Dialogues)
        {
            if (dialogueBDD == null) return;
            var entry = dialogueBDD.Entries.FirstOrDefault(x => x.key == key);
            if (entry == null)
            {
                entry = new BDD_Dialogue.DialogueEntry { key = key, translations = new List<BDD_Dialogue.TranslationData>() };
                dialogueBDD.Entries.Add(entry);
            }
            if (entry.translations == null) entry.translations = new List<BDD_Dialogue.TranslationData>();
            var trad = entry.translations.FirstOrDefault(x => x.languageCode == langCode);
            if (trad == null)
            {
                trad = new BDD_Dialogue.TranslationData { languageCode = langCode };
                entry.translations.Add(trad);
            }
            trad.text = text;
            EditorUtility.SetDirty(dialogueBDD);
        }
        else // UI MODE
        {
            if (uiBDD == null) return;
            var entry = uiBDD.Entries.FirstOrDefault(x => x.key == key);
            if (entry == null)
            {
                entry = new BDD_UI.UIEntry { key = key, translations = new List<BDD_UI.TranslationData>() };
                uiBDD.Entries.Add(entry);
            }
            if (entry.translations == null) entry.translations = new List<BDD_UI.TranslationData>();
            var trad = entry.translations.FirstOrDefault(x => x.languageCode == langCode);
            if (trad == null)
            {
                trad = new BDD_UI.TranslationData { languageCode = langCode };
                entry.translations.Add(trad);
            }
            trad.text = text;
            EditorUtility.SetDirty(uiBDD);
        }
    }

    private string GetDraftFromBDD(string key, string langCode)
    {
        if (currentMode == LocMode.Dialogues)
        {
            if (dialogueBDD == null || dialogueBDD.Entries == null) return "";
            var entry = dialogueBDD.Entries.FirstOrDefault(x => x.key == key);
            if (entry != null && entry.translations != null)
            {
                var trad = entry.translations.FirstOrDefault(x => x.languageCode == langCode);
                if (trad != null) return trad.text;
            }
        }
        else // UI MODE
        {
            if (uiBDD == null || uiBDD.Entries == null) return "";
            var entry = uiBDD.Entries.FirstOrDefault(x => x.key == key);
            if (entry != null && entry.translations != null)
            {
                var trad = entry.translations.FirstOrDefault(x => x.languageCode == langCode);
                if (trad != null) return trad.text;
            }
        }
        return "";
    }

    private void CreateNewKeyDialog()
    {
        string newKey = "New_Key_" + UnityEngine.Random.Range(100, 999);
        selectedCollection.SharedData.AddKey(newKey);
        EditorUtility.SetDirty(selectedCollection.SharedData);

        var entry = selectedCollection.SharedData.GetEntry(newKey);
        if (entry != null)
        {
            selectedKeyId = entry.Id;
            lastEditedKeyId = -1;
        }
    }

    private void CreateAndAddLocale(string lang)
    {
        LocaleIdentifier id = new LocaleIdentifier((SystemLanguage)System.Enum.Parse(typeof(SystemLanguage), lang));
        if (LocalizationEditorSettings.GetLocales().Any(x => x.Identifier == id)) return;

        var newLocale = Locale.CreateLocale(id);
        string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Localization/Locales/{lang}.asset");
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        AssetDatabase.CreateAsset(newLocale, path);
        LocalizationEditorSettings.AddLocale(newLocale);
        if (selectedCollection != null) selectedCollection.AddNewTable(id);
        AssetDatabase.SaveAssets();
    }

    private void RemoveLocale(Locale l)
    {
        if (EditorUtility.DisplayDialog("Supprimer", $"Supprimer la langue {l.name} ?", "Oui", "Non"))
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
        if (EditorUtility.DisplayDialog("Attention", "Supprimer cette clé de TOUTES les langues ?", "Oui", "Non"))
        {
            selectedCollection.SharedData.RemoveKey(id);
            EditorUtility.SetDirty(selectedCollection.SharedData);

            foreach (var locale in LocalizationEditorSettings.GetLocales())
            {
                var t = selectedCollection.GetTable(locale.Identifier) as StringTable;
                if (t != null)
                {
                    t.RemoveEntry(id);
                    EditorUtility.SetDirty(t);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }

    private void SaveToScriptableObject()
    {
        var allLocales = LocalizationEditorSettings.GetLocales();
        if (allLocales.Count == 0) return;

        // MODE DIALOGUES
        if (currentMode == LocMode.Dialogues)
        {
            if (dialogueBDD == null) return;
            Undo.RecordObject(dialogueBDD, "Save Loca Dialogues");
            dialogueBDD.Entries.Clear();

            foreach (var sharedEntry in selectedCollection.SharedData.Entries)
            {
                BDD_Dialogue.DialogueEntry newSOEntry = new BDD_Dialogue.DialogueEntry();
                newSOEntry.key = sharedEntry.Key;
                newSOEntry.translations = new List<BDD_Dialogue.TranslationData>();

                foreach (var locale in allLocales)
                {
                    string val = GetTextForSave(sharedEntry, locale);
                    newSOEntry.translations.Add(new BDD_Dialogue.TranslationData() { languageCode = locale.Identifier.Code, text = val });
                }
                dialogueBDD.Entries.Add(newSOEntry);
            }
            EditorUtility.SetDirty(dialogueBDD);
            Debug.Log($"Sauvegarde dans {dialogueBDD.name} réussie !");
        }
        // MODE UI
        else
        {
            if (uiBDD == null) return;
            Undo.RecordObject(uiBDD, "Save Loca UI");
            uiBDD.Entries.Clear();

            foreach (var sharedEntry in selectedCollection.SharedData.Entries)
            {
                BDD_UI.UIEntry newSOEntry = new BDD_UI.UIEntry();
                newSOEntry.key = sharedEntry.Key;
                newSOEntry.translations = new List<BDD_UI.TranslationData>();

                foreach (var locale in allLocales)
                {
                    string val = GetTextForSave(sharedEntry, locale);
                    newSOEntry.translations.Add(new BDD_UI.TranslationData() { languageCode = locale.Identifier.Code, text = val });
                }
                uiBDD.Entries.Add(newSOEntry);
            }
            EditorUtility.SetDirty(uiBDD);
            Debug.Log($"Sauvegarde dans {uiBDD.name} réussie !");
        }

        AssetDatabase.SaveAssets();
    }

    private string GetTextForSave(SharedTableData.SharedTableEntry sharedEntry, Locale locale)
    {
        var table = selectedCollection.GetTable(locale.Identifier) as StringTable;
        string val = "";

        if (table != null)
        {
            var entry = table.GetEntry(sharedEntry.Id);
            if (entry != null) val = entry.Value;
        }

        if (string.IsNullOrEmpty(val))
        {
            string draft = GetDraftFromBDD(sharedEntry.Key, locale.Identifier.Code);
            if (!string.IsNullOrEmpty(draft)) val = draft;
        }
        return val;
    }

    // --- KEY GENERATION LOGIC ---

    private string CalculateSmartKeyString(string content)
    {
        if (string.IsNullOrEmpty(content)) return "new_key_" + Random.Range(0, 1000);

        string cleaned = RemoveAccents(content).ToLower();
        string consonnes = Regex.Replace(cleaned, "[^a-z]|[aeiouy]", "");

        if (consonnes.Length > 10) consonnes = consonnes.Substring(0, 10);
        if (string.IsNullOrEmpty(consonnes)) consonnes = "txt";

        string baseKey = consonnes;
        string finalKey = baseKey;
        int index = 2;

        while (selectedCollection.SharedData.GetEntry(finalKey) != null)
        {
            finalKey = $"{baseKey}_{index}";
            index++;
        }

        return finalKey;
    }

    private string RemoveAccents(string text) => new string(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

    // --- SYNC CSV---

    private void SyncCSVDirectly()
    {
        if (csvFile == null) return;

        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return;

        string[] headers = lines[0].Split(',');
        Dictionary<int, Locale> columnMap = new Dictionary<int, Locale>();
        var allLocales = LocalizationEditorSettings.GetLocales();

        int frColIndex = -1;

        for (int i = 0; i < headers.Length; i++)
        {
            string h = headers[i].Trim().Replace("\"", "").Trim();
            if (string.IsNullOrEmpty(h) || h.ToUpper() == "ID" || h.ToUpper() == "KEY") continue;

            var locale = allLocales.FirstOrDefault(l =>
                l.Identifier.Code.Equals(h, System.StringComparison.OrdinalIgnoreCase) ||
                l.name.Equals(h, System.StringComparison.OrdinalIgnoreCase) ||
                (h.ToLower() == "fr" && l.Identifier.Code.ToLower().StartsWith("fr"))
            );

            if (locale != null)
            {
                columnMap.Add(i, locale);
                if (locale.Identifier.Code.ToLower().StartsWith("fr") || h.ToLower() == "fr")
                {
                    frColIndex = i;
                }
            }
        }

        if (frColIndex == -1)
        {
            Debug.LogError("Erreur : Impossible de trouver une colonne 'fr' !");
            return;
        }

        Undo.RecordObject(selectedCollection.SharedData, "Sync CSV Keys");
        bool sharedDataChanged = false;

        foreach (var line in lines.Skip(1))
        {
            string[] cells = line.Split(',');
            if (frColIndex >= cells.Length) continue;
            string frText = cells[frColIndex].Trim().Replace(";", ",");

            if (string.IsNullOrEmpty(frText)) continue;

            string keyToUse = "";
            string idealKey = CalculateSmartKeyString(frText);
            string baseKeyCandidates = Regex.Replace(idealKey, @"_\d+$", "");

            var frTable = selectedCollection.GetTable(columnMap[frColIndex].Identifier) as StringTable;
            bool foundExistingKey = false;

            foreach (var entry in selectedCollection.SharedData.Entries)
            {
                if (entry.Key.StartsWith(baseKeyCandidates))
                {
                    var existingFrEntry = frTable.GetEntry(entry.Id);
                    if (existingFrEntry != null && existingFrEntry.Value == frText)
                    {
                        keyToUse = entry.Key;
                        foundExistingKey = true;
                        break;
                    }
                }
            }

            if (!foundExistingKey)
            {
                keyToUse = idealKey;
                if (selectedCollection.SharedData.GetEntry(keyToUse) == null)
                {
                    selectedCollection.SharedData.AddKey(keyToUse);
                    sharedDataChanged = true;
                }
            }

            var sharedEntry = selectedCollection.SharedData.GetEntry(keyToUse);
            if (sharedEntry == null) continue;

            foreach (var col in columnMap)
            {
                if (col.Key >= cells.Length) continue;

                var table = selectedCollection.GetTable(col.Value.Identifier) as StringTable;
                if (table != null)
                {
                    string textValue = cells[col.Key].Trim().Replace(";", ",");

                    if (!string.IsNullOrEmpty(textValue))
                    {
                        var e = table.GetEntry(sharedEntry.Id);
                        if (e == null)
                        {
                            table.AddEntry(sharedEntry.Id, textValue);
                            EditorUtility.SetDirty(table);
                        }
                        else if (e.Value != textValue)
                        {
                            e.Value = textValue;
                            EditorUtility.SetDirty(table);
                        }
                    }
                }
            }
        }

        if (sharedDataChanged)
        {
            EditorUtility.SetDirty(selectedCollection.SharedData);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Sync CSV Terminée avec succès !");
    }

    private void ExportToCSV()
    {
        var allLocales = LocalizationEditorSettings.GetLocales();
        if (allLocales == null || allLocales.Count == 0) return;

        string path = EditorUtility.SaveFilePanel("Exporter la table en CSV", "", "Export.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        StringBuilder sb = new StringBuilder();
        List<string> header = new List<string> { "ID", "Key" };
        foreach (var locale in allLocales) header.Add(locale.Identifier.Code.ToUpper());
        sb.AppendLine(string.Join(",", header));

        foreach (var sharedEntry in selectedCollection.SharedData.Entries)
        {
            List<string> row = new List<string>();
            row.Add(sharedEntry.Id.ToString());
            row.Add(sharedEntry.Key);

            foreach (var locale in allLocales)
            {
                var table = selectedCollection.GetTable(locale.Identifier) as StringTable;
                var entry = table?.GetEntry(sharedEntry.Id);
                string val = entry != null ? entry.Value.Replace(",", ";").Replace("\n", " ") : "";
                row.Add(val);
            }
            sb.AppendLine(string.Join(",", row));
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("Export CSV Terminé");
    }

    private void DrawSeparator()
    {
        GUILayout.Space(5);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
        GUILayout.Space(5);
    }
}
