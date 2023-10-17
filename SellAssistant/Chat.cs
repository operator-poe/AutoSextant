using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;

namespace AutoSextant.SellAssistant;

public class ChatPointer
{
    public string Pointer { get; set; }
    public int Index { get; set; }
}
public static class Chat
{
    public static Dictionary<string, ChatPointer> _pointers = new Dictionary<string, ChatPointer>();
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

    private static IList<string> ChatMessages
    {
        get
        {
            return AutoSextant.Instance.GameController.Game.IngameState.IngameUi.ChatMessages;
        }
    }

    public static string GetPointer()
    {
        var pointerId = Guid.NewGuid().ToString();
        var pointer = (ChatMessages.Count == 0) ? new ChatPointer
        {
            Pointer = null,
            Index = -1
        } : new ChatPointer
        {
            Pointer = ChatMessages[^1],
            Index = ChatMessages.Count - 1
        };
        _pointers.Add(pointerId, pointer);
        return pointerId;
    }

    public static void RemovePointer(string pointerId)
    {
        if (_pointers.ContainsKey(pointerId))
            _pointers.Remove(pointerId);
    }

    public static void UpdatePointer(string pointerId)
    {
        if (!_pointers.ContainsKey(pointerId))
            return;
        if (ChatMessages.Count == 0)
            return;
        var pointer = _pointers[pointerId];
        pointer.Index = ChatMessages.Count - 1;
        pointer.Pointer = ChatMessages[^1];
    }

    public static List<string> NewMessages(string pointerId, bool updatePointer = true)
    {
        if (!_pointers.ContainsKey(pointerId))
            return null;
        var pointer = _pointers[pointerId];
        if (ChatMessages.Count == 0)
            return new List<string>();

        List<string> Messages = new List<string>();
        for (int i = ChatMessages.Count - 1; i >= 0; i--)
        {
            if (ChatMessages[i] == pointer.Pointer)
            {
                break;
            }
            Messages.Add(ChatMessages[i]);
        }
        if (updatePointer)
        {
            pointer.Index = ChatMessages.Count - 1;
            pointer.Pointer = ChatMessages[^1];
        }
        return Messages;
    }

    public static bool CheckNewMessages(string pointerId, string search, bool updatePointer = true)
    {
        if (pointerId == null || !_pointers.ContainsKey(pointerId))
            return false;
        var messages = NewMessages(pointerId, updatePointer);
        return messages.Any(x => x.Contains(search));
    }

    public static List<string> GetPastMessages(string search, int count = -1)
    {
        var messages = ChatMessages.Where(x => x.Contains(search)).ToList();
        if (count != -1)
            messages = messages.TakeLast(count).ToList();
        return messages;
    }

    public static List<string> GetPastMessages(string search, List<Regex> regex, int count = -1)
    {
        var messages = ChatMessages.Where(x => (search == "" || x.Contains(search)) && x.Contains(search) && regex.Any(y => y.IsMatch(x))).ToList();
        if (count != -1)
            messages = messages.TakeLast(count).ToList();
        return messages;
    }

    private static List<string> MessageQueue = new List<string>();

    public static void QueueMessage(string message)
    {
        Log.Debug($"Queuing message: {message}");
        MessageQueue.Add(message);
    }

    public static void QueueMessage(string[] messages)
    {
        MessageQueue.AddRange(messages);
    }

    public static bool IsAnyRoutineRunning
    {
        get
        {
            return Core.ParallelRunner.FindByName(_sendChatCoroutineName) != null;
        }
    }
    public static void StopAllRoutines()
    {
        MessageQueue.Clear();
        Core.ParallelRunner.FindByName(_sendChatCoroutineName)?.Done();
    }

    public static void Tick()
    {
        if (MessageQueue.Count > 0 && Core.ParallelRunner.FindByName(_sendChatCoroutineName) == null)
        {
            string message = MessageQueue[0];
            MessageQueue.RemoveAt(0);
            SendChatMessage(message);
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