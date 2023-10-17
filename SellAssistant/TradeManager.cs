using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using SharpDX;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace AutoSextant.SellAssistant;

public enum TradeRequestStatus
{
    None,
    RequestMade,
    RequestAccepted,
    ItemsTransferred,
    ValuesHovered,
    ManualFinish,
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
            if (Chat.CheckNewMessages(chatPointer, "Trade cancelled", false)) // Always interrupt if cancelled
                ActiveTrade.Status = TradeRequestStatus.Cancelled;
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
                    Log.Debug("All items transferred, begin hovering items");
                    Core.ParallelRunner.Run(new Coroutine(HoverTradeItems(), AutoSextant.Instance, _tradeCoroutineName));
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
        if (ActiveTrade == null || !TradeWindow.IsVisible && ActiveTrade.ExpectedValue > 0)
            return;

        var rect = TradeWindow.GetClientRect();
        // draw total chaos value
        var chaosValue = Util.FormatChaosPrice(TotalChaosValue, DivinePrice);
        var expectedValue = Util.FormatChaosPrice(ActiveTrade.ExpectedValue, DivinePrice);
        var color = TotalChaosValue >= ActiveTrade.ExpectedValue * 0.95 ? Color.Green : Color.Red;
        AutoSextant.Instance.Graphics.DrawText($"Total Value: {chaosValue} / {expectedValue}", rect.TopLeft, color, 20);
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

    private static IList<NormalInventoryItem> OfferedItems
    {
        get
        {
            return TradeWindow.OtherOffer;
        }
    }

    private static float DivinePrice
    {
        get
        {
            var report = SellAssistant.CurrentReport;
            if (report == null)
                return 0; // Todo insert price from poestack download here
            else
                return report.DivinePrice;
        }
    }

    private static float TotalChaosValue
    {
        get
        {
            return OfferedItems.Sum(item =>
            {
                var name = item.Item.GetComponent<Base>().Name;
                var stack = item.Item.GetComponent<ExileCore.PoEMemory.Components.Stack>();
                if (name == "Divine Orb")
                    return stack.Size * DivinePrice;
                else if (name == "Chaos Orb")
                    return stack.Size;
                else
                    return 0;
            });
        }
    }

    private static bool ItemsLeftToHover
    {
        get
        {
            var enableAcceptLabel = TradeWindow.GetChildFromIndices(3, 1, 0, 0, 4);
            return enableAcceptLabel != null && enableAcceptLabel.Width > 0;
        }
    }

    public static IEnumerator HoverTradeItems()
    {
        bool valueMode = false;
        float divinePrice = 0;
        if (SellAssistant.CurrentReport != null)
        {
            valueMode = ActiveTrade.ExpectedValue > 0;
            divinePrice = SellAssistant.CurrentReport.DivinePrice;
        }

        if (!valueMode)
        {
            Log.Debug("Hovering other player's items in non-value mode");
            int startingItems = 0;
            Dictionary<int, bool> hoveredItems = new Dictionary<int, bool>();
            // In non-value mode we're just waiting for the trade to be accepted
            // mouse movement is not restricted, only taken over when new items are added
            while (ActiveTrade.Status == TradeRequestStatus.ItemsTransferred)
            {
                var dirty = false;
                if (startingItems != OfferedItems.Count())
                {
                    Log.Debug($"Items added, hovering {OfferedItems.Count()} items total");
                    startingItems = OfferedItems.Count();
                    dirty = true;
                }
                if (dirty)
                {
                    for (int i = 0; i < OfferedItems.Count(); i++)
                    {
                        if (!hoveredItems.ContainsKey(i))
                            hoveredItems.Add(i, false);

                        if (!hoveredItems[i])
                        {
                            yield return new WaitTime(10);
                            Input.SetCursorPos(OfferedItems[i].GetClientRect().Center);
                            yield return new WaitTime(10);
                            // Width is only intialized after the item is hovered
                            yield return new WaitFunctionTimed(() => OfferedItems[i].Width > 0, false, 100);
                            hoveredItems[i] = true;
                        }
                    }
                }
                Log.Debug($"Hovered {hoveredItems.Count(x => x.Value)} of {hoveredItems.Count} items, waiting for more");
                yield return new WaitFunctionTimed(() => OfferedItems.Count() > startingItems, false, 500);
            }
            // one last check to make sure we didn't miss any items
            if (ItemsLeftToHover)
                yield return HoverTradeItems();
            // no need to do anything else in non-value mode, just return
            yield break;
        }

        Log.Debug("Hovering other player's items in value mode");

        int itemCount = 0;
        Dictionary<int, bool> itemsHovered = new Dictionary<int, bool>();
        while (TotalChaosValue < ActiveTrade.ExpectedValue * 0.95)
        {
            Log.Debug($"Total value of {TotalChaosValue} is less than {ActiveTrade.ExpectedValue * 0.95}, waiting for more items to hover");
            var dirty = false;
            if (itemCount != OfferedItems.Count())
            {
                Log.Debug($"Items added, hovering {OfferedItems.Count()} items total");
                itemCount = OfferedItems.Count();
                dirty = true;
            }
            if (dirty)
            {
                for (int i = 0; i < OfferedItems.Count(); i++)
                {
                    if (!itemsHovered.ContainsKey(i))
                        itemsHovered.Add(i, false);

                    if (!itemsHovered[i])
                    {
                        yield return new WaitTime(10);
                        Input.SetCursorPos(OfferedItems[i].GetClientRect().Center);
                        yield return new WaitTime(10);
                        // Width is only intialized after the item is hovered
                        yield return new WaitFunctionTimed(() => OfferedItems[i].Width > 0, false, 100);
                        itemsHovered[i] = true;
                    }
                }
            }
            Log.Debug($"Hovered {itemsHovered.Count(x => x.Value)} of {itemsHovered.Count} items, waiting for more");
            yield return new WaitFunctionTimed(() => OfferedItems.Count() > itemCount, false, 500);
        }
        // one last check to make sure we didn't miss any items
        if (ItemsLeftToHover)
            yield return HoverTradeItems();

        Log.Debug($"Total value of {TotalChaosValue} is greater than {ActiveTrade.ExpectedValue * 0.95}, accepting trade");
        yield return new WaitTime(100);
        yield return Input.ClickElement(TradeWindow.AcceptButton.GetClientRect().Center);
        yield return new WaitFunctionTimed(() => TradeWindow.SellerAccepted, false, 1000, "Button never reached accepted state");
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
