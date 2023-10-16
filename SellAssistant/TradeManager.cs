using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace AutoSextant.SellAssistant;

public enum TradeRequestStatus
{
    None,
    RequestMade,
    RequestAccepted,
    ItemsTransferred,
    ValuesHovered,
    Accepted,
    Cancelled,

}
public class TradeRequest
{
    public string PlayerName { get; set; }
    public float ExpectedValue { get; set; }
    public TradeRequestStatus Status { get; set; } = TradeRequestStatus.None;
}

public static class TradeManager
{
    private static TradeWindow TradeWindow
    {
        get
        {
            return AutoSextant.Instance.GameController.Game.IngameState.IngameUi.TradeWindow;
        }
    }

    private static readonly string _tradeCoroutineName = "AutoSextantTradeAction";

    private static List<TradeRequest> TradeQueue = new List<TradeRequest>();
    private static TradeRequest ActiveTrade = null;
    private static string chatPointer = null;

    public static void AddTradeRequest(TradeRequest request)
    {
        TradeQueue.Add(request);
    }

    public static bool IsAnyRoutineRunning
    {
        get
        {
            return
                TradeQueue.Count > 0 ||
                ActiveTrade != null ||
                Core.ParallelRunner.FindByName(_tradeCoroutineName) != null;
        }
    }
    public static void StopAllRoutines(bool clearQueue = true)
    {
        if (clearQueue)
            TradeQueue.Clear();
        ActiveTrade = null;
        if (chatPointer != null)
            Chat.RemovePointer(chatPointer);
        chatPointer = null;
        Core.ParallelRunner.FindByName(_tradeCoroutineName)?.Done();
        Input.ReleaseCtrl();
    }

    public static void Tick()
    {
        var coroutineRunning = Core.ParallelRunner.FindByName(_tradeCoroutineName) != null;
        if (TradeQueue.Count > 0 && ActiveTrade == null && !coroutineRunning)
        {
            ActiveTrade = TradeQueue[0];
            TradeQueue.RemoveAt(0);
        }

        if (ActiveTrade != null && !coroutineRunning)
        {
            if (chatPointer == null)
                chatPointer = Chat.GetPointer();
            if (Chat.CheckNewMessages(chatPointer, "Trade cancelled"))
                ActiveTrade.Status = TradeRequestStatus.Cancelled;

            switch (ActiveTrade.Status)
            {
                case TradeRequestStatus.None:
                    Log.Debug($"Sending trade request to {ActiveTrade.PlayerName}");
                    SendTradeRequest();
                    break;
                case TradeRequestStatus.RequestMade:
                    Log.Debug($"Waiting for {ActiveTrade.PlayerName} to accept trade request");
                    if (TradeWindow.IsVisible)
                    {
                        ActiveTrade.Status = TradeRequestStatus.RequestAccepted;
                    }
                    break;
                case TradeRequestStatus.RequestAccepted:
                    Core.ParallelRunner.Run(new Coroutine(TransferItems(), AutoSextant.Instance, _tradeCoroutineName));
                    break;
                case TradeRequestStatus.ItemsTransferred:
                    // Break here for now for testing purposes
                    ActiveTrade.Status = TradeRequestStatus.Accepted;
                    break;
                case TradeRequestStatus.ValuesHovered:
                    break;
                case TradeRequestStatus.Accepted:
                case TradeRequestStatus.Cancelled:
                    StopAllRoutines(false);
                    break;
                default:
                    break;
            }
        }
    }

    public static void Render()
    {

    }

    private static IList<InventSlotItem> Compasses
    {
        get
        {
            return NInventory.Inventory.GetByName("Charged Compass");
        }
    }
    private static int CompassCount
    {
        get
        {
            return Compasses.Count();
        }
    }
    private static InventSlotItem NextCompass
    {
        get
        {
            return Compasses.First();
        }
    }
    public static IEnumerator TransferItems()
    {
        yield return Util.ForceFocus();
        int startingItems = CompassCount;
        Log.Debug($"Transferring {startingItems} compasses to {ActiveTrade.PlayerName}");

        Input.HoldCtrl();
        var attempts = 0;
        while (TradeWindow.YourOffer.Count() < startingItems)
        {
            attempts++;
            if (attempts > 3)
            {
                Log.Error("Failed to transfer compasses");
                yield break;
            }
            foreach (var item in Compasses)
            {
                var oldCount = TradeWindow.YourOffer.Count();

                yield return Input.ClickElement(item.GetClientRect().Center);

                yield return new WaitFunctionTimed(() => TradeWindow.YourOffer.Count() > oldCount, false, 100, "Compass not transferred");
            }
        }
        Input.ReleaseCtrl();

        ActiveTrade.Status = TradeRequestStatus.ItemsTransferred;
    }

    public static void SendTradeRequest()
    {
        Chat.QueueMessage("/tradewith " + ActiveTrade.PlayerName);
        ActiveTrade.Status = TradeRequestStatus.RequestMade;
    }
}
