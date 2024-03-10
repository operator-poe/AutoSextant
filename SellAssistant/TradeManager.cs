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
    GotChange,
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
    public float ReceivedValue { get; set; } = 0;
    public bool WithChange { get; set; } = false;
    public TradeRequestStatus Status { get; set; } = TradeRequestStatus.None;
    public Action<TradeRequest> Callback { get; set; } = null;
    public bool CallbackInvoked { get; set; } = false;
    public Dictionary<string, int> Items { get; set; }
}

public static class TradeManager
{
    private static AutoSextant Instance = AutoSextant.Instance;
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
    public static async void StopAllRoutines(bool clearQueue = true)
    {
        if (clearQueue)
            lock (TradeQueue)
                TradeQueue.Clear();
        if (ActiveTrade != null)
        {
            if (ActiveTrade.Callback != null && !ActiveTrade.CallbackInvoked)
                ActiveTrade.Callback?.Invoke(ActiveTrade);
            lock (ActiveTrade)
                ActiveTrade = null;
        }
        if (chatPointer != null)
            Chat.RemovePointer(chatPointer);
        if (chatPointer != null)
            lock (chatPointer)
                chatPointer = null;
        Core.ParallelRunner.FindByName(_tradeCoroutineName)?.Done();
        await InputAsync.ReleaseCtrl();
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

            if (ActiveTrade != null)
            {
                if (chatPointer == null)
                    chatPointer = Chat.GetPointer();
                if (Chat.CheckNewMessages(chatPointer, "Trade cancelled", false)) // Always interrupt if cancelled
                    ActiveTrade.Status = TradeRequestStatus.Cancelled;
                if (Chat.CheckNewMessages(chatPointer, "Player not found in this area", false))
                    ActiveTrade.Status = TradeRequestStatus.Cancelled;
                if (Chat.CheckNewMessages(chatPointer, "Trade accepted"))
                    ActiveTrade.Status = TradeRequestStatus.Accepted;

                Log.Debug($"Trade status: {ActiveTrade.Status}");
            }

            if (ActiveTrade != null && !coroutineRunning)
            {
                lock (ActiveTrade)
                {
                    switch (ActiveTrade.Status)
                    {
                        case TradeRequestStatus.None:
                            Log.Debug($"Sending trade request to {ActiveTrade.PlayerName}");
                            if (ActiveTrade.ExpectedValue > 0 && ActiveTrade.WithChange)
                                Instance.Scheduler.AddTask(GetChange(), "TradeManager.GetChange");
                            else
                                SendTradeRequest();
                            break;
                        case TradeRequestStatus.GotChange:
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
                            Instance.Scheduler.AddTask(TransferItems(), "TradeManager.TransferItems");
                            break;
                        case TradeRequestStatus.ItemsTransferred:
                            if (TradeWindow is { IsVisible: false })
                            {
                                Log.Debug("Trade window closed, but still on ItemsTransferred - breaking");
                                break;
                            }
                            Log.Debug("All items transferred, begin hovering items");
                            Instance.Scheduler.AddTask(HoverTradeItems(), "TradeManager.HoverTradeItems");
                            break;
                        case TradeRequestStatus.ValuesHovered:
                            break;
                        case TradeRequestStatus.Accepted:
                            Log.Debug($"Detected that trade with {ActiveTrade.PlayerName} has been accepted, stashing currency");
                            ActiveTrade.Callback?.Invoke(ActiveTrade);
                            Instance.Scheduler.AddTask(StashCurrency(), "TradeManager.StashCurrency");
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
            var expectedValue = Util.FormatChaosPrice(ActiveTrade.ExpectedValue + ChangeAmount, DivinePrice);
            var color = TotalChaosValue >= (ActiveTrade.ExpectedValue + ChangeAmount) * 0.99 ? Color.Green : Color.Red;
#pragma warning disable CS0612 // Type or member is obsolete
            AutoSextant.Instance.Graphics.DrawText($"Total Value: {chaosValue} / {expectedValue}", rect.TopLeft + new Vector2(0, 20), color, 20);
#pragma warning restore CS0612 // Type or member is obsolete
        }

        // add another row and say if we're waiting for more items
        var needToHover = ItemsLeftToHover;
        var text = needToHover ? "Waiting for more items" : "Ready to accept";
        var textColor = needToHover ? Color.Red : Color.Green;
#pragma warning disable CS0612 // Type or member is obsolete
        AutoSextant.Instance.Graphics.DrawText(text, rect.TopLeft + new Vector2(0, 40), textColor, 20);
#pragma warning restore CS0612 // Type or member is obsolete
    }

    private static IList<InventSlotItem> Compasses
    {
        get
        {
            if (ActiveTrade != null && ActiveTrade.WithChange)
            {
                var items = NInventory.Inventory.GetByModName(ActiveTrade.Items.Select(x => (x.Key, x.Value)).ToList()).ToList();
                items.AddRange(NInventory.Inventory.GetByName("Chaos Orb"));
                return items;
            }
            else
                return NInventory.Inventory.GetByModName(ActiveTrade.Items.Select(x => (x.Key, x.Value)).ToList());
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

    private static float ChangeAmount
    {
        get
        {
            if (ActiveTrade.ExpectedValue <= 0 || !ActiveTrade.WithChange)
                return 0;
            else
            {
                float remaining = DivinePrice - (ActiveTrade.ExpectedValue % DivinePrice);
                return remaining == DivinePrice ? 0 : remaining;
            }
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

    private static int ChaosOrbsInInventory
    {
        get
        {
            return NInventory.Inventory.GetByName("Chaos Orb").Sum(item => item.Item.GetComponent<ExileCore.PoEMemory.Components.Stack>().Size);
        }
    }

    public static async SyncTask<bool> GetChange()
    {
        await Util.ForceFocusAsync();
        await AutoSextant.Instance.EnsureStash();
        await NStash.Stash.SelectTab(AutoSextant.Instance.Settings.RestockSextantFrom.Value);
        var chaosOrbs = Stash.GetFirstItemTypeFromStash("Chaos Orb");
        var chaosStack = new Item(chaosOrbs);
        var change = (int)ChangeAmount;

        while (ChaosOrbsInInventory < change)
        {
            var chaosLeft = change - ChaosOrbsInInventory;
            if (chaosLeft < 20)
            {
                var position = NInventory.Inventory.NextFreeInventoryPositinon.Position;
                await chaosStack.GetFraction(chaosLeft);
                await InputAsync.ClickElement(position);
            }
            else
            {
                await chaosStack.GetStack(true);
            }
            await InputAsync.Wait();
        }

        lock (ActiveTrade)
            ActiveTrade.Status = TradeRequestStatus.GotChange;
        return true;
    }

    public static async SyncTask<bool> StashCurrency()
    {
        await AutoSextant.Instance.EnsureStash();
        var stashableCurrency = NInventory.Inventory.GetByName("Chaos Orb", "Divine Orb");
        await InputAsync.HoldCtrl();
        foreach (var item in stashableCurrency)
        {
            await InputAsync.ClickElement(item.GetClientRect().Center);
            await InputAsync.Wait();
        }
        await InputAsync.ReleaseCtrl();

        lock (ActiveTrade)
            ActiveTrade.Status = TradeRequestStatus.Done;
        return true;
    }

    public static async SyncTask<bool> HoverTradeItems()
    {
        bool valueMode = false;
        float divinePrice = 0;
        if (SellAssistant.CurrentReport != null)
        {
            valueMode = ActiveTrade.ExpectedValue > 0;
            divinePrice = SellAssistant.CurrentReport.DivinePrice;
        }

        Log.Debug("Hovering other player's items in value mode");
        Log.Debug("Expected value: " + ActiveTrade.ExpectedValue);

        while (TradeWindow is { IsVisible: true })
        {
            Log.Debug("In outer loop");
            await InputAsync.Wait();
            if (ActiveTrade.ExpectedValue > 0)
            {
                HashSet<int> itemsHovered = new HashSet<int>();
                while (TotalChaosValue < (ActiveTrade.ExpectedValue + ChangeAmount) * 0.99 || ItemsLeftToHover)
                {
                    Log.Debug("In inner loop");
                    if (TradeWindow is { IsVisible: false })
                    {
                        break;
                    }
                    if (itemsHovered.Count() == OfferedItems.Count() && ItemsLeftToHover)
                    {
                        itemsHovered.Clear();
                    }
                    for (int i = 0; i < OfferedItems.Count(); i++)
                    {
                        if (!itemsHovered.Contains(i))
                        {
                            await InputAsync.Wait();
                            await InputAsync.MoveMouseToElement(OfferedItems[i].GetClientRect().Center);
                            await InputAsync.Wait();
                            // Width is only intialized after the item is hovered
                            await InputAsync.Wait(() => OfferedItems[i].Tooltip.Width > 0, 100);
                            itemsHovered.Add(i);
                        }
                    }
                    ActiveTrade.ReceivedValue = TotalChaosValue;
                    await InputAsync.Wait(500);
                }
                if (ItemsLeftToHover)
                    continue;
                await InputAsync.Wait();
            }
            else
            {
                Log.Debug("Expected value is 0, skipping hovering");
            }
            if (TradeWindow is { IsVisible: false })
            {
                Log.Debug("Trade window closed, breaking");
                break;
            }
            if (!TradeWindow.SellerAccepted)
            {
                Log.Debug("Accept trade");
                ActiveTrade.ReceivedValue = TotalChaosValue;
                await InputAsync.ClickElement(TradeWindow.AcceptButton.GetClientRect().Center);
            }
            await InputAsync.Wait(200);
        }
        return true;
    }

    public static IList<NormalInventoryItem> YourOffer
    {
        get
        {
            return TradeWindow.YourOffer;
            // return TradeWindow.YourOffer.ToList().Where(x => x?.Text != "Place items you want to trade here").ToList();
        }
    }

    public static async SyncTask<bool> TransferItems()
    {
        await Util.ForceFocusAsync();
        int startingItems = CompassCount;
        Log.Debug($"Transferring {startingItems} compasses to {ActiveTrade.PlayerName}");

        await InputAsync.HoldCtrl();
        var attempts = 0;
        while (YourOffer.Count() < startingItems)
        {
            attempts++;
            Log.Debug($"Attempt {attempts} to transfer compasses");
            if (attempts > 3)
            {
                Log.Error("Failed to transfer compasses");
                return false;
            }
            foreach (var item in Compasses)
            {
                var oldCount = YourOffer.Count();

                await InputAsync.ClickElement(item.GetClientRect().Center);

                await InputAsync.Wait(() => YourOffer.Count() > oldCount, 50, "Compass not transferred");
            }
            await InputAsync.Wait();
        }
        if (startingItems == 1) // Since youroffer label "place items" is counted as an item run this manually
        {
            foreach (var item in Compasses)
            {
                var oldCount = YourOffer.Count();

                await InputAsync.ClickElement(item.GetClientRect().Center);

                await InputAsync.Wait(() => YourOffer.Count() > oldCount, 50, "Compass not transferred");
            }
        }
        await InputAsync.ReleaseCtrl();

        ActiveTrade.Status = TradeRequestStatus.ItemsTransferred;

        return true;
    }

    public static void SendTradeRequest()
    {
        Chat.QueueMessage("/tradewith " + ActiveTrade.PlayerName);
        ActiveTrade.Status = TradeRequestStatus.RequestMade;
    }
}
