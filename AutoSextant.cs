using System.Collections;
using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using SharpDX;
using ExileCore.Shared.Enums;
using System.Linq;
using ExileCore.PoEMemory.Elements;
using System;
using AutoSextant.PoEStack;

namespace AutoSextant;

public class AutoSextant : BaseSettingsPlugin<AutoSextantSettings>
{
    public Scheduler Scheduler = new Scheduler();
    internal static AutoSextant Instance;

    public override bool Initialise()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        InputAsync.RegisterKey(Settings.RestockHotkey);
        Settings.RestockHotkey.OnValueChanged += () => { InputAsync.RegisterKey(Settings.RestockHotkey); };
        InputAsync.RegisterKey(Settings.CleanInventoryHotKey);
        Settings.CleanInventoryHotKey.OnValueChanged += () => { InputAsync.RegisterKey(Settings.CleanInventoryHotKey); };
        InputAsync.RegisterKey(Settings.RunHotkey);
        Settings.RunHotkey.OnValueChanged += () => { InputAsync.RegisterKey(Settings.RunHotkey); };
        InputAsync.RegisterKey(Settings.CancelHotKey);
        Settings.RunHotkey.OnValueChanged += () => { InputAsync.RegisterKey(Settings.CancelHotKey); };

        Settings.UpdatePoeStackPrices.OnPressed += async () =>
        {
            Log.Debug("Updating PoeStack prices");
            var priceFetcher = new PoEStack.PriceFetcher();
            (Dictionary<string, PoeStackPrice>, Dictionary<string, PoeStackPrice>) result = await priceFetcher.Load(true);
            var Prices = result.Item1;
            var CurrencyPrices = result.Item2;
            // var (Prices, CurrencyPrices) = x.Result;
            CompassList.Prices.Clear();
            foreach (var price in Prices.Values)
            {
                CompassList.Prices.Add(price.Name, new CompassPrice
                {
                    Name = price.Name,
                    ChaosPrice = (int)price.Value,
                    DivinePrice = 0
                });
            }
            CompassList.DivinePrice = (float)CurrencyPrices["Divine Orb"].Value;
            CompassList.AwakenedSextantPrice = (float)CurrencyPrices["Awakened Sextant"].Value;
        };

        var priceFetcher = new PoEStack.PriceFetcher();
        var task = priceFetcher.Load();
        task.ContinueWith((x) =>
        {
            var result = x.Result;
            CompassList.Prices.Clear();
            foreach (var price in result.Item1.Values)
            {
                CompassList.Prices.Add(price.Name, new CompassPrice
                {
                    Name = price.Name,
                    ChaosPrice = (int)price.Value,
                    DivinePrice = 0
                });
            }
            CompassList.DivinePrice = (float)result.Item2["Divine Orb"].Value;
            CompassList.AwakenedSextantPrice = (float)result.Item2["Awakened Sextant"].Value;
        });

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        //Perform once-per-zone processing here
        //For example, Radar builds the zone map texture here

