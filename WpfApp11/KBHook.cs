using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WpfApp11
{
    class KBHook
    {
        private static int _hookHandle = 0;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern bool UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern short GetKeyState(int nVirtKey);

        public delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public const int WH_KEYBOARD_LL = 13;
        public delegate void OnHotkeyDownEventHandeler(); 
        public delegate void OnHotkeyUpHandeler();
        public delegate void OnSetKeyHandeler();
        public event OnHotkeyDownEventHandeler OnHotKeyDown = null;   
        public event OnHotkeyDownEventHandeler OnHotKeyUp = null;
        public event OnSetKeyHandeler OnSetKey = null;
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
        public static HookProc hookCallback = null;
        public Keys hotkey;
        private bool SetHotKeyMode=false;
        public bool Active = false;
        private bool IsPressed = false;
        private int CountDown = 500;

        public KBHook(Keys key)
        {
            hotkey = key;
            GC.KeepAlive(hookCallback);
            SetHook();
        }

        public KBHook()
        {
            SetHotKeyMode = true;
            GC.KeepAlive(hookCallback);
            SetHook();
        }

        public void SwitchToSetMode()
        {
            SetHotKeyMode = true;
        }

        public void SwitchToHotKeyMode()
        {
            SetHotKeyMode = false;
        }

        private void SetHook()
        {
            // Set system-wide hook.
            hookCallback = KbHookProc;
            _hookHandle = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                hookCallback,
                (IntPtr)0,
                0);
        }

        private int KbHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = (KbLLHookStruct)Marshal.PtrToStructure(lParam, typeof(KbLLHookStruct));
                if (SetHotKeyMode)
                {
                    hotkey =(Keys)hookStruct.vkCode;
                    if (OnSetKey != null)
                    {
                        OnSetKey();
                    }
                }
                else if (hookStruct.vkCode == (int)hotkey && Active) 
                {
                    if (IsPressed == false)
                    {
                        IsPressed = true;
                        CountDown = 500;
                        Task.Run(() =>
                        {
                            OnHotKeyDown();
                            while (CountDown > 0)
                            {
                                Thread.Sleep(100);
                                CountDown -= 100;
                            }
                            IsPressed = false;
                            OnHotKeyUp();
                        });
                    }
                    else
                    {
                        CountDown = 500;
                    }
                }
            }
            // Pass to other keyboard handlers. Makes the Ctrl+V pass through.
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        ~KBHook()
        {
            UnhookWindowsHookEx(_hookHandle);
        }

        //Declare the wrapper managed MouseHookStruct class.
        [StructLayout(LayoutKind.Sequential)]
        public class KbLLHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }
    }
}
