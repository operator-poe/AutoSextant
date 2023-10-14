using System.Linq;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace AutoSextant;

public class AutoSextantSettings : ISettings
{
    //Mandatory setting to allow enabling/disabling your plugin
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    //Put all your settings here if you can.
    //There's a bunch of ready-made setting nodes,
    //nested menu support and even custom callbacks are supported.
    //If you want to override DrawSettings instead, you better have a very good reason.

    public AutoSextantSettings()
    {
        ExtraDelay = new RangeNode<int>(0, 0, 2000);
    }

    [Menu("Extra Delay", "Delay to wait after each inventory clearing attempt(in ms).")]
    public RangeNode<int> ExtraDelay { get; set; }

    public HotkeyNode RestockHotkey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.F7);
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
}