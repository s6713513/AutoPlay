using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using System.Collections.Generic;

namespace AutoPlay
{
    public class KeyCodeEvent
    {
        private static KeyCodeEvent instance;

        private delegate void KeyEventHandler(KeyCode e);
        private static event KeyEventHandler KeyEvent;

        public static bool maxDamage = false;
        public static bool cheats = false;
        public static bool modify = false;       
        public static bool pickUp = false;
        public static bool clearScreen = false;
        public static bool refreshMonster = false;
        public static bool kick = false;
        public static bool kill = false;
        private static List<NetworkUser> users;

        private static void OnKeyPressed(KeyCode e)
        {
            KeyEventHandler keyEvent = KeyEvent;
            if (keyEvent != null)
            {
                keyEvent(e);
            }
        }

        public static KeyCodeEvent GetInstance()
        {
            return KeyCodeEvent.instance;
        }
        public KeyCodeEvent()
        {
            KeyCodeEvent.instance = this;
            users = new List<NetworkUser>();
            On.InterpolationController.Update += Update;            
            KeyEvent += new KeyCodeEvent.KeyEventHandler(KeyEvent_New);
        }

        private static void Update(On.InterpolationController.orig_Update orig, global::InterpolationController self)
        {            
            Event current = Event.current;
            bool flag2 = current != null;
            if (flag2)
            {
                bool isKey = current.isKey;
                if (isKey)
                {
                    bool flag3 = current.type == EventType.KeyDown;
                    if (flag3)
                    {                       
                        OnKeyPressed(current.keyCode);
                    }
                }
            }            
            ClientCheat.GetInstance().AutoPlay();
            orig(self);
        }

        private static void KeyEvent_New(KeyCode e)
        {

            bool flag = KeyCode.F1 == e;
            bool flag2 = KeyCode.F2 == e;

            if (flag)
            {
                cheats = !cheats;
                if (cheats)
                {
                    ClientCheat.GetInstance().Enable();
                }
                else
                {
                    ClientCheat.GetInstance().Disable();
                }
            }
            if (flag2)
            {
                //maxDamage = !maxDamage;
                Stage.instance.BeginAdvanceStage(Run.instance.nextStageScene);
            }

            if (KeyCode.F3 == e)
            {
                ClientCheat.GetInstance().KillAllPlayer();
            }

            if (KeyCode.F4 == e)
            {
                modify = !modify;
                if (modify)
                {
                    ClientCheat.GetInstance().ModifyCharacterAttr();
                }
                else
                {
                    ClientCheat.GetInstance().ResetCharacterAttr();
                }
            }

            if (KeyCode.F5 == e)
            {
                pickUp = !pickUp;               
                                       
            }

            if (KeyCode.F6 == e)
            {
                clearScreen = !clearScreen;
            }
            

            if (KeyCode.F8 == e)
            {
                ClientCheat.GetInstance().God_Mode();
            }

            if (KeyCode.F9 == e)
            {
                ClientCheat.GetInstance().MoveToAnywhere();
            }
           
            if((NetworkServer.active && KeyCode.F10 == e) || KeyCode.F11 == e)
            {
                if(KeyCode.F10 == e)
                {
                    kick = true;
                    kill = false;
                }
                else if(KeyCode.F11 == e)
                {
                    kill = true;
                    kick = false;
                }
                if (kill || kick)
                {
                    PrintPlayerName();
                }
            }           



            if (users.Count > 0 && (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad3)))
            {
                int position = 0;
                
                if(Input.GetKeyDown(KeyCode.Keypad2))
                {
                    position = 1;
                }
                else if (Input.GetKeyDown(KeyCode.Keypad3))
                {
                    position = 2;
                }
                if(position > users.Count - 1)
                {
                    return;
                }
                if (kick)
                {
                    ClientCheat.GetInstance().Kick(users[position]);
                }
                if(kill)
                {
                    ClientCheat.GetInstance().KillChoosePlayer(users[position]);
                }
            }
        }       

        private static void PrintPlayerName()
        {
            users.Clear();
            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
            {
                if (player.networkUser && !player.networkUser.isLocalPlayer)
                {
                    users.Add(player.networkUser);
                    Chat.AddMessage(users.Count.ToString() + "." + player.networkUser.userName);
                }
            }
        }
    }
   
}
