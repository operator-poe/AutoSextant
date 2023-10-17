using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ExileCore.Shared;
using SharpDX;

namespace AutoSextant;

public class Input : ExileCore.Input
{
  private static AutoSextant Instance = AutoSextant.Instance;
  public static IEnumerator ClickElement(Vector2 pos, MouseButtons mouseButton = MouseButtons.Left, int delay = 30)
  {
    MoveMouseToElement(pos);
    yield return Delay(delay);
    Click(mouseButton);
    yield return Delay(delay);
  }


  public static IEnumerator ClickElement(Vector2 pos, int delay)
  {
    yield return ClickElement(pos, MouseButtons.Left, delay);
  }

  // public static new IEnumerator Click(MouseButtons mouseButton = MouseButtons.Left)
  // {
  //   ExileCore.Input.Click(mouseButton);
  //   yield return Delay();
  // }

  public static void MoveMouseToElement(Vector2 pos)
  {
    SetCursorPos(pos + Instance.GameController.Window.GetWindowRectangle().TopLeft);
  }

  public static IEnumerator Delay(int ms = 0)
  {
    yield return new WaitTime(Instance.Settings.ExtraDelay.Value + ms);
  }

  public static new void VerticalScroll(bool scrollUp, int clicks)
  {
    const int wheelDelta = 120;
    if (scrollUp)
      WinApi.mouse_event(MOUSE_EVENT_WHEEL, 0, 0, clicks * wheelDelta, 0);
    else
      WinApi.mouse_event(MOUSE_EVENT_WHEEL, 0, 0, -(clicks * wheelDelta), 0);
  }

  private static Vector2 LastMousePosition = Vector2.Zero;
  public static void StorePosition()
  {
    LastMousePosition = Input.ForceMousePosition;
  }

  public static void RestorePosition()
  {
    Input.SetCursorPos(LastMousePosition);
    LastMousePosition = Vector2.Zero;
  }

  public static void HoldCtrl()
  {
    Log.Debug("Holding Ctrl");
    KeyDown(Keys.ControlKey);
  }

  public static void ReleaseCtrl()
  {
    Log.Debug("Releasing Ctrl");
    KeyUp(Keys.ControlKey);
  }

  public static void HoldShift()
  {
    Log.Debug("Holding Shift");
    KeyDown(Keys.ShiftKey);
  }

  public static void ReleaseShift()
  {
    Log.Debug("Releasing Shift");
    KeyUp(Keys.ShiftKey);
  }

  public static void BlockMouse()
  {
    MouseInputBlocker.BlockMouseInput();
  }

  public static void UnblockMouse()
  {
    MouseInputBlocker.UnblockMouseInput();
  }

  public static void RegisterKey(Keys key, bool suppress = false)
  {
    if (suppress)
      KeySuppressionManager.SuppressKey(key);
    ExileCore.Input.RegisterKey(key);
  }
}


public static class KeySuppressionManager
{
  private const int WH_KEYBOARD_LL = 13;
  private const int WM_KEYDOWN = 0x0100;
  private static LowLevelKeyboardProc _proc = HookCallback;
  private static IntPtr _hookID = IntPtr.Zero;
  private static HashSet<Keys> _suppressedKeys = new HashSet<Keys>();

  static KeySuppressionManager()
  {
    _hookID = SetHook(_proc);
    Application.ApplicationExit += (sender, e) => UnhookWindowsHookEx(_hookID);
  }

  public static void SuppressKey(Keys key)
  {
    _suppressedKeys.Add(key);
  }

  public static void AllowKey(Keys key)
  {
    _suppressedKeys.Remove(key);
  }

  private static IntPtr SetHook(LowLevelKeyboardProc proc)
  {
    using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
    using (var curModule = curProcess.MainModule)
    {
      return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
    }
  }

  private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

  private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
  {
    if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
    {
      int vkCode = Marshal.ReadInt32(lParam);
      Keys key = (Keys)vkCode;
      if (_suppressedKeys.Contains(key))
      {
        return (IntPtr)1;
      }
    }
    return CallNextHookEx(_hookID, nCode, wParam, lParam);
  }

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool UnhookWindowsHookEx(IntPtr hhk);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

  [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr GetModuleHandle(string lpModuleName);
}


public class MouseInputBlocker
{
  private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
  private static IntPtr hookId = IntPtr.Zero;

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool UnhookWindowsHookEx(IntPtr hhk);

  public static void BlockMouseInput()
  {
    hookId = SetWindowsHookEx(14, HookCallback, IntPtr.Zero, 0);  // 14 is WH_MOUSE_LL
  }

  public static void UnblockMouseInput()
  {
    UnhookWindowsHookEx(hookId);
  }

  private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
  {
    return (IntPtr)1; // Block all mouse input
  }
}
