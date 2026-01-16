using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Subtegral.DialogueSystem.DataContainers; // Nécessaire pour BDD_Dialogue et l'Enum Language

namespace Subtegral.DialogueSystem.Runtime
{
    public class DialogueParser : MonoBehaviour
    {
        [Header("Settings")]
        /*public Language currentLanguage = Language.French;*/

        [Header("References")]
        [SerializeField] private DialogueContainer dialogue;
        [SerializeField] private BDD_Dialogue database;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Button choicePrefab;
        [SerializeField] private Transform buttonContainer;

        [Header("Window Mode")]
        [SerializeField] WindowMode WindowMode;
        [SerializeField] private Mode mode;

        private void Start()
        {
            var narrativeData = dialogue.NodeLinks.First();
            ProceedToNarrative(narrativeData.TargetNodeGUID);
            WindowMode.SwitchWindowMode(mode);
            
        }

        private void ProceedToNarrative(string narrativeDataGUID)
        {
            var rawKey = dialogue.DialogueNodeData.Find(x => x.NodeGUID == narrativeDataGUID).DialogueText;

            string translatedText = rawKey;

            if (database != null)
            {
                /*translatedText = database.GetTextByKey(rawKey.Trim(), currentLanguage);*/
            }
            else
            {
                Debug.LogWarning("Attention : Aucune BDD_Dialogue assignée dans l'inspecteur du DialogueParser !");
            }

            dialogueText.text = ProcessProperties(translatedText);

            var choices = dialogue.NodeLinks.Where(x => x.BaseNodeGUID == narrativeDataGUID);
            var buttons = buttonContainer.GetComponentsInChildren<Button>();
            for (int i = 0; i < buttons.Length; i++)
            {
                Destroy(buttons[i].gameObject);
            }

            foreach (var choice in choices)
            {
                var button = Instantiate(choicePrefab, buttonContainer);
     
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