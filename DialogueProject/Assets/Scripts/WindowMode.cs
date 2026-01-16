//using Codice.Client.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

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
        public Mode mode;

        public void SwitchWindowMode(Mode mode)
        {

            switch (mode) 
            {
                case Mode.Panel:
                    Panel();
                    break;

                case Mode.Popup:
                    Popup();
                    break;

                case Mode.Bubble:
                     Bubble();
                    break;

                default: Console.WriteLine("No mode find please fix it");
                        break;
            }
            
        }

        public void Panel()
        {

            Debug.Log("Panel");
            TextMeshProUGUI[] TMP = GameObject.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (TextMeshProUGUI tmp in TMP) 
            {
                //Console.WriteLine(tmp.name);
            }
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
