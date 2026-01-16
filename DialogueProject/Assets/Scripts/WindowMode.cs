using Subtegral.DialogueSystem.DataContainers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;

/*namespace Assets.GraphNode.NodeBasedDialogueSystem_master.NodeBasedDialogueSystem_master.com.subtegral.dialoguesystem.Editor.Graph*/
/*{*/

public enum Mode
{
    Panel,
    Bubble,
    Popup
}

[CreateAssetMenu(fileName = "WindowMode", menuName = "ScriptableObjects/WMode")]
public class WindowMode : ScriptableObject
{
    [SerializeField] private GameObject PanelPrefab;
    [SerializeField] private GameObject BubblePrefab;
    [SerializeField] private GameObject PopupPrefab;
    [SerializeField] private TextMeshProUGUI dialogueText;


    public Mode mode;

    public GameObject InstantiateWindow(Mode mode, Transform parent, string dialogueText) 
    {
        GameObject prefabToSpawn = null;

        switch (mode)
        {
            case Mode.Panel:
                prefabToSpawn = PanelPrefab;
                parent.GetComponentInChildren<TextMeshProUGUI>(true);

                Panel();
                break;

            case Mode.Popup:
                prefabToSpawn = PopupPrefab;
                Popup();
                break;

            case Mode.Bubble:
                prefabToSpawn = BubblePrefab;
                Bubble();
                break;

            default:
                Console.WriteLine("No mode find please fix it");
                break;
        }

        if (prefabToSpawn == null) 
        {
            Debug.LogError($"Prefab manquant pour {mode}");
            return null;
        }

        GameObject instance = Instantiate(prefabToSpawn, parent);
        instance.SetActive(true);


        TextMeshProUGUI tmp = instance.GetComponentInChildren<TextMeshProUGUI>();

         if (tmp != null)
        {
            tmp.text = dialogueText;
        }
        else
        {
            Debug.LogWarning("Aucun TextMeshProUGUI trouvé dans le prefab !");
        }

        return instance;
    }

    public void SwitchWindowMode(Mode mode)
    {
    }

    public void Panel()
    {

        Debug.Log("Panel");
        
    }

    public void Bubble()
    {
        Debug.Log("Bubble");
    }

    public void Popup()
    {
        Debug.Log("Popup");
    }
}
/*}*/
