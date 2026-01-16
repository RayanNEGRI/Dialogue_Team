using System;
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
        [SerializeField] private string tableName = "Dialogues"; // Table pour les textes narratifs
        [SerializeField] private string uiTableName = "UI";      // Table pour les boutons (Choix)

        [Header("References")]
        [SerializeField] private DialogueContainer dialogue;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Button choicePrefab;
        [SerializeField] private Transform buttonContainer;

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
            // Si on est en cours de dialogue, on rafraîchit le nœud actuel
            if (!string.IsNullOrEmpty(_currentGuid))
            {
                // On relance l'affichage du nœud
                var node = dialogue.DialogueNodeData.FirstOrDefault(n => n.NodeGUID == _currentGuid);
                if (node != null) ShowNode(node);
            }
        }

        private void Start()
        {
            if (dialogue == null)
            {
                Debug.LogError("[DialogueParser] DialogueContainer not assigned.");
                return;
            }

            var start = dialogue.NodeLinks.FirstOrDefault();
            if (start == null || string.IsNullOrEmpty(start.TargetNodeGUID))
            {
                Debug.LogError("[DialogueParser] No links from START node.");
                return;
            }

            Proceed(start.TargetNodeGUID);
        }

        private void Proceed(string guid)
        {
            var node = dialogue.DialogueNodeData.FirstOrDefault(n => n.NodeGUID == guid);
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
            if (node.NodeType == DialogueNodeType.Branch)
            {
                var links = dialogue.NodeLinks.Where(l => l.BaseNodeGUID == node.NodeGUID).ToList();
                var trueLink = links.FirstOrDefault(l => string.Equals(l.PortId, "true", StringComparison.OrdinalIgnoreCase));
                var falseLink = links.FirstOrDefault(l => string.Equals(l.PortId, "false", StringComparison.OrdinalIgnoreCase));

                var branchResult = EvaluateCondition(node.ConditionExpression);
                var chosen = branchResult ? trueLink : falseLink;

                if (chosen == null || string.IsNullOrEmpty(chosen.TargetNodeGUID))
                {
                    Debug.LogError("[DialogueParser] Branch missing True/False links.");
                    return;
                }

                Proceed(chosen.TargetNodeGUID);
                return;
            }

            if (node.NodeType == DialogueNodeType.End)
            {
                buttonContainer.gameObject.SetActive(false);
                dialogueText.text = "";
                return;
            }

            if (dialogueText != null)
            {
                // --- MODIFICATION : On utilise 'tableName' pour le texte principal ---
                SetLocalizedText(tableName, node.DialogueText, dialogueText);
            }

            foreach (Transform child in buttonContainer)
                Destroy(child.gameObject);

            var choices = dialogue.NodeLinks.Where(l => l.BaseNodeGUID == node.NodeGUID).ToList();

            foreach (var choice in choices)
            {
                if (string.IsNullOrEmpty(choice.TargetNodeGUID)) continue;

                if (!EvaluateCondition(choice.ConditionExpression)) continue;

                var btn = Instantiate(choicePrefab, buttonContainer);

                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();

                string key = string.IsNullOrWhiteSpace(choice.PortLabel) ? "Choix" : choice.PortLabel;

                if (tmp != null)
                {
                    // uitable pour les buttons
                    SetLocalizedText(uiTableName, key, tmp);
                }
                else
                {
                    var legacy = btn.GetComponentInChildren<Text>();
                    
                    if (legacy != null) SetLocalizedTextLegacy(uiTableName, key, legacy);
                }

                var target = choice.TargetNodeGUID;
                btn.onClick.AddListener(() => Proceed(target));
            }
        }

        private void SetLocalizedText(string table, string key, TextMeshProUGUI targetText)
        {
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key.Trim());

            if (op.IsDone)
            {
                targetText.text = ProcessProperties(op.Result);
            }
            else
            {
                op.Completed += (handle) =>
                {
                    if (targetText != null)
                        targetText.text = ProcessProperties(handle.Result);
                };
            }
        }

        private void SetLocalizedTextLegacy(string table, string key, Text targetText)
        {
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key.Trim());
            if (op.IsDone) targetText.text = ProcessProperties(op.Result);
            else op.Completed += (handle) => { if (targetText != null) targetText.text = ProcessProperties(handle.Result); };
        }

        private string ProcessProperties(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (var p in dialogue.ExposedProperties)
                text = text.Replace($"[{p.PropertyName}]", p.PropertyValue);
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
            return false;
        }

        private object ResolveValue(string token)
        {
            if (token.StartsWith("[") && token.EndsWith("]"))
            {
                var varName = token.Substring(1, token.Length - 2);
                var prop = dialogue.ExposedProperties.FirstOrDefault(p => p.PropertyName == varName);
                if (prop == null) return null;
                if (int.TryParse(prop.PropertyValue, out var i)) return i;
                if (bool.TryParse(prop.PropertyValue, out var b)) return b;
                return prop.PropertyValue;
            }
            if (int.TryParse(token, out var intVal)) return intVal;
            if (bool.TryParse(token, out var boolVal)) return boolVal;
            if (token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2) return token.Substring(1, token.Length - 2);
            return token;
        }

        private bool Compare(object left, object right, string op)
        {
            if (left is int li && right is int ri)
            {
                return op switch { ">=" => li >= ri, "<=" => li <= ri, ">" => li > ri, "<" => li < ri, "==" => li == ri, "!=" => li != ri, _ => false };
            }
            if (left is bool lb && right is bool rb)
            {
                return op switch { "==" => lb == rb, "!=" => lb != rb, _ => false };
            }
            return op switch { "==" => Equals(left, right), "!=" => !Equals(left, right), _ => false };
        }
    }
}
