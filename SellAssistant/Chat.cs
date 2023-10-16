using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;

namespace AutoSextant.SellAssistant;
public static class Chat
{
    public static ChatPanel Panel
    {
        get
        {
            return AutoSextant.Instance.GameController.Game.IngameState.IngameUi.ChatPanel;
        }
    }

    public static bool IsOpen
    {
        get
        {
            return Panel.ChatInputElement.IsVisible;
        }
    }

    public static string CurrentInput
    {
        get
        {
            return Panel.InputText;
        }
    }

    private static readonly string _sendChatCoroutineName = "AutoSextant.SendChatMessage";
    public static void SendChatMessage(string message)
    {
        if (Core.ParallelRunner.FindByName(_sendChatCoroutineName) == null)
        {
            Core.ParallelRunner.Run(Send(new string[] { message }), AutoSextant.Instance, _sendChatCoroutineName);
        }
        else
        {
            Log.Error("Cannot send chat message while another message is being sent");
            return;
        }
    }
    public static void SendChatMessage(string[] message)
    {
        if (Core.ParallelRunner.FindByName(_sendChatCoroutineName) == null)
        {
            Core.ParallelRunner.Run(Send(message), AutoSextant.Instance, _sendChatCoroutineName);
        }
        else
        {
            Log.Error("Cannot send chat message while another message is being sent");
            return;
        }
    }

    public static IEnumerator Clear()
    {
        yield return Open();

        if (CurrentInput == null || CurrentInput == "")
            yield break;


        // second method: select all and delete
        Input.KeyDown(Keys.ControlKey);
        yield return Input.KeyPress(Keys.A);
        Input.KeyUp(Keys.ControlKey);
        yield return Input.KeyPress(Keys.Delete);

        yield return new WaitFunctionTimed(() => CurrentInput == null, false, 100, "Chat input not cleared (2)");

        // third method: just backspace until empty
        while (CurrentInput != null)
        {
            yield return Input.KeyPress(Keys.Back);
        }
        yield return new WaitFunctionTimed(() => CurrentInput == null, true, 100, "Chat input not cleared (3)");
    }

    public static IEnumerator Replace()
    {
        yield return Open();

        if (CurrentInput != null && CurrentInput != "")
        {
            Input.KeyDown(Keys.ControlKey);
            yield return Input.KeyPress(Keys.A);
            Input.KeyUp(Keys.ControlKey);
        }
    }

    public static IEnumerator Send(string[] messages, bool replace = true)
    {
        foreach (var message in messages)
        {
            yield return Open();
            if (replace)
                yield return Replace();
            else
                yield return Clear();

            Util.SetClipBoardText(message);
            yield return new WaitFunctionTimed(() => Util.GetClipboardText() == message, true, 1000, "Clipboard text not set");
            Input.KeyDown(Keys.ControlKey);
            yield return Input.KeyPress(Keys.V);
            Input.KeyUp(Keys.ControlKey);

            yield return new WaitFunctionTimed(() => CurrentInput == message, true, 1000, "Chat input not set to message");

            yield return Input.KeyPress(Keys.Enter);
            yield return new WaitFunctionTimed(() => !IsOpen, true, 1000, "Chat window not closed");
        }
    }

    public static IEnumerator Open()
    {
        yield return Util.ForceFocus();
        if (!IsOpen)
        {
            yield return Input.KeyPress(Keys.Enter);
        }

        yield return new WaitFunctionTimed(() => IsOpen, true, 1000, "Cannot open chat");
    }
}