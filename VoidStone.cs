using System.Collections.Generic;
using System.ComponentModel;
using ExileCore.PoEMemory.Elements.AtlasElements;
using SharpDX;

namespace AutoSextant;

public enum VoidStonePosition
{
  Left,
  Right,
  Bottom,
  Top
}

public class VoidStone
{
  private static AutoSextant Instance = AutoSextant.Instance;
  private VoidStonePosition _position;

  public VoidStone(VoidStonePosition position)
  {
    _position = position;
  }

  public VoidStoneSlot Slot
  {
    get
    {
      var InnerAtlas = Instance.GameController.IngameState.IngameUi.Atlas.InnerAtlas;
      if (!InnerAtlas.IsVisible)
      {
        return null;
      }
      var socketedStones = InnerAtlas.GetChildFromIndices(125, 0).AsObject<VoidStoneInventory>();
      switch (_position)
      {
        case VoidStonePosition.Left:
          return socketedStones.LeftSlot;
        case VoidStonePosition.Right:
          return socketedStones.RightSlot;
        case VoidStonePosition.Bottom:
          return socketedStones.BottomSlot;
        case VoidStonePosition.Top:
          return socketedStones.TopSlot;
        default:
          return null;
      }
    }
  }

  public Vector2 Position
  {
    get
    {
      return Slot.GetClientRect().Center;
    }
  }

  public string ModName
  {
    get
    {
      string name = null;
      try
      {
        name = Slot?.SextantMod?.RawName;
      }
      catch
      {
      }
      return name;
    }
  }

  public string ClearName
  {
    get
    {
      var name = ModName;
      if (name == null)
        return null;
      if (name.Contains("Barrel"))
        return "Barrel";
      if (!Instance.CompassList.ModNameToPrice.ContainsKey(name))
        return null;
      return Instance.CompassList.ModNameToPrice.GetValueOrDefault(name, null);
    }
  }

  public CompassPrice Price
  {
    get
    {
      var name = ModName;
      if (name == null)
      {
        return null;
      }
      if (name.Contains("Barrel"))
      {
        return new CompassPrice
        {
          Name = "Barrel",
          ChaosPrice = 0
        };
      }
      if (!Instance.CompassList.ModNameToPrice.ContainsKey(name))
      {
        return null;
      }
      var compassName = Instance.CompassList.ModNameToPrice.GetValueOrDefault(name, null);
      if (!Instance.CompassList.Prices.ContainsKey(compassName))
      {
        Error.AddAndShow("FATAL", $"No price found for {compassName}");
        return null;
      }
      var compassPrice = Instance.CompassList.Prices.GetValueOrDefault(compassName, null);
      return compassPrice;
    }
  }
}