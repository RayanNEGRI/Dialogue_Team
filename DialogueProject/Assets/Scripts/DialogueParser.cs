using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Subtegral.DialogueSystem.DataContainers;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings; // Nécessaire pour détecter le changement de langue

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

        // --- NOUVEAU : On stocke l'ID du nœud actuel pour pouvoir rafraîchir ---
        private string currentNodeGUID;

        private void OnEnable()
        {
            // On s'abonne à l'événement de changement de langue
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDisable()
        {
            // On se désabonne toujours pour éviter les erreurs de mémoire
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        // Cette fonction est appelée automatiquement par Unity quand tu changes la langue
        private void OnLocaleChanged(Locale locale)
        {
            // Si on est déjà en train d'afficher un dialogue, on le rafraîchit
            if (!string.IsNullOrEmpty(currentNodeGUID))
            {
                ProceedToNarrative(currentNodeGUID);
            }
        }

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
            // --- NOUVEAU : On mémorise où on est ---
            currentNodeGUID = narrativeDataGUID;

            var nodeData = dialogue.DialogueNodeData.Find(x => x.NodeGUID == narrativeDataGUID);

            // Sécurité si le nœud n'est pas trouvé
            if (nodeData == null) return;

            var rawKey = nodeData.DialogueText.Trim(); // Ta clé (ex: "bjr")

            // 1. On appelle la traduction Unity
            UpdateText(rawKey);

            // 2. Gestion du Speaker (inchangé - commenté dans ton code original)
            // ... (ton code speaker) ...

            // 3. Gestion des Boutons
            // On nettoie les anciens boutons
            var buttons = buttonContainer.GetComponentsInChildren<Button>();
            for (int i = 0; i < buttons.Length; i++) Destroy(buttons[i].gameObject);

            var choices = dialogue.NodeLinks.Where(x => x.BaseNodeGUID == narrativeDataGUID);

            foreach (var choice in choices)
            {
                var button = Instantiate(choicePrefab, buttonContainer);

                // --- TRADUCTION DES BOUTONS ---
                // Ici, "choice.PortName" est ta clé (ex: "oui_btn").
                // On demande la trad direct pour le bouton aussi.
                var btnKey = choice.PortName.Trim();

                // Petit hack rapide pour traduire le bouton : on lance une requête async pour le label du bouton
                var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, btnKey);
                var btnText = button.GetComponentInChildren<Text>(); // Ou TextMeshProUGUI si tu as upgrade tes boutons

                if (op.IsDone)
                {
                    btnText.text = ProcessProperties(op.Result);
                }
                else
                {
                    op.Completed += (handle) =>
                    {
                        if (btnText != null) btnText.text = ProcessProperties(handle.Result);
                    };
                }

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
