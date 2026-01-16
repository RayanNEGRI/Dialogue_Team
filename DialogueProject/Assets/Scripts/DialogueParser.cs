using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Subtegral.DialogueSystem.DataContainers;

namespace Subtegral.DialogueSystem.Runtime
{
    public class DialogueParser : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string tableName = "Dialogues";
        [SerializeField] private string uiTableName = "UI";

        [Header("References")]
        [SerializeField] private DialogueContainer dialogue;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Button choicePrefab;
        [SerializeField] private Transform buttonContainer;

        // --- AJOUT : Paramètres de taille de police ---
        [Header("Text Auto-Sizing")]
        [SerializeField] private float minFontSize = 20f; // Taille minimum (pour ne pas devenir illisible)
        [SerializeField] private float maxFontSize = 72f; // Taille maximum (pour les textes courts)

        private string _currentGuid;

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale locale)
        {
            if (dialogue == null) return;
            if (string.IsNullOrEmpty(_currentGuid)) return;

            var node = dialogue.DialogueNodeData.FirstOrDefault(n => n != null && n.NodeGUID == _currentGuid);
            if (node != null) ShowNode(node);
        }

        private void Start()
        {
            if (dialogue == null)
            {
                Debug.LogError("[DialogueParser] DialogueContainer not assigned.");
                return;
            }

            // --- MODIFICATION ICI : Configuration Auto-Size ---
            if (dialogueText != null)
            {
                // Active l'ajustement automatique de la taille
                dialogueText.enableAutoSizing = true;
                // Définit la limite basse (pour que ça reste lisible)
                dialogueText.fontSizeMin = minFontSize;
                // Définit la limite haute (pour les titres ou mots seuls)
                dialogueText.fontSizeMax = maxFontSize;
                // S'assure que le texte revient à la ligne
                dialogueText.enableWordWrapping = true;
            }
            // --------------------------------------------------

            var startLink = GetStartLink(dialogue);
            if (startLink == null || string.IsNullOrEmpty(startLink.TargetNodeGUID))
            {
                Debug.LogError("[DialogueParser] No links from START node.");
                return;
            }

            Proceed(startLink.TargetNodeGUID);
        }

        private static NodeLinkData GetStartLink(DialogueContainer container)
        {
            if (container == null || container.NodeLinks == null) return null;

            var byPortId = container.NodeLinks.FirstOrDefault(l => l != null && string.Equals(l.PortId, "start", StringComparison.OrdinalIgnoreCase));
            if (byPortId != null) return byPortId;

            var nodeGuids = new HashSet<string>((container.DialogueNodeData ?? new List<DialogueNodeData>()).Where(n => n != null).Select(n => n.NodeGUID));
            var maybeStart = container.NodeLinks.FirstOrDefault(l => l != null && !string.IsNullOrEmpty(l.BaseNodeGUID) && !nodeGuids.Contains(l.BaseNodeGUID));
            return maybeStart ?? container.NodeLinks.FirstOrDefault(l => l != null);
        }

        private void Proceed(string guid)
        {
            var node = dialogue.DialogueNodeData.FirstOrDefault(n => n != null && n.NodeGUID == guid);
            if (node == null)
            {
                Debug.LogError($"[DialogueParser] Node not found: {guid}");
                return;
            }

            _currentGuid = guid;
            ShowNode(node);
        }

        private void ShowNode(DialogueNodeData node)
        {
            if (node == null) return;

            if (node.NodeType == DialogueNodeType.Branch)
            {
                var links = (dialogue.NodeLinks ?? new List<NodeLinkData>())
                    .Where(l => l != null && l.BaseNodeGUID == node.NodeGUID)
                    .ToList();

                var trueLink = links.FirstOrDefault(l => string.Equals(l.PortId, "true", StringComparison.OrdinalIgnoreCase));
                var falseLink = links.FirstOrDefault(l => string.Equals(l.PortId, "false", StringComparison.OrdinalIgnoreCase));

                var branchResult = EvaluateCondition(node.ConditionExpression);
                var chosen = branchResult ? trueLink : falseLink;

                if (chosen == null || string.IsNullOrEmpty(chosen.TargetNodeGUID))
                {
                    EndDialogue();
                    return;
                }

                Proceed(chosen.TargetNodeGUID);
                return;
            }

            if (node.NodeType == DialogueNodeType.End)
            {
                EndDialogue();
                return;
            }

            if (buttonContainer != null) buttonContainer.gameObject.SetActive(true);

            if (dialogueText != null)
                SetLocalizedText(tableName, node.DialogueText, dialogueText);

            ClearButtons();

            var choices = (dialogue.NodeLinks ?? new List<NodeLinkData>())
                .Where(l => l != null && l.BaseNodeGUID == node.NodeGUID)
                .ToList();

            var anyShown = false;

            foreach (var choice in choices)
            {
                if (string.IsNullOrEmpty(choice.TargetNodeGUID)) continue;
                if (!EvaluateCondition(choice.ConditionExpression)) continue;
                CreateChoiceButton(choice);
                anyShown = true;
            }

            if (!anyShown)
                EndDialogue();
        }

        private void EndDialogue()
        {
            ClearButtons();
            if (buttonContainer != null) buttonContainer.gameObject.SetActive(false);
            _currentGuid = null;
        }

        private void ClearButtons()
        {
            if (buttonContainer == null) return;
            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
                Destroy(buttonContainer.GetChild(i).gameObject);
        }

        private void CreateChoiceButton(NodeLinkData choice)
        {
            if (choicePrefab == null || buttonContainer == null) return;

            var btn = Instantiate(choicePrefab, buttonContainer);

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            var key = string.IsNullOrWhiteSpace(choice.PortLabel) ? "Choix" : choice.PortLabel;

            if (tmp != null)
            {
                SetLocalizedText(uiTableName, key, tmp);
            }
            else
            {
                var legacy = btn.GetComponentInChildren<Text>();
                if (legacy != null) SetLocalizedTextLegacy(uiTableName, key, legacy);
            }

            var target = choice.TargetNodeGUID;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => Proceed(target));
        }

        private void SetLocalizedText(string table, string key, TextMeshProUGUI targetText)
        {
            if (targetText == null) return;
            key = (key ?? "").Trim();

            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
            if (op.IsDone)
            {
                targetText.text = ProcessProperties(op.Result);
            }
            else
            {
                op.Completed += handle =>
                {
                    if (targetText != null)
                        targetText.text = ProcessProperties(handle.Result);
                };
            }
        }

        private void SetLocalizedTextLegacy(string table, string key, Text targetText)
        {
            if (targetText == null) return;
            key = (key ?? "").Trim();

            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
            if (op.IsDone) targetText.text = ProcessProperties(op.Result);
            else op.Completed += handle => { if (targetText != null) targetText.text = ProcessProperties(handle.Result); };
        }

        private string ProcessProperties(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (dialogue == null || dialogue.ExposedProperties == null) return text;

            foreach (var p in dialogue.ExposedProperties)
            {
                if (p == null) continue;
                var name = p.PropertyName ?? "";
                var value = p.PropertyValue ?? "";
                if (string.IsNullOrEmpty(name)) continue;
                text = text.Replace($"[{name}]", value);
            }
            return text;
        }

        private bool EvaluateCondition(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;

            var andParts = expression.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in andParts)
            {
                if (!EvaluateAtom(part.Trim())) return false;
            }
            return true;
        }

        private bool EvaluateAtom(string atom)
        {
            if (string.IsNullOrWhiteSpace(atom)) return true;

            atom = atom.Trim();

            string[] operators = { ">=", "<=", "==", "!=", ">", "<" };
            foreach (var op in operators)
            {
                var idx = atom.IndexOf(op, StringComparison.Ordinal);
                if (idx < 0) continue;

                var left = atom.Substring(0, idx).Trim();
                var right = atom.Substring(idx + op.Length).Trim();
                var leftValue = ResolveValue(left);
                var rightValue = ResolveValue(right);

                if (leftValue == null || rightValue == null) return false;
                return Compare(leftValue, rightValue, op);
            }

            if (bool.TryParse(atom, out var b)) return b;

            var v = ResolveValue(atom);
            if (v is bool vb) return vb;
            if (v is int vi) return vi != 0;
            if (v is string vs) return !string.IsNullOrEmpty(vs);
            return false;
        }

        private object ResolveValue(string token)
        {
            token = (token ?? "").Trim();
            if (string.IsNullOrEmpty(token)) return null;

            if (token.StartsWith("[") && token.EndsWith("]") && token.Length >= 2)
            {
                var varName = token.Substring(1, token.Length - 2).Trim();
                if (string.IsNullOrEmpty(varName)) return null;

                var prop = (dialogue?.ExposedProperties ?? new List<ExposedProperty>()).FirstOrDefault(p => p != null && p.PropertyName == varName);
                if (prop == null) return null;

                var raw = prop.PropertyValue ?? "";
                if (int.TryParse(raw, out var i)) return i;
                if (bool.TryParse(raw, out var b)) return b;
                return raw;
            }

            if (int.TryParse(token, out var intVal)) return intVal;
            if (bool.TryParse(token, out var boolVal)) return boolVal;

            if (token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2)
                return token.Substring(1, token.Length - 2);

            return token;
        }

        private bool Compare(object left, object right, string op)
        {
            if (left is int li && right is int ri)
            {
                return op switch
                {
                    ">=" => li >= ri,
                    "<=" => li <= ri,
                    ">" => li > ri,
                    "<" => li < ri,
                    "==" => li == ri,
                    "!=" => li != ri,
                    _ => false
                };
            }

            if (left is bool lb && right is bool rb)
            {
                return op switch
                {
                    "==" => lb == rb,
                    "!=" => lb != rb,
                    _ => false
                };
            }

            if (left is string ls && right is string rs)
            {
                return op switch
                {
                    "==" => string.Equals(ls, rs, StringComparison.Ordinal),
                    "!=" => !string.Equals(ls, rs, StringComparison.Ordinal),
                    _ => false
                };
            }

            return op switch
            {
                "==" => Equals(left, right),
                "!=" => !Equals(left, right),
                _ => false
            };
        }
    }
}
