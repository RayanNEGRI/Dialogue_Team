using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Subtegral.DialogueSystem.DataContainers;
// Ces deux lignes sont OBLIGATOIRES pour le nouveau système
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Subtegral.DialogueSystem.Runtime
{
    public class DialogueParser : MonoBehaviour
    {
        [Header("Settings")]
        // Mets ici EXACTEMENT le nom de la table que tu as créée à l'Etape 1 (ex: "Dialogues")
        [SerializeField] private string tableName = "Dialogues";

        [Header("References")]
        [SerializeField] private DialogueContainer dialogue;
        // On n'a plus besoin de "BDD_Dialogue database" ici, Unity s'en occupe !

        [Header("UI Text")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private TextMeshProUGUI characterNameText;

        [Header("UI Visuals")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private AudioSource audioSource;

        [Header("Choices")]
        [SerializeField] private Button choicePrefab;
        [SerializeField] private Transform buttonContainer;

        private void Start()
        {
            var narrativeData = dialogue.NodeLinks.First();
            ProceedToNarrative(narrativeData.TargetNodeGUID);
        }

        // --- NOUVELLE FONCTION POUR CHARGER LE TEXTE ---
        private void UpdateText(string key)
        {
            // On demande à Unity de chercher la clé dans la table "Dialogues"
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, key);

            if (op.IsDone)
            {
                dialogueText.text = ProcessProperties(op.Result);
            }
            else
            {
                // Si ça charge encore, on attend le résultat
                op.Completed += (handle) =>
                {
                    dialogueText.text = ProcessProperties(handle.Result);
                };
            }
        }

        private void ProceedToNarrative(string narrativeDataGUID)
        {
            var nodeData = dialogue.DialogueNodeData.Find(x => x.NodeGUID == narrativeDataGUID);
            var rawKey = nodeData.DialogueText.Trim(); // Ta clé (ex: "bjr")

            // 1. On appelle la traduction Unity
            UpdateText(rawKey);

            // 2. Gestion du Speaker (inchangé)
            //var speakerProfile = nodeData.Speaker as BDD_Speaker;
            //if (speakerProfile != null)
            //{
            //    if (characterNameText != null)
            //    {
            //        characterNameText.text = speakerProfile.CharacterName;
            //        characterNameText.color = speakerProfile.NameColor;
            //    }

            //    var currentMood = speakerProfile.Moods.Find(m => m.MoodName == nodeData.MoodKey);
            //    if (!string.IsNullOrEmpty(currentMood.MoodName))
            //    {
            //        if (portraitImage != null && currentMood.Portrait != null)
            //        {
            //            portraitImage.sprite = currentMood.Portrait;
            //            portraitImage.gameObject.SetActive(true);
            //        }
            //        if (audioSource != null && currentMood.VoiceSample != null)
            //        {
            //            audioSource.Stop();
            //            audioSource.PlayOneShot(currentMood.VoiceSample);
            //        }
            //    }
            //}
            //else
            //{
            //    if (characterNameText != null) characterNameText.text = "";
            //    if (portraitImage != null) portraitImage.gameObject.SetActive(false);
            //}

            // 3. Gestion des Boutons
            var choices = dialogue.NodeLinks.Where(x => x.BaseNodeGUID == narrativeDataGUID);
            var buttons = buttonContainer.GetComponentsInChildren<Button>();
            for (int i = 0; i < buttons.Length; i++) Destroy(buttons[i].gameObject);

            foreach (var choice in choices)
            {
                var button = Instantiate(choicePrefab, buttonContainer);
                // Note : Pour l'instant les choix affichent la clé brute.
                button.GetComponentInChildren<Text>().text = ProcessProperties(choice.PortName);
                button.onClick.AddListener(() => ProceedToNarrative(choice.TargetNodeGUID));
            }
        }

        private string ProcessProperties(string text)
        {
            foreach (var exposedProperty in dialogue.ExposedProperties)
            {
                text = text.Replace($"[{exposedProperty.PropertyName}]", exposedProperty.PropertyValue);
            }
            return text;
        }
    }
}