        // SellAssistant.SellAssistant.Enable();
        if (SellAssistant.SellAssistant.Enabled)
        {
            SellAssistant.SellAssistant.Disable();
        }
    }

    private string _restockCoroutineName = "AutoSextant_RestockCoroutine";
    private string _dumpCoroutineName = "AutoSextant_DumpCoroutine";
    private string _runCoroutineName = "AutoSextant_RunCoroutine";

    public bool IsAnyRoutineRunning
    {
        get
        {
            return Core.ParallelRunner.FindByName(_restockCoroutineName) != null ||
                   Core.ParallelRunner.FindByName(_dumpCoroutineName) != null ||
                   Core.ParallelRunner.FindByName(_runCoroutineName) != null ||
                   SellAssistant.SellAssistant.IsAnyRoutineRunning;
        }
    }

    public void StopAllRoutines(Action callback = null, bool clear = true)
    {
        Scheduler.Stop();
        if (clear)
            Scheduler.Clear();
        InputAsync.LOCK_CONTROLLER = false;
        InputAsync.IControllerEnd();
        SellAssistant.SellAssistant.StopAllRoutines();
        Scheduler.AddTask(ResetTask(callback), "ResetTask");
    }

    private static async SyncTask<bool> ResetTask(Action callback = null)
    {
        await InputAsync.ReleaseCtrl();
        await InputAsync.ReleaseShift();
        await Cursor.ReleaseItemOnCursorAsync(callback);
        return true;
    }

    public override Job Tick()
    {
        if (Settings.EnableSellAssistant && !SellAssistant.SellAssistant.Enabled)
            SellAssistant.SellAssistant.Enable();
        else if (!Settings.EnableSellAssistant && SellAssistant.SellAssistant.Enabled)
            SellAssistant.SellAssistant.Disable();


        if (Settings.CancelHotKey.PressedOnce())
        {
            StopAllRoutines();
            return null;
        }
        if (Settings.RestockHotkey.PressedOnce())
        {
            Settings.EnableSellAssistant.Value = true;
        }
        if (Settings.CleanInventoryHotKey.PressedOnce())
        {
            Scheduler.AddTask(CleanInventory(), "CleanInventory");
        }
        if (Settings.RunHotkey.PressedOnce())
        {
            Scheduler.AddTask(RunFromInventory(), "RunFromInventory");
        }

        SellAssistant.SellAssistant.Tick();
        return null;
    }

    public async SyncTask<bool> CleanInventory()
    {
        await EnsureStash();
        await Cursor.ReleaseItemOnCursorAsync();
        await Dump();
        await NStash.Stash.SelectTab(Settings.RestockSextantFrom);
        var stashableCurrency = NInventory.Inventory.GetByName(Item.ItemNames[ItemType.Sextant], Item.ItemNames[ItemType.Compass]);
        await InputAsync.HoldCtrl();
        foreach (var item in stashableCurrency)
        {
            await InputAsync.ClickElement(item.GetClientRect().Center);
        }
        await InputAsync.ReleaseCtrl();
        await Stock.Refresh();
        return true;
    }

    public async SyncTask<bool> OpenAtlasInInventoryMode()
    {
        if (GameController.IngameState.IngameUi.Atlas.IsVisible && GameController.IngameState.IngameUi.InventoryPanel.IsVisible && GameController.IngameState.IngameUi.StashElement.IsVisible)
        {
            return true;
        }
        await EnsureEverythingIsClosed();

        var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;

        foreach (LabelOnGround labelOnGround in itemsOnGround)
        {
            if (!labelOnGround.ItemOnGround.Path.Contains("/Stash"))
            {
                continue;
            }
            if (!labelOnGround.IsVisible)
            {
                Log.Error("Stash not visible");
                return false;
            }
            // click 150px to the right
            await InputAsync.ClickElement(labelOnGround.Label.GetClientRect().Center + new Vector2(350, 0));
        }

        await InputAsync.Wait(800);


        itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;

        foreach (LabelOnGround labelOnGround in itemsOnGround)
        {
            if (!labelOnGround.ItemOnGround.Path.Contains("/Stash"))
            {
                continue;
            }
            if (!labelOnGround.IsVisible)
            {
                Log.Error("Stash not visible");
                return false;
            }
            await InputAsync.ClickElement(labelOnGround.Label.GetClientRect().Center);
        }

        await InputAsync.KeyDown(Settings.AtlasHotKey.Value);
        await InputAsync.KeyUp(Settings.AtlasHotKey.Value);

        await InputAsync.Wait(() => GameController.IngameState.IngameUi.Atlas.IsVisible, 1000, "Atlas not opened");
        await InputAsync.Wait(30);

        return await InputAsync.Wait(() => GameController.IngameState.IngameUi.InventoryPanel != null && GameController.IngameState.IngameUi.InventoryPanel.IsVisible && GameController.IngameState.IngameUi.StashElement.IsVisible, 1000, "Inventory not opened");
    }

    public async SyncTask<bool> RunFromInventory()
    {
        InputAsync.LOCK_CONTROLLER = true;
        await Stock.Refresh();
        await OpenAtlasInInventoryMode();

        if (!Atlas.HasBlockMods)
        {
            LogError("No block mods");
            return false;
        }

        var stone = new VoidStone(VoidStonePosition.Top);
        var maxCharged = 8 * 5;

        var capacity = Stock.Capacity;
        var totalStock = Stock.Count;
        if (totalStock > capacity)
        {
            // TriggerCleanInventory();
            return false;
        }
        var maxCompassesThisSession = Math.Min(maxCharged, capacity - totalStock);

        var holdingShift = false;
        // TODO: attempt counter, then if too many, clear cursor and try again
        while (Inventory.TotalChargedCompasses < maxCompassesThisSession)
        {
            await NStash.Stash.SelectTab(Settings.RestockSextantFrom);
            var compassPrice = stone.Price;
            var currentName = compassPrice?.Name ?? null;

            bool capOk = true;
            int index, cap;
            string _;
            bool never, always;
            if (currentName != null)
            {
                index = Settings.ModSettings.FindIndex(x => x.Item1 == currentName);
                (_, never, always, cap) = index != -1 ? Settings.ModSettings[index] : (currentName, false, false, 0);
                capOk = cap > 0 ? Stock.Get(CompassList.PriceToModName[currentName]) < cap : true;
            }

            if (compassPrice == null || compassPrice.ChaosPrice < Settings.MinChaosValue || !capOk)
            {
                var nextSextant = Stash.NextSextant;
                if (nextSextant == null)
                {
                    // TriggerCleanInventory();
                    break;
                }
                if (!holdingShift)
                {
                    await InputAsync.UseItem(nextSextant.Position);
                    holdingShift = true;
                    await InputAsync.KeyDown(System.Windows.Forms.Keys.ShiftKey);
                }
                await InputAsync.ClickElement(stone.Position);
                SessionWindow.IncreaseSextantCount();
                await InputAsync.Wait(() => stone.Price != null && stone.Price.Name != currentName, 100);
                if (stone.Price == null || stone.Price.Name == currentName)
                {
                    holdingShift = false;
                    await InputAsync.KeyUp(System.Windows.Forms.Keys.ShiftKey);
                    await Cursor.ReleaseItemOnCursorAsync();
                    // Didn't work or was the same, try again
                    continue;
                }
            }

            compassPrice = stone.Price;

            index = Settings.ModSettings.FindIndex(x => x.Item1 == compassPrice.Name);
            (_, never, always, cap) = index != -1 ? Settings.ModSettings[index] : (compassPrice.Name, false, false, 0);

            capOk = cap > 0 ? Stock.Get(CompassList.PriceToModName[compassPrice.Name]) < cap : true;
            if ((!never && stone.Price != null && compassPrice.ChaosPrice >= Settings.MinChaosValue && capOk) || (always && capOk))
            {
                holdingShift = false;
                await InputAsync.KeyUp(System.Windows.Forms.Keys.ShiftKey);
                var nextCompass = Stash.NextCompass;
                var nextFreeSlot = Inventory.NextFreeChargedCompassSlot;
                if (nextCompass == null || nextFreeSlot == null)
                {
                    break;
                }
                while (Cursor.ItemName == null || Cursor.ItemName != "Charged Compass")
                {
                    await InputAsync.UseItem(nextCompass.Position);
                    await InputAsync.ClickElement(stone.Position);
                    await InputAsync.Wait(() => Cursor.ItemName != null && Cursor.ItemName == "Charged Compass", 100);
                }
                SessionWindow.AddMod(compassPrice.Name);
                var count = NInventory.Inventory.InventoryCount;
                while (NInventory.Inventory.InventoryCount == count || (Cursor.ItemName != null && Cursor.ItemName == "Charged Compass"))
                {
                    await InputAsync.ClickElement(nextFreeSlot.Position);
                    await InputAsync.Wait(() => NInventory.Inventory.InventoryCount != count || (Cursor.ItemName != null && Cursor.ItemName == "Charged Compass"), 100);
                }
            }
            await InputAsync.Wait();
            await InputAsync.Wait();
        }
        holdingShift = false;
        await InputAsync.KeyUp(System.Windows.Forms.Keys.ShiftKey);

        await Dump();

        await RunFromInventory();

        InputAsync.LOCK_CONTROLLER = false;
        InputAsync.IControllerEnd();
        return true;
    }

    public async SyncTask<bool> Dump(string specificMod = null, int? specifiedAmount = null, Action<int> callback = null)
    {
        await EnsureStash();

        var dumpTabs = Settings.DumpTabs.Value.Split(',');

        long leftToDump = specifiedAmount ?? 60;
        long dumped = 0;
        while ((specificMod == null ? Inventory.TotalChargedCompasses : NInventory.Inventory.GetByModName(specificMod).Count()) > 0)
        {
            foreach (var tabName in dumpTabs)
            {
                if (Stock.Tabs.ContainsKey(tabName) && Stock.Tabs[tabName].Values.Sum() >= 576)
                    continue;

                var items = specificMod == null ? Inventory.ChargedCompasses : NInventory.Inventory.GetByModName(specificMod).Select(x => new Item(x)).ToArray();
                items = items.OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y).ToArray();
                var tab = Stash.GetStashTabIndexForName(tabName);
                await NStash.Stash.SelectTab(tab);
                await InputAsync.Wait(30);
                var stashTabType = GameController.IngameState.IngameUi.StashElement.VisibleStash.InvType;
                var max = stashTabType == InventoryType.QuadStash ? 576 : 144;
                var count = GameController.IngameState.IngameUi.StashElement.VisibleStash.ItemCount;
                if (count >= max)
                {
                    continue;
                }
                var freeSlots = max - count;
                var toDump = Math.Min(Math.Min(freeSlots, items.Count()), leftToDump);
                await InputAsync.KeyDown(System.Windows.Forms.Keys.ControlKey);
                for (int i = 0; i < toDump; i++)
                {
                    await InputAsync.ClickElement(items[i].Position);
                }
                leftToDump -= toDump;
                dumped += toDump;
                await InputAsync.KeyUp(System.Windows.Forms.Keys.ControlKey);
                await InputAsync.Wait(30);
                if (toDump == items.Count() || leftToDump <= 0)
                {
                    callback?.Invoke((int)dumped);
                    return true;
                }
            }
        }
        callback?.Invoke((int)dumped);
        return true;
    }

    public async SyncTask<bool> EnsureStash()
    {
        if (GameController.IngameState.IngameUi.StashElement.IsVisible && GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
        {
            return true;
        }
        await EnsureEverythingIsClosed();

        var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;
        var stash = GameController.IngameState.IngameUi.StashElement;

        if (stash is { IsVisible: true })
        {
            return true;
        }

        foreach (LabelOnGround labelOnGround in itemsOnGround)
        {
            if (!(labelOnGround?.ItemOnGround?.Path?.Contains("/Stash") ?? true))
            {
                continue;
            }
            if (!labelOnGround.IsVisible)
            {
                Log.Error("Stash not visible");
                return false;
            }
            await InputAsync.ClickElement(labelOnGround.Label.GetClientRect().Center);
            await InputAsync.Wait(() => stash is { IsVisible: true }, 2000, "Stash not reached in time");
            if (stash is { IsVisible: false })
            {
                Log.Error("Stash not visible");
                return false;
            }
        }
        return true;
    }

    private async SyncTask<bool> EnsureEverythingIsClosed()
    {
        if (GameController.IngameState.IngameUi.Atlas.IsVisible)
        {
            await InputAsync.KeyDown(Settings.AtlasHotKey.Value);
            await InputAsync.KeyUp(Settings.AtlasHotKey.Value);
            await InputAsync.Wait(() => GameController.IngameState.IngameUi.Atlas.IsVisible, 1000, "Atlas not closed");
        }
        if (GameController.IngameState.IngameUi.InventoryPanel != null && GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
        {
            await InputAsync.KeyDown(Settings.InventoryHotKey.Value);
            await InputAsync.KeyUp(Settings.InventoryHotKey.Value);
            await InputAsync.Wait(() => !GameController.IngameState.IngameUi.InventoryPanel.IsVisible, 1000, "Inventory not closed");
        }
        if (GameController.IngameState.IngameUi.StashElement != null && GameController.IngameState.IngameUi.StashElement.IsVisible)
        {
            await InputAsync.KeyDown(System.Windows.Forms.Keys.Escape);
            await InputAsync.KeyUp(System.Windows.Forms.Keys.Escape);
            await InputAsync.Wait(() => !GameController.IngameState.IngameUi.StashElement.IsVisible, 1000, "Inventory not closed");
        }
        return true;
    }

    public override void Render()
    {
        Scheduler.Run();
        SessionWindow.Render();
        Error.Render();
        SellAssistant.SellAssistant.Render();
        if (Settings.PositionDebug.Value && GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
        {
            var free = Inventory.NextFreeChargedCompassSlot;
            if (free != null)
            {
                var newRect = new RectangleF(free.Position.X - 35, free.Position.Y - 35, 70, 70);
                Graphics.DrawFrame(newRect, Color.Green, 100f, 3, 0);
            }
        }
        var InnerAtlas = GameController.IngameState.IngameUi.Atlas.InnerAtlas;
        if (InnerAtlas.IsVisible)
        {
            VoidStone[] blockStones = {
                new(VoidStonePosition.Left),
                new(VoidStonePosition.Right),
                new(VoidStonePosition.Bottom)
            };

            foreach (var blockStone in blockStones)
            {
                var modName = blockStone.ModName;

                var isBlocked = modName != null &&
                                (Settings.UseModsForBlockingGroup1.Value.Contains(modName) ||
                                Settings.UseModsForBlockingGroup2.Value.Contains(modName) ||
                                Settings.UseModsForBlockingGroup3.Value.Contains(modName));

                var rect = blockStone.Slot.GetClientRect();
                // make new rect that is half the width and half the height and adjust the x and y to be in the center
                var newRect = new RectangleF(rect.X + rect.Width / 4, rect.Y + rect.Height / 4, rect.Width / 2,
                    rect.Height / 2);
                if (isBlocked)
                {
                    Graphics.DrawFrame(newRect, Color.Green, 100f, 3, 0);
                }
                else
                {
                    Graphics.DrawFrame(newRect, Color.Red, 100f, 3, 0);
                }
            }

            var rollingStone = new VoidStone(VoidStonePosition.Top);
            var compassPrice = rollingStone.Price;

            if (compassPrice != null)
            {
                string currentName = compassPrice.Name;
                var index = Settings.ModSettings.FindIndex(x => x.Item1 == currentName);
                var (_, never, always, cap) = index != -1 ? Settings.ModSettings[index] : (currentName, false, false, 0);
                var capOk = cap > 0 ? Stock.Get(CompassList.PriceToModName[currentName]) < cap : true;

                var chaosPrice = compassPrice.ChaosPrice;
                var color = !never && capOk && (chaosPrice >= Settings.MinChaosValue || (always && capOk)) ? Color.Green : Color.Red;
                Graphics.DrawFrame(rollingStone.Slot.GetClientRect(), color, 100f, 3, 0);
                var txt = $"{rollingStone.ClearName} - {chaosPrice} Chaos";
                if (never)
                    txt += " (Never)";
                else if (always && capOk)
                    txt += " (Always)";
                else if (!capOk)
                    txt += $" (Cap reached: {cap})";

                var textSize = Graphics.MeasureText(txt, 20);
                var textPos = new System.Numerics.Vector2
                {
                    X = rollingStone.Slot.GetClientRect().Center.X - textSize.X / 2,
                    Y = rollingStone.Slot.GetClientRect().Top - 20
                };
                Graphics.DrawText(txt, textPos, color, 21);
            }
            else
            {
                Graphics.DrawFrame(rollingStone.Slot.GetClientRect(), Color.Red, 100f, 3, 0);
            }

        }
    }

    public override void EntityAdded(Entity entity)
    {
        //If you have a reason to process every entity only once,
        //this is a good place to do so.
        //You may want to use a queue and run the actual
        //processing (if any) inside the Tick method.
    }
}