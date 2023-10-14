using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared;
using ImGuiNET;

namespace AutoSextant.SellAssistant;

public static class SellAssistant
{
    private static bool _enabled = false;
    public static string selectedMod = "";
    public static int selectedAmount = 1;
    public static string selectedFilter = "";
    public static float priceMultiplier = 1.2f;

    public static PoeStackReport CurrentReport = null;

    private static AutoSextant I = AutoSextant.Instance;

    public static Dictionary<string, int> CompassCounts = new Dictionary<string, int>();
    private static (System.Numerics.Vector2, System.Numerics.Vector2) _windowPos = (System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero);

    public static readonly string _sellAssistantInitCoroutineName = "AutoSextant.SellAssistant.SellAssistant.Init";
    public static readonly string _sellAssistantTakeFromStashCoroutineName = "AutoSextant.SellAssistant.SellAssistant.TakeFromStash";
    public static bool IsAnyRoutineRunning
    {
        get
        {
            return
                Core.ParallelRunner.FindByName(_sellAssistantInitCoroutineName) != null ||
                Core.ParallelRunner.FindByName(_sellAssistantTakeFromStashCoroutineName) != null;
        }
    }
    public static void StopAllRoutines()
    {
        Core.ParallelRunner.FindByName(_sellAssistantInitCoroutineName)?.Done();
        Core.ParallelRunner.FindByName(_sellAssistantTakeFromStashCoroutineName)?.Done();
    }
    public static void Enable()
    {
        if (!NStash.Stash.IsVisible)
        {
            Error.AddAndShow("SellAssistant", "Stash is not visible");
            return;
        }
        Input.KeyUp(System.Windows.Forms.Keys.ControlKey);
        var inventoryRect = I.GameController.Game.IngameState.IngameUi.InventoryPanel.GetClientRect();
        _windowPos = (new System.Numerics.Vector2(inventoryRect.X, inventoryRect.Y),
        new System.Numerics.Vector2(inventoryRect.Width, inventoryRect.Height / 2));
        _enabled = true;
        Core.ParallelRunner.Run(new Coroutine(Init(), AutoSextant.Instance, _sellAssistantInitCoroutineName));
    }

    public static IEnumerator Init()
    {
        yield return Util.ForceFocus();
        var dumpTabs = I.Settings.DumpTabs.Value.Split(',').Select(x => x.Trim()).ToList();
        foreach (var t in dumpTabs)
        {
            yield return NStash.Stash.SelectTab(t);
            int TabIndex = NStash.Stash.TabIndex(t);
            yield return new WaitFunctionTimed(() =>
                 NStash.Ex.StashElement.AllInventories[TabIndex] != null
            , false, 500);
        }
        yield return new WaitTime(200);

        RefreshTable();
        yield break;

    }

    private static void RefreshTable()
    {
        selectedAmount = 1;
        selectedMod = "";
        CompassCounts.Clear();

        var dumpTabs = I.Settings.DumpTabs.Value.Split(',').Select(x => x.Trim()).ToList();
        var tabs = dumpTabs.Select(x => new NStash.StashTab(x)).ToList();
        foreach (var t in tabs)
        {
            foreach (var i in t.StashElement.VisibleInventoryItems)
            {
                if (i.Item.HasComponent<Mods>())
                {
                    var mods = i.Item.GetComponent<Mods>();
                    foreach (var m in mods.ItemMods)
                    {
                        if (I.CompassList.ModNameToPrice.ContainsKey(m.RawName))
                        {
                            if (!CompassCounts.ContainsKey(m.RawName))
                                CompassCounts.Add(m.RawName, 0);
                            CompassCounts[m.RawName]++;
                        }
                    }
                }
            }
        }
    }

    public static IEnumerator Highlight()
    {
        var tft = I.CompassList.ModNameToPrice[selectedMod];
        yield return Util.ForceFocus();
        Util.SetClipBoardText(tft);
        yield return new WaitFunctionTimed(() => Util.GetClipboardText() == tft, true, 1000, "Clipboard text not set");

        Input.KeyDown(Keys.ControlKey);
        yield return Input.KeyPress(Keys.F);
        Input.KeyUp(Keys.ControlKey);

        yield return new WaitTime(50);

        Input.KeyDown(Keys.ControlKey);
        yield return Input.KeyPress(Keys.V);
        Input.KeyUp(Keys.ControlKey);
    }

    public static IEnumerator TakeFromStash()
    {
        yield return Util.ForceFocus();

        Input.StorePosition();

        List<(string, SharpDX.Vector2)> items = new List<(string, SharpDX.Vector2)>();
        var dumpTabs = I.Settings.DumpTabs.Value.Split(',').Select(x => x.Trim()).ToList();
        var tabs = dumpTabs.Select(x => new NStash.StashTab(x)).ToList();

        foreach (var t in tabs)
        {
            foreach (var i in t.StashElement.VisibleInventoryItems)
            {
                if (i.Item.HasComponent<Mods>())
                {
                    var mods = i.Item.GetComponent<Mods>();
                    foreach (var m in mods.ItemMods)
                    {
                        if (m.RawName == selectedMod)
                        {
                            items.Add((t.Name, i.GetClientRect().Center));
                            break;
                        }
                    }
                }
            }
        }

        for (int i = 0; i < Math.Min(selectedAmount, 60); i++)
        {
            var item = items[i];
            yield return NStash.Stash.SelectTab(item.Item1);
            Input.KeyDown(Keys.ControlKey);
            yield return Input.ClickElement(item.Item2);
            Input.KeyUp(Keys.ControlKey);
            yield return new WaitTime(10);
        }

        RefreshTable();

        Input.RestorePosition();
    }

    public static void Tick()
    {
        if (!_enabled)
        {
            return;
        }
        WhisperManager.Tick();
    }

