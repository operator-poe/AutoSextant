using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
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
    public static AutoSextant Instance = AutoSextant.Instance;
    public static Dictionary<string, ChatPointer> _pointers = new Dictionary<string, ChatPointer>();
    public static List<string> LastChatUsers = new List<string>();

    private static ThrottledAction _updateLastChatUsers = new ThrottledAction(TimeSpan.FromMilliseconds(1000), () =>
    {
        HashSet<string> uniqueNames = new HashSet<string>();
        List<string> lastThreeUniqueNames = new List<string>(5); // Allocate size
        List<string> Messages = ChatMessages.ToArray().ToList();
        int chatCount = Messages.Count;

        for (int i = chatCount - 1; i >= 0 && lastThreeUniqueNames.Count < 5; i--)
        {
            string entry = Messages[i];
            if (entry.Length <= 5 || entry[0] != '@' || (entry[1] != 'F' && entry[1] != 'T')) continue;

            int indexColon = entry.IndexOf(':');
            int indexSpace = entry.LastIndexOf(' ', indexColon);

            if (indexSpace > 0 && indexColon > indexSpace)
            {
                string name = entry.Substring(indexSpace + 1, indexColon - indexSpace - 1);
                if (uniqueNames.Add(name))
                {
                    lastThreeUniqueNames.Add(name);
                }
            }
        }

        LastChatUsers = lastThreeUniqueNames;
    });
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


    public static void QueueMessage(string message)
    {
        Log.Debug($"Queueing message: {message}");
        SendChatMessage(message);
    }

    public static void QueueMessage(string[] messages)
    {
        Log.Debug($"Queueing messages: {string.Join(", ", messages)}");
        SendChatMessage(messages);
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
    }

    public static void Tick()
    {
        _updateLastChatUsers.Run();
    }

    private static readonly string _sendChatCoroutineName = "AutoSextant.SendChatMessage";
    public static void SendChatMessage(string message)
    {
        Instance.Scheduler.AddTask(Send([message]), "Chat.SendChatMessage");
    }
    public static void SendChatMessage(string[] message)
    {
        Instance.Scheduler.AddTask(Send(message), "Chat.SendChatMessage");
    }

    public static async SyncTask<bool> Clear()
    {
        await Open();

        if (CurrentInput == null || CurrentInput == "")
            return true;


        // second method: select all and delete
        await InputAsync.KeyDown(Keys.ControlKey);
        await InputAsync.KeyPress(Keys.A);
        await InputAsync.KeyUp(Keys.ControlKey);
        await InputAsync.KeyPress(Keys.Delete);

        await InputAsync.Wait(() => CurrentInput == null, 100, "Chat input not cleared (2)");

        // third method: just backspace until empty
        while (CurrentInput != null)
        {
            await InputAsync.KeyPress(Keys.Back);
        }
        await InputAsync.Wait(() => CurrentInput == null, 100, "Chat input not cleared (3)");
        return true;
    }

    public static async SyncTask<bool> Replace()
    {
        await Open();

        if (CurrentInput != null && CurrentInput != "")
        {
            await InputAsync.KeyDown(Keys.ControlKey);
            await InputAsync.KeyPress(Keys.A);
            await InputAsync.KeyUp(Keys.ControlKey);
        }
        return true;
    }

    public static async SyncTask<bool> Send(string[] messages, bool replace = true)
    {
        foreach (var message in messages)
        {
            await Open();
            if (replace)
                await Replace();
            else
                await Clear();

            Util.SetClipBoardText(message);
            await InputAsync.Wait(() => Util.GetClipboardText() == message, 1000, "Clipboard text not set");
            await InputAsync.KeyDown(Keys.ControlKey);
            await InputAsync.KeyPress(Keys.V);
            await InputAsync.KeyUp(Keys.ControlKey);

            await InputAsync.Wait(() => CurrentInput == message, 1000, "Chat input not set to message");

            await InputAsync.KeyPress(Keys.Enter);

            await InputAsync.Wait(() => !IsOpen, 1000, "Chat window not closed");
        }
        return true;
    }

    public static void ChatWith(string username)
    {
        Instance.Scheduler.AddTask(ChatWithUser(username), "Chat.ChatWithUser");
    }

    public static async SyncTask<bool> ChatWithUser(string username)
    {
        await Open();
        await Replace();

        Util.SetClipBoardText($"@{username} ");
        await InputAsync.Wait(() => Util.GetClipboardText() == $"@{username} ", 1000, "Clipboard text not set");
        await InputAsync.KeyDown(Keys.ControlKey);
        await InputAsync.KeyPress(Keys.V);
        await InputAsync.KeyUp(Keys.ControlKey);

        await InputAsync.Wait(() => CurrentInput == $"@{username} ", 1000, "Chat input not set to message");
        return true;
    }

    public static async SyncTask<bool> Open()
    {
        await Util.ForceFocusAsync();
        if (!IsOpen)
        {
            await InputAsync.KeyPress(Keys.Enter);
        }

        return await InputAsync.Wait(() => IsOpen, 1000, "Cannot open chat");
    }
}