using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore.Shared;

namespace AutoSextant.SellAssistant;

public static class Util
{

    public static int LevenshteinDistance(string s, string t)
    {
        // Special cases
        if (s == t) return 0;
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;
        // Initialize the distance matrix
        int[,] distance = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) distance[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) distance[0, j] = j;
        // Calculate the distance
        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
            }
        }
        // Return the distance
        return distance[s.Length, t.Length];
    }

    public static void SetClipBoardText(string text)
    {
        var thread = new Thread(() =>
        {
            Clipboard.SetText(text);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    public static IEnumerator ForceFocus()
    {
        if (!AutoSextant.Instance.GameController.Window.IsForeground())
        {
            IntPtr handle = FindWindow(null, "Path of Exile");
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
        }
        yield return new WaitFunctionTimed(AutoSextant.Instance.GameController.Window.IsForeground, true, 1000, "Window could not be focused");
    }

    public static string GetClipboardText()
    {
        string result = string.Empty;
        Thread staThread = new Thread(() =>
        {
            result = Clipboard.GetText();
        });
        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        return result;
    }

    [DllImport("user32.dll")]
    public static extern short VkKeyScan(char ch);

    public static Keys KeyCodeSlash = (Keys)(int)(byte)(VkKeyScan('/') & 0xFF);

    public static string FormatChaosPrice(float value, float? DivinePrice = null)
    {
        if (DivinePrice == null || Math.Abs(value) < DivinePrice)
            return $"{value.ToString("0.0")}c";

        int divines = (int)(value / DivinePrice);
        float chaos = value % DivinePrice ?? 0;
        return $"{divines} div, {chaos.ToString("0.0")}c";
    }
}
