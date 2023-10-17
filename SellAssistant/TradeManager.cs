using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    Accepted,
    Cancelled,
    Done,
}
public class TradeRequest
{
    public string PlayerName { get; set; }
    public float ExpectedValue { get; set; } = 0;
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
            lock (TradeQueue)
                TradeQueue.Clear();
        if (ActiveTrade != null)
            lock (ActiveTrade)
                ActiveTrade = null;
        if (chatPointer != null)
            Chat.RemovePointer(chatPointer);
        if (chatPointer != null)
            lock (chatPointer)
                chatPointer = null;
        Core.ParallelRunner.FindByName(_tradeCoroutineName)?.Done();
        Input.ReleaseCtrl();
    }

    public static void Tick()
    {
        var coroutineRunning = Core.ParallelRunner.FindByName(_tradeCoroutineName) != null;
        lock (TradeQueue)
        {
            if (TradeQueue.Count > 0 && ActiveTrade == null && !coroutineRunning)
            {
                ActiveTrade = TradeQueue[0];
                TradeQueue.RemoveAt(0);
            }

            if (ActiveTrade != null && !coroutineRunning)
            {
                lock (ActiveTrade)
                {
                    if (chatPointer == null)
                        chatPointer = Chat.GetPointer();
                    if (Chat.CheckNewMessages(chatPointer, "Trade cancelled", false)) // Always interrupt if cancelled
                        ActiveTrade.Status = TradeRequestStatus.Cancelled;
                    if (Chat.CheckNewMessages(chatPointer, "That player is currently busy", false))
                        ActiveTrade.Status = TradeRequestStatus.Cancelled;
                    if (Chat.CheckNewMessages(chatPointer, "That player is currently busy", false))
                        ActiveTrade.Status = TradeRequestStatus.Cancelled;
                    if (Chat.CheckNewMessages(chatPointer, "Player not found in this area", false))
                        ActiveTrade.Status = TradeRequestStatus.Cancelled;
                    if (Chat.CheckNewMessages(chatPointer, "Trade accepted"))
                        ActiveTrade.Status = TradeRequestStatus.Accepted;

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
                            Log.Debug($"Detected that trade with {ActiveTrade.PlayerName} has been accepted, stashing currency");
                            Core.ParallelRunner.Run(new Coroutine(StashCurrency(), AutoSextant.Instance, _tradeCoroutineName));
                            break;
                        case TradeRequestStatus.Cancelled:
                        case TradeRequestStatus.Done:
                            Log.Debug($"Trade {ActiveTrade.Status}");
                            StopAllRoutines(false);
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }

    public static void Render()
    {
        if (ActiveTrade == null || !TradeWindow.IsVisible)
            return;

        var rect = TradeWindow.GetClientRect();
        if (ActiveTrade.ExpectedValue > 0)
        {
            // draw total chaos value
            var chaosValue = Util.FormatChaosPrice(TotalChaosValue, DivinePrice);
            var expectedValue = Util.FormatChaosPrice(ActiveTrade.ExpectedValue, DivinePrice);
            var color = TotalChaosValue >= ActiveTrade.ExpectedValue * 0.99 ? Color.Green : Color.Red;
            AutoSextant.Instance.Graphics.DrawText($"Total Value: {chaosValue} / {expectedValue}", rect.TopLeft + new Vector2(0, 20), color, 20);
        }

        // add another row and say if we're waiting for more items
        var needToHover = ItemsLeftToHover;
        var text = needToHover ? "Waiting for more items" : "Ready to accept";
        var textColor = needToHover ? Color.Red : Color.Green;
        AutoSextant.Instance.Graphics.DrawText(text, rect.TopLeft + new Vector2(0, 40), textColor, 20);
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

    public static IEnumerator StashCurrency()
    {
        yield return AutoSextant.Instance.EnsureStash();
        var stashableCurrency = NInventory.Inventory.GetByName("Chaos Orb", "Divine Orb");
        Input.HoldCtrl();
        foreach (var item in stashableCurrency)
        {
            yield return Input.ClickElement(item.GetClientRect().Center);
            yield return new WaitTime(10);
        }
        Input.ReleaseCtrl();

        lock (ActiveTrade)
            ActiveTrade.Status = TradeRequestStatus.Done;
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
            HashSet<int> hoveredItems = new HashSet<int>();
            // In non-value mode we're just waiting for the trade to be accepted
            // mouse movement is not restricted, only taken over when new items are added
            while (ActiveTrade.Status == TradeRequestStatus.ItemsTransferred)
            {
                startingItems = OfferedItems.Count();
                if (hoveredItems.Count() == OfferedItems.Count() && ItemsLeftToHover)
                {
                    hoveredItems.Clear();
                }
                for (int i = 0; i < OfferedItems.Count(); i++)
                {
                    if (!hoveredItems.Contains(i))
                    {
                        yield return new WaitTime(10);
                        Input.SetCursorPos(OfferedItems[i].GetClientRect().Center);
                        yield return new WaitTime(10);
                        // Width is only intialized after the item is hovered
                        yield return new WaitFunctionTimed(() => OfferedItems[i].Tooltip.Width > 0, false, 100);
                        hoveredItems.Add(i);
                    }
                }
                yield return new WaitFunctionTimed(() => OfferedItems.Count() != startingItems, false, 500);
            }
            // one last check to make sure we didn't miss any items
            if (ItemsLeftToHover)
                yield return HoverTradeItems();
            // no need to do anything else in non-value mode, just return
            yield break;
        }

        Log.Debug("Hovering other player's items in value mode");


        while (TradeWindow is { IsVisible: true })
        {
            yield return new WaitTime(50);
            int itemCount = 0;
            HashSet<int> itemsHovered = new HashSet<int>();
            while (TotalChaosValue < ActiveTrade.ExpectedValue * 0.99 || ItemsLeftToHover)
            {
                itemCount = OfferedItems.Count();
                if (itemsHovered.Count() == OfferedItems.Count() && ItemsLeftToHover)
                {
                    itemsHovered.Clear();
                }
                for (int i = 0; i < OfferedItems.Count(); i++)
                {
                    if (!itemsHovered.Contains(i))
                    {
                        yield return new WaitTime(10);
                        Input.SetCursorPos(OfferedItems[i].GetClientRect().Center);
                        yield return new WaitTime(10);
                        // Width is only intialized after the item is hovered
                        yield return new WaitFunctionTimed(() => OfferedItems[i].Tooltip.Width > 0, false, 100);
                        itemsHovered.Add(i);
                    }
                }
                yield return new WaitTime(500);
            }
            if (ItemsLeftToHover)
                continue;
            yield return new WaitTime(50);
            if (!TradeWindow.SellerAccepted)
                yield return Input.ClickElement(TradeWindow.AcceptButton.GetClientRect().Center);
            yield return new WaitTime(50);
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
