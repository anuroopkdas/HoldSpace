using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace HoldSpace.Services
{
    public enum HookStatus
    {
        Idle,
        TriggerDown,
        OverlayArmed,
        TriggerReleased
    }

    public class InputHookService : IDisposable
    {
        // Delegates and Win32 Signatures
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // Win32 Constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const byte VK_CAPITAL = 0x14; // Caps Lock
        private const byte VK_ESCAPE = 0x1B;  // Escape

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int LLKHF_INJECTED = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        // State variables
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _hookProcedure;
        
        private bool _isTriggerDown;
        private bool _isArmed;
        private bool _isCanceled;
        private int _holdDelayMs = 120;
        private byte _targetVkCode = VK_CAPITAL;
        
        private DispatcherTimer? _holdTimer;
        private HookStatus _status = HookStatus.Idle;

        // Events
        public event Action? TriggerPressed;
        public event Action<bool>? TriggerReleased; // true if it was armed and not canceled
        public event Action<HookStatus>? StatusChanged;

        public HookStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    StatusChanged?.Invoke(_status);
                }
            }
        }

        public void StartHook(int holdDelayMs = 120, byte targetVk = VK_CAPITAL)
        {
            _holdDelayMs = holdDelayMs;
            _targetVkCode = targetVk;

            _holdTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(_holdDelayMs)
            };
            _holdTimer.Tick += OnHoldTimerTick;

            _hookProcedure = HookCallback;
            _hookId = SetHook(_hookProcedure);
            Status = HookStatus.Idle;
        }

        public void StopHook()
        {
            if (_holdTimer != null)
            {
                _holdTimer.Stop();
                _holdTimer.Tick -= OnHoldTimerTick;
                _holdTimer = null;
            }

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            Status = HookStatus.Idle;
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    
                    // Ignore events injected by our own calls
                    bool isInjected = (kb.flags & LLKHF_INJECTED) != 0;
                    if (!isInjected)
                    {
                        int message = wParam.ToInt32();

                        // 1. Handle Trigger Key (e.g. Caps Lock)
                        if (kb.vkCode == _targetVkCode)
                        {
                            if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                            {
                                if (!_isTriggerDown)
                                {
                                    _isTriggerDown = true;
                                    _isCanceled = false;
                                    _isArmed = false;
                                    Status = HookStatus.TriggerDown;

                                    // Start timing the hold
                                    _holdTimer?.Start();
                                }
                                // Block trigger key press from propagating (prevents Caps Lock toggle on hold start)
                                return (IntPtr)1;
                            }
                            else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                            {
                                if (_isTriggerDown)
                                {
                                    _holdTimer?.Stop();
                                    _isTriggerDown = false;

                                    if (_isCanceled)
                                    {
                                        // Already canceled by Esc, just reset state
                                        _isCanceled = false;
                                        Status = HookStatus.Idle;
                                    }
                                    else if (_isArmed)
                                    {
                                        // Trigger was held and released normally
                                        Status = HookStatus.TriggerReleased;
                                        TriggerReleased?.Invoke(true);
                                        Status = HookStatus.Idle;
                                    }
                                    else
                                    {
                                        // Short tap: simulate normal Caps Lock toggle behavior
                                        Status = HookStatus.TriggerReleased;
                                        TriggerReleased?.Invoke(false); // Released but not armed
                                        Status = HookStatus.Idle;

                                        SimulateTap(_targetVkCode);
                                    }
                                }
                                // Block release event to clean up Caps Lock state
                                return (IntPtr)1;
                            }
                        }
                        // 2. Handle Escape Key (only when armed)
                        else if (kb.vkCode == VK_ESCAPE && _isArmed && _isTriggerDown)
                        {
                            if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                            {
                                _isCanceled = true;
                                _holdTimer?.Stop();
                                
                                // Immediately close overlay/de-arm
                                Status = HookStatus.TriggerReleased;
                                TriggerReleased?.Invoke(false); // Notify release with armed=false due to cancellation
                                Status = HookStatus.Idle;

                                // Swallow the Escape key press so it doesn't propagate to other active windows
                                return (IntPtr)1;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in keyboard hook callback: {ex.Message}\n{ex.StackTrace}");
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void OnHoldTimerTick(object? sender, EventArgs e)
        {
            _holdTimer?.Stop();
            if (_isTriggerDown && !_isCanceled)
            {
                _isArmed = true;
                Status = HookStatus.OverlayArmed;
                TriggerPressed?.Invoke();
            }
        }

        private void SimulateTap(byte vkCode)
        {
            // Simulate key down and up. Since these have the LLKHF_INJECTED flag, our hook skips them.
            keybd_event(vkCode, 0x45, 0, UIntPtr.Zero);
            keybd_event(vkCode, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void Dispose()
        {
            StopHook();
        }
    }
}