    public static void TakeFromStash(string mod, int amount)
    {
        selectedMod = mod;
        selectedAmount = amount;
        Core.ParallelRunner.Run(new Coroutine(TakeFromStash(), AutoSextant.Instance, _sellAssistantTakeFromStashCoroutineName));
    }

    public static void Render()
    {
        if (!_enabled)
        {
            return;
        }
        var show = _enabled;

        ImGui.SetNextWindowPos(_windowPos.Item1);
        ImGui.SetNextWindowSize(_windowPos.Item2);

        ImGui.Begin("AutoSextant SellAssistant", ref show);
        _enabled = show;


        ImGui.BeginChild("Top Pane", new System.Numerics.Vector2(-1, 120));
        if (CurrentReport != null)
        {
            if (ImGui.Button("Clear PoE Stack Report"))
            {
                CurrentReport = null;
            }
            ImGui.SameLine();
            if (ImGui.Button("Refresh PoE Stack Report"))
            {
                try
                {
                    var report = Util.GetClipboardText();
                    CurrentReport = new PoeStackReport(report);
                }
                catch (System.Exception e)
                {
                    Error.AddAndShow("SellAssistant", e.ToString());
                }

            }
        }
        else
        {
            ImGui.SliderFloat("Price Multiplier", ref priceMultiplier, 1, 2);
            ImGui.SameLine();
            if (ImGui.Button("Use PoE Stack Report"))
            {
                try
                {
                    var report = Util.GetClipboardText();
                    CurrentReport = new PoeStackReport(report);
                }
                catch (System.Exception e)
                {
                    Error.AddAndShow("SellAssistant", e.ToString());
                }
            }

        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (selectedMod != "")
        {
            ImGui.Text(I.CompassList.ModNameToPrice[selectedMod]);
            ImGui.SameLine();

            if (CurrentReport != null)
            {
                var priceString = CurrentReport.AmountToString(I.CompassList.ModNameToPrice[selectedMod], selectedAmount);
                ImGui.Text($" | {priceString}");
            }
            else
            {
                var price = I.CompassList.Prices[I.CompassList.ModNameToPrice[selectedMod]];
                var mPrice = (float)(price.DivinePrice > 1.0f ? price.DivinePrice : price.ChaosPrice) * selectedAmount * priceMultiplier;
                var priceString = price.DivinePrice > 1.0f ? mPrice.ToString("0.0") + " Divine" : mPrice.ToString("0.0") + " Chaos";
                ImGui.Text($" | {priceString}");
            }

            ImGui.InputInt("Enter Amount", ref selectedAmount, 1);
            ImGui.SliderInt("Select Amount", ref selectedAmount, 1, CompassCounts[selectedMod]);
            if (ImGui.Button("Take from stash"))
            {
                Core.ParallelRunner.Run(new Coroutine(TakeFromStash(), AutoSextant.Instance, _sellAssistantTakeFromStashCoroutineName));
            }
            ImGui.SameLine();
            if (ImGui.Button("Highlight"))
            {
                Core.ParallelRunner.Run(new Coroutine(Highlight(), AutoSextant.Instance, _sellAssistantTakeFromStashCoroutineName));
            }
        }

        ImGui.EndChild();

        ImGui.Separator();

        // Two panes side by side at the bottom
        float paneWidth1 = ImGui.GetContentRegionAvail().X * 0.35f;
        float paneWidth2 = ImGui.GetContentRegionAvail().X * 0.65f;

        ImGui.BeginChild("Left Pane", new System.Numerics.Vector2(paneWidth1, -1));
        ImGui.InputText("Filter", ref selectedFilter, 100);
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            selectedFilter = "";
        }
        ImGui.Spacing();

        float tableWidth = ImGui.GetContentRegionAvail().X;

        if (ImGui.BeginTable("SellAssistantTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, tableWidth * 0.1f);
            ImGui.TableSetupColumn("Total Value", ImGuiTableColumnFlags.WidthFixed, tableWidth * 0.3f);
            ImGui.TableHeadersRow();

            foreach (var c in AutoSextant.Instance.CompassList.PriceToModName)
            {
                var tft = c.Key;
                ImGui.PushID(tft);

                if (selectedFilter != "" && !tft.ToUpper().Contains(selectedFilter.ToUpper()))
                    continue;
                var mod = c.Value;
                if (!CompassCounts.ContainsKey(mod))
                    continue;

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var selected = selectedMod == mod;
                if (ImGui.Selectable(tft, ref selected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    selectedMod = mod;
                    selectedAmount = CompassCounts[mod];
                }

                ImGui.TableNextColumn();
                ImGui.Text(CompassCounts[mod].ToString());

                ImGui.TableNextColumn();
                if (CurrentReport != null)
                {
                    var price = CurrentReport.AmountToString(tft, CompassCounts[mod]);
                    if (price != null)
                        ImGui.Text(price);
                    else
                        ImGui.Text("-");
                }
                else
                {
                    var price = I.CompassList.Prices[tft];
                    var mPrice = (float)(price.DivinePrice > 1.0f ? price.DivinePrice : price.ChaosPrice) * CompassCounts[mod] * priceMultiplier;
                    var priceString = price.DivinePrice > 1.0f ? mPrice.ToString("0.0") + "d" : mPrice.ToString("0.0") + "c";
                    ImGui.Text(priceString);
                }

                ImGui.PopID();

            }

            ImGui.EndTable();
        }

        ImGui.EndChild();

        ImGui.SameLine(); // Put the next child window beside the previous one

        ImGui.BeginChild("Right Pane", new System.Numerics.Vector2(paneWidth2, -1));

        WhisperManager.Render();

        ImGui.EndChild();

        ImGui.End();
    }

}