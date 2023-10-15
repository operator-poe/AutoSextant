using ExileCore.PoEMemory.Elements.InventoryElements;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using SharpDX;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using ExileCore.Shared;

namespace AutoSextant;

public enum ItemType
{
  Sextant,
  Compass,
  ChargedCompass
}

public class Item
{
  public static Dictionary<ItemType, string> ItemNames = new Dictionary<ItemType, string>()
  {
    { ItemType.Sextant, "Awakened Sextant" },
    { ItemType.Compass, "Surveyor's Compass" },
    { ItemType.ChargedCompass, "Charged Compass" }
  };

  private static AutoSextant Instance = AutoSextant.Instance;
  public Vector2 Position { get; set; }

  public Item(NormalInventoryItem item)
  {
    Position = item.GetClientRect().Center;
  }

  public Item(InventSlotItem item)
  {
    Position = item.GetClientRect().Center;
  }

  public Item(Vector2 position)
  {
    Position = position;
  }

  public IEnumerator Hover()
  {
    Input.MoveMouseToElement(Position);
    yield return Input.Delay(30);
  }

  public IEnumerator GetStack(bool CtrlClick = false)
  {
    if (CtrlClick)
    {
      Input.KeyDown(Keys.ControlKey);
    }
    yield return Input.ClickElement(Position, 10);
    if (CtrlClick)
    {
      Input.KeyUp(Keys.ControlKey);
    }
  }

  public IEnumerator GetFraction(int num)
  {
    yield return Hover();
    Input.KeyDown(Keys.ShiftKey);
    Input.Click(MouseButtons.Left);
    Input.KeyUp(Keys.ShiftKey);
    yield return new WaitFunctionTimed(() => Instance.GameController.IngameState.IngameUi.GetChildAtIndex(149) is { IsVisible: true }, true, 1000, "Split window not opened");
    var numAsString = num.ToString();

    // iterate each number and send the key
    foreach (var c in numAsString)
    {
      var key = (Keys)c;
      Input.KeyDown(key);
      Input.KeyUp(key);
      yield return Input.Delay(20);
    }
    yield return Input.Delay(20);
    Input.KeyDown(Keys.Enter);
    Input.KeyUp(Keys.Enter);
    yield return Input.Delay(20);
  }
}
