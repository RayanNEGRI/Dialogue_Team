using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Subtegral.DialogueSystem.DataContainers;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Subtegral.DialogueSystem.Runtime
{
    public class DialogueParser : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string tableName = "Dialogues";

        [Header("References")]
        [SerializeField] private DialogueContainer dialogue;

        [Header("UI Text")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        //[SerializeField] private TextMeshProUGUI characterNameText;

        [Header("UI Visuals")]
        //[SerializeField] private Image portraitImage;
        //[SerializeField] private AudioSource audioSource;

        [Header("Choices")]
        [SerializeField] private Button choicePrefab;
        [SerializeField] private Transform buttonContainer;

        private void Start()
        {
            var narrativeData = dialogue.NodeLinks.First();
            ProceedToNarrative(narrativeData.TargetNodeGUID);
        }

        private void UpdateText(string key)
        {
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, key);

            if (op.IsDone)
            {
                dialogueText.text = ProcessProperties(op.Result);
            }
            else
            {
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
