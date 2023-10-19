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
    public static bool Enabled
    {
        get
        {
            return _enabled;
        }
    }
    public static string selectedMod = "";
    public static int selectedAmount = 1;
    public static bool selectedFocus = false;
    public static string selectedFilter = "";
    private static List<Action> _executeOnNextTick = new List<Action>();
    public static void ExecuteOnNextTick(Action action)
    {
        lock (_executeOnNextTick)
            _executeOnNextTick.Add(action);
    }

    private static PoeStackReport _currentReport = null;
    private static DateTime _lastReportRefresh = DateTime.MinValue;
    public static PoeStackReport CurrentReport
    {
        get
        {
            if (_currentReport == null || _lastReportRefresh < PoeStackReport.LastModified)
            {
                _currentReport = PoeStackReport.CreateFromFile();
                _lastReportRefresh = PoeStackReport.LastModified;
            }
            return _currentReport;
        }
        set
        {
            _currentReport = value;
        }
    }

    private static AutoSextant I = AutoSextant.Instance;

    public static Dictionary<string, int> CompassCounts = new Dictionary<string, int>();
    private static (System.Numerics.Vector2, System.Numerics.Vector2) _windowPos = (System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero);

    public static readonly string _sellAssistantInitCoroutineName = "AutoSextant.SellAssistant.SellAssistant.Init";
    public static readonly string _sellAssistantTakeFromStashCoroutineName = "AutoSextant.SellAssistant.SellAssistant.TakeFromStash";
    private static List<(string, int)> ExtractionQueue = new List<(string, int)>();
    public static bool IsAnyRoutineRunning
    {
        get
        {
            return
                ExtractionQueue.Count > 0 ||
                Chat.IsAnyRoutineRunning ||
                TradeManager.IsAnyRoutineRunning ||
                Core.ParallelRunner.FindByName(_sellAssistantInitCoroutineName) != null ||
                Core.ParallelRunner.FindByName(_sellAssistantTakeFromStashCoroutineName) != null;
        }
    }
    public static void StopAllRoutines()
    {
        ExtractionQueue.Clear();
        Core.ParallelRunner.FindByName(_sellAssistantInitCoroutineName)?.Done();
        Core.ParallelRunner.FindByName(_sellAssistantTakeFromStashCoroutineName)?.Done();
        Chat.StopAllRoutines();
        TradeManager.StopAllRoutines();
    }

    public static void AddToExtractionQueue(string mod, int amount)
    {
        ExtractionQueue.Add((mod, amount));
    }
    public static void AddToExtractionQueue(List<(string, int)> mods)
    {
        ExtractionQueue.AddRange(mods);
    }

    public static void Enable()
    {
        var inventoryRect = I.GameController.Game.IngameState.IngameUi.InventoryPanel.GetClientRect();
        _windowPos = (new System.Numerics.Vector2(inventoryRect.X, inventoryRect.Y), new System.Numerics.Vector2(inventoryRect.Width, inventoryRect.Height / 2));

        _enabled = true;
        Core.ParallelRunner.Run(new Coroutine(Init(), AutoSextant.Instance, _sellAssistantInitCoroutineName));
    }

    public static void Disable()
    {
        selectedAmount = 1;
        selectedMod = "";
        _windowPos = (System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero);
        selectedFilter = "";
        CurrentReport = null;
        CompassCounts.Clear();
        StopAllRoutines();
        WhisperManager.Whispers.Clear();
        _enabled = false;
    }

    public static IEnumerator Init()
    {
        yield return Util.ForceFocus();
        yield return AutoSextant.Instance.EnsureStash();
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
        lock (CompassCounts)
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
                        if (CompassList.ModNameToPrice.ContainsKey(m.RawName))
                        {
                            if (!CompassCounts.ContainsKey(m.RawName))
                                CompassCounts.Add(m.RawName, 0);
                            CompassCounts[m.RawName]++;
                        }
                    }
                }
            }
        }

        if (selectedMod != "" && !CompassCounts.ContainsKey(selectedMod))
        {
            selectedMod = "";
        }
        else if (selectedMod != "")
        {
            selectedAmount = CompassCounts[selectedMod];
        }
    }

    public static IEnumerator Highlight()
    {
        yield return Util.ForceFocus();
        yield return AutoSextant.Instance.EnsureStash();
        var tft = CompassList.ModNameToPrice[selectedMod];
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

    public static IEnumerator TakeFromStash(string mod = null, int? amount = null)
    {
        yield return Util.ForceFocus();
        yield return AutoSextant.Instance.EnsureStash();

        mod ??= selectedMod;
        amount ??= selectedAmount;
        amount = Math.Min(amount.Value, 60);

        Input.StorePosition();

        List<(string, SharpDX.Vector2)> items = new List<(string, SharpDX.Vector2)>();
        var dumpTabs = I.Settings.DumpTabs.Value.Split(',').Select(x => x.Trim()).ToList();
        var tabs = dumpTabs.Select(x => new NStash.StashTab(x)).ToList();

        foreach (var t in tabs)
        {
            if (items.Count >= amount)
                break;
            foreach (var i in t.StashElement.VisibleInventoryItems)
            {
                if (items.Count >= amount)
                    break;
                if (i.Item.HasComponent<Mods>())
                {
                    var mods = i.Item.GetComponent<Mods>();
                    foreach (var m in mods.ItemMods)
                    {
                        if (m.RawName == mod)
                        {
                            items.Add((t.Name, i.GetClientRect().Center));
                            break;
                        }
                    }
                }
            }
        }

        for (int i = 0; i < amount; i++)
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
        _executeOnNextTick.ForEach(x => x.Invoke());
        _executeOnNextTick.Clear();
        if (!_enabled)
        {
            return;
        }
        WhisperManager.Tick();
        Chat.Tick();
        TradeManager.Tick();
        PoeStackReport.CheckClipboardForReport();

        if (ExtractionQueue.Count > 0 && Core.ParallelRunner.FindByName(_sellAssistantTakeFromStashCoroutineName) == null)
        {
            var (mod, amount) = ExtractionQueue[0];
            ExtractionQueue.RemoveAt(0);
            Core.ParallelRunner.Run(new Coroutine(TakeFromStash(mod, amount), AutoSextant.Instance, _sellAssistantTakeFromStashCoroutineName));
        }
    }

    public static void SelectAndTakeFromStash(string mod, int amount)
    {
        ExecuteOnNextTick(() =>
        {
            selectedMod = mod;
            selectedAmount = amount;
            Core.ParallelRunner.Run(new Coroutine(TakeFromStash(), AutoSextant.Instance, _sellAssistantTakeFromStashCoroutineName));
        });
    }

    private static bool _isAmountFocused = false;
    public static bool IsAmountFocussed
    {
        get
        {
            return _isAmountFocused;
        }
    }

    public static void Render()
    {
        if (!_enabled)
        {
            return;
        }
        TradeManager.Render();
        var show = _enabled;

        ImGui.Begin("AutoSextant SellAssistant", ref show);
        _enabled = show;

        ImGui.BeginChild("Top Pane", new System.Numerics.Vector2(-1, 120));
        if (CurrentReport != null)
        {
            ImGui.Text("PoE Stack Report last updated: ");
            ImGui.SameLine();
            ImGui.Text(Util.FormatTimeSpan(DateTime.Now - _lastReportRefresh) + " ago");
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No PoE Stack Report loaded, please copy one to your clipboard");
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (selectedMod != "")
        {
            ImGui.Text(CompassList.ModNameToPrice[selectedMod]);
            ImGui.SameLine();

            if (CurrentReport != null)
            {
                var priceString = CurrentReport.AmountToString(CompassList.ModNameToPrice[selectedMod], selectedAmount);
                ImGui.Text($" | {priceString}");
            }
            else
            {
                var price = CompassList.Prices[CompassList.ModNameToPrice[selectedMod]];
                var priceString = Util.FormatChaosPrice((float)(price.DivinePrice > 1.0f ? price.DivinePrice : price.ChaosPrice) * selectedAmount, price.DivinePrice);
                ImGui.Text($" | {priceString}");
            }

            if (selectedFocus)
            {
                ImGui.SetKeyboardFocusHere();
                selectedFocus = false;
            }
            ImGui.InputInt("Enter Amount", ref selectedAmount, 1);
            if (!_isAmountFocused && ImGui.IsItemFocused())
                _isAmountFocused = true;
            else if (_isAmountFocused && !ImGui.IsItemFocused())
                _isAmountFocused = false;

            ImGui.SliderInt("Select Amount", ref selectedAmount, 1, CompassCounts[selectedMod]);
            if (ImGui.Button("Take from stash"))
            {
                ExecuteOnNextTick(() => Core.ParallelRunner.Run(new Coroutine(TakeFromStash(), AutoSextant.Instance, _sellAssistantTakeFromStashCoroutineName)));
            }
            ImGui.SameLine();
            if (ImGui.Button("Highlight"))
            {
                ExecuteOnNextTick(() => Core.ParallelRunner.Run(new Coroutine(Highlight(), AutoSextant.Instance, _sellAssistantTakeFromStashCoroutineName)));
            }
            ImGui.SameLine();
            ImGui.Text("Add to: ");
            ImGui.SameLine();
            var whispers = WhisperManager.ActiveWhispers;
            for (int i = 0; i < whispers.Count && i < 3; i++)
            {
                var whisper = whispers[i];
                if (ImGui.Button((i + 1).ToString()))
                {
                    ExecuteOnNextTick(() =>
                    {
                        WhisperManager.ExecuteOnNextTick(() =>
                        {
                            whisper.AddItem(new WhisperItem
                            {
                                Name = CompassList.ModNameToPrice[selectedMod],
                                ModName = selectedMod,
                                Quantity = selectedAmount
                            });
                        });
                    });
                }
                ImGui.SameLine();
            }
            if (WhisperManager.HotKeySelected != "")
            {
                if (ImGui.Button("Selected"))
                {
                    ExecuteOnNextTick(() =>
                    {
                        WhisperManager.ExecuteOnNextTick(() =>
                        {
                            WhisperManager.Whispers.Find(x => x.Uuid == WhisperManager.HotKeySelected).AddItem(new WhisperItem
                            {
                                Name = CompassList.ModNameToPrice[selectedMod],
                                ModName = selectedMod,
                                Quantity = selectedAmount
                            });
                        });
                    });
                }
            }
        }

        ImGui.EndChild();

        ImGui.Separator();

        // Two panes side by side at the bottom
        float paneWidth1 = ImGui.GetContentRegionAvail().X * 0.35f;
        float paneWidth2 = ImGui.GetContentRegionAvail().X * 0.65f;

        ImGui.BeginChild("Left Pane", new System.Numerics.Vector2(paneWidth1, -1));
        ImGui.InputText("Filter", ref selectedFilter, 50);
        ImGui.SameLine();
        if (ImGui.Button("C"))
            ExecuteOnNextTick(() => { selectedFilter = ""; });
        ImGui.SameLine();
        if (ImGui.Button("R"))
            ExecuteOnNextTick(Enable);
        ImGui.Spacing();

        float tableWidth = ImGui.GetContentRegionAvail().X;

        if (ImGui.BeginTable("SellAssistantTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, tableWidth * 0.1f);
            ImGui.TableSetupColumn("Total Value", ImGuiTableColumnFlags.WidthFixed, tableWidth * 0.3f);
            ImGui.TableHeadersRow();

            foreach (var c in CompassList.PriceToModName)
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
                    ExecuteOnNextTick(() =>
                    {
                        selectedMod = mod;
                        selectedAmount = CompassCounts[mod];
                        selectedFocus = true;
                    });
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
                    var price = CompassList.Prices[tft];
                    var priceString = Util.FormatChaosPrice((float)(price.DivinePrice > 1.0f ? price.DivinePrice : price.ChaosPrice) * CompassCounts[mod], price.DivinePrice);
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