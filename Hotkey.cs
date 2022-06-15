using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinVolume
{
    internal struct KeyboardParams
    {
        public IntPtr wParam;
        public int vkCode;

        public KeyboardParams(IntPtr wParam, int vkCode)
        {
            this.wParam = wParam;
            this.vkCode = vkCode;
        }
    }

    internal class KeybindStruct : IEquatable<KeybindStruct>
    {
        public readonly int VirtualKeyCode;
        public readonly List<ModifierKeys> Modifiers;
        public readonly Guid? Identifier;

        public KeybindStruct(IEnumerable<ModifierKeys> modifiers, int virtualKeyCode, Guid? identifier = null)
        {
            this.VirtualKeyCode = virtualKeyCode;
            this.Modifiers = new List<ModifierKeys>(modifiers);
            this.Identifier = identifier;
        }

        public bool Equals(KeybindStruct other)
        {
            if (other == null)
                return false;

            if (this.VirtualKeyCode != other.VirtualKeyCode)
                return false;

            if (this.Modifiers.Count != other.Modifiers.Count)
                return false;

            foreach (var modifier in this.Modifiers)
            {
                if (!other.Modifiers.Contains(modifier))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;

            return Equals((KeybindStruct)obj);
        }

        public override int GetHashCode()
        {
            var hash = 13;
            hash = (hash * 7) + VirtualKeyCode.GetHashCode();
            var modifiersHashSum = 0;
            foreach (var modifier in this.Modifiers)
            {
                modifiersHashSum += modifier.GetHashCode();
            }
            hash = (hash * 7) + modifiersHashSum;
            return hash;
        }
    }

    public class KeyboardHookManager
    {
        private readonly Dictionary<KeybindStruct, Action> _registeredCallbacks;
        private readonly HashSet<ModifierKeys> _downModifierKeys;
        private readonly HashSet<int> _downKeys;
        private readonly object _modifiersLock = new object();
        private LowLevelKeyboardProc _hook;
        private bool _isStarted;
        public KeyboardHookManager()
        {
            this._registeredCallbacks = new Dictionary<KeybindStruct, Action>();
            this._downModifierKeys = new HashSet<ModifierKeys>();
            this._downKeys = new HashSet<int>();
        }
        public void Start()
        {
            if (this._isStarted) return;

            this._hook = this.HookCallback;
            _hookId = SetHook(this._hook);
            this._isStarted = true;
        }
        public void Stop()
        {
            if (this._isStarted)
            {
                UnhookWindowsHookEx(_hookId);
                this._isStarted = false;
            }
        }
        public Guid RegisterHotkey(int virtualKeyCode, Action action)
        {
            return this.RegisterHotkey(new ModifierKeys[0], virtualKeyCode, action);
        }
        public Guid RegisterHotkey(ModifierKeys modifiers, int virtualKeyCode, Action action)
        {
            var allModifiers = Enum.GetValues(typeof(ModifierKeys)).Cast<ModifierKeys>().ToArray();
            var selectedModifiers = allModifiers.Where(modifier => modifiers.HasFlag(modifier)).ToArray();
            return RegisterHotkey(selectedModifiers, virtualKeyCode, action);
        }
        public Guid RegisterHotkey(ModifierKeys[] modifiers, int virtualKeyCode, Action action)
        {
            var keybindIdentity = Guid.NewGuid();
            var keybind = new KeybindStruct(modifiers, virtualKeyCode, keybindIdentity);
            if (this._registeredCallbacks.ContainsKey(keybind))
            {
                throw new HotkeyAlreadyRegisteredException();
            }

            this._registeredCallbacks[keybind] = action;
            return keybindIdentity;
        }
        public void UnregisterAll()
        {
            this._registeredCallbacks.Clear();
        }
        public void UnregisterHotkey(int virtualKeyCode)
        {
            this.UnregisterHotkey(new ModifierKeys[0], virtualKeyCode);
        }
        public void UnregisterHotkey(ModifierKeys[] modifiers, int virtualKeyCode)
        {
            var keybind = new KeybindStruct(modifiers, virtualKeyCode);

            if (!this._registeredCallbacks.Remove(keybind))
            {
                throw new HotkeyNotRegisteredException();
            }
        }
        public void UnregisterHotkey(Guid keybindIdentity)
        {
            var keybindToRemove = this._registeredCallbacks.Keys.FirstOrDefault(keybind =>
                keybind.Identifier.HasValue && keybind.Identifier.Value.Equals(keybindIdentity));

            if (keybindToRemove == null || !this._registeredCallbacks.Remove(keybindToRemove))
            {
                throw new HotkeyNotRegisteredException();
            }
        }
        private void HandleKeyPress(int virtualKeyCode)
        {
            var currentKey = new KeybindStruct(this._downModifierKeys, virtualKeyCode);
            if (!this._registeredCallbacks.ContainsKey(currentKey))
            {
                return;
            }
            if (this._registeredCallbacks.TryGetValue(currentKey, out var callback))
            {
                callback.Invoke();
            }
        }
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private static IntPtr _hookId = IntPtr.Zero;
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            var userLibrary = LoadLibrary("User32");

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                userLibrary, 0);
        }
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var vkCode = Marshal.ReadInt32(lParam);
                ThreadPool.QueueUserWorkItem(this.HandleSingleKeyboardInput, new KeyboardParams(wParam, vkCode));
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        private void HandleSingleKeyboardInput(object keyboardParamsObj)
        {
            var keyboardParams = (KeyboardParams)keyboardParamsObj;
            var wParam = keyboardParams.wParam;
            var vkCode = keyboardParams.vkCode;
            var modifierKey = ModifierKeysUtilities.GetModifierKeyFromCode(vkCode);
            if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
            {
                if (modifierKey != null)
                {
                    lock (this._modifiersLock)
                    {
                        this._downModifierKeys.Add(modifierKey.Value);
                    }
                }
                if (!this._downKeys.Contains(vkCode))
                {
                    this.HandleKeyPress(vkCode);
                    this._downKeys.Add(vkCode);
                }
            }
            if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
            {
                if (modifierKey != null)
                {
                    lock (this._modifiersLock)
                    {
                        this._downModifierKeys.Remove(modifierKey.Value);
                    }
                }
                this._downKeys.Remove(vkCode);
            }
        }
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }

    public class KeyboardHookException : Exception
    {
    }

    public class HotkeyAlreadyRegisteredException : KeyboardHookException
    {
    }

    public class HotkeyNotRegisteredException : KeyboardHookException
    {
    }

    [Flags]
    public enum ModifierKeys
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        WindowsKey = 8,
    }

    public static class ModifierKeysUtilities
    {
        public static ModifierKeys? GetModifierKeyFromCode(int keyCode)
        {
            switch (keyCode)
            {
                case 0xA0:
                case 0xA1:
                case 0x10:
                    return ModifierKeys.Shift;
                case 0xA2:
                case 0xA3:
                case 0x11:
                    return ModifierKeys.Control;
                case 0x12:
                case 0xA4:
                case 0xA5:
                    return ModifierKeys.Alt;
                case 0x5B:
                case 0x5C:
                    return ModifierKeys.WindowsKey;
                default:
                    return null;
            }
        }
    }
}
