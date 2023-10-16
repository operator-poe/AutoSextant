using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;

namespace AutoSextant;

public class AutoSextantSettings : ISettings
{
    //Mandatory setting to allow enabling/disabling your plugin
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    //Put all your settings here if you can.
    //There's a bunch of ready-made setting nodes,
    //nested menu support and even custom callbacks are supported.
    //If you want to override DrawSettings instead, you better have a very good reason.

    [JsonIgnore]
    public CustomNode ModSettingsNode { get; set; }

    public AutoSextantSettings()
    {
        ExtraDelay = new RangeNode<int>(0, 0, 2000);

        // ModSettingsNode = new CustomNode
        // {
        //     DrawDelegate = () =>
        //     {
        // var modNames = new List<string>(CompassList.PriceToModName.Keys);
        // var modNames = new List<string>();

        // if (ImGui.BeginTable("AutoSextantSettingsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        // {
        //     ImGui.TableSetupColumn("Name");
        //     ImGui.TableSetupColumn("Always Skip");
        //     ImGui.TableSetupColumn("Always Take");
        //     ImGui.TableSetupColumn("Cap");
        //     ImGui.TableSetupColumn("Price");
        //     ImGui.TableHeadersRow();

        // foreach (var modName in modNames)
        // {
        //     ImGui.TableNextRow();
        //     ImGui.TableNextColumn();
        //     ImGui.Text(modName);

        //     ImGui.TableNextColumn();
        // var index = ModSettings.FindIndex(x => x.Item1 == modName);
        // var (_, never, always, cap) = index != -1 ? ModSettings[index] : (modName, false, false, 0);
        // if (ImGui.Checkbox($"##{modName}Never", ref never))
        // {
        //     if (index != -1)
        //         ModSettings[index] = (modName, never, always, cap);
        //     else
        //         ModSettings.Add((modName, never, always, cap));
        // }

        // ImGui.TableNextColumn();
        // if (ImGui.Checkbox($"##{modName}Always", ref always))
        // {
        //     if (index != -1)
        //         ModSettings[index] = (modName, never, always, cap);
        //     else
        //         ModSettings.Add((modName, never, always, cap));
        // }
        // ImGui.TableNextColumn();
        // if (ImGui.DragInt($"##{modName}Cap", ref cap, 1, 0, 100))
        // {
        //     if (index != -1)
        //         ModSettings[index] = (modName, never, always, cap);
        //     else
        //         ModSettings.Add((modName, never, always, cap));
        // }

        // ImGui.TableNextColumn();
        // if (CompassList.Prices != null)
        // {
        //     var p = CompassList.Prices.TryGetValue(modName, out var price) ? price.ChaosPrice : 0;

        //     if (p >= MinChaosValue.Value)
        //     {
        //         ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0, 1));
        //         ImGui.Text(p.ToString("0.0") + "c");
        //         ImGui.PopStyleColor();
        //     }
        //     else
        //     {
        //         ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
        //         ImGui.Text(p.ToString("0.0") + "c");
        //         ImGui.PopStyleColor();
        //     }
        // }
        // else
        // {
        //     ImGui.Text("-");
        // }
        // }

        //     ImGui.EndTable();
        // }
        //     }
        // };
    }

    [Menu("Extra Delay", "Delay to wait after each inventory clearing attempt(in ms).")]
    public RangeNode<int> ExtraDelay { get; set; }

    public HotkeyNode RestockHotkey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.F7);
    public HotkeyNode CancelHotKey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.Delete);
    public HotkeyNode DumpHotkey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.F8);
    public HotkeyNode RunHotkey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.F9);
    public HotkeyNode AtlasHotKey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.G);
    public HotkeyNode InventoryHotKey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.I);

    // TODO: Replace with list nodes
    public TextNode RestockSextantFrom { get; set; } = new TextNode();
    public RangeNode<int> MinChaosValue { get; set; } = new RangeNode<int>(6, 1, 300);

    public TextNode UseModsForBlockingGroup1 { get; set; } = new TextNode("MapAtlasChaosDamageAndPacks3,MapAtlasColdDamageAndPacks3,MapAtlasFireDamageAndPacks3,MapAtlasLightningDamageAndPacks3");
    public TextNode UseModsForBlockingGroup2 { get; set; } = new TextNode("MapAtlasInstantFlasksAndHealingMonsters3,MapAtlasMontersThatConvertOnDeath3");
    public TextNode UseModsForBlockingGroup3 { get; set; } = new TextNode("MapAtlasGloomShrineEffectAndDuration2,MapAtlasResonatingShrineEffectAndDuration2");

    public TextNode DumpTabs { get; set; } = new TextNode("CHARGED1,CHARGED2");
    public ToggleNode PositionDebug { get; set; } = new ToggleNode(false);

    public List<(string, bool, bool, int)> ModSettings { get; set; } = new List<(string, bool, bool, int)>(); // (Name, Never, Always, Cap)
}