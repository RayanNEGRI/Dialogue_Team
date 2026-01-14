using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.GraphNode.NodeBasedDialogueSystem_master.NodeBasedDialogueSystem_master.com.subtegral.dialoguesystem.Editor.Graph
{

    public enum Mode 
    {
        Panel,
        Bubble,
        Popup
    }

    internal class WindowMode : ScriptableObject
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
            //TODO
        }

        public void Bubble()
        {
            //TODO
        }

        public void Popup() 
        {
            //TODO
        }
    }
}
