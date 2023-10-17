using System.Collections.Generic;
using System.Data;
using System.Linq;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace AutoSextant.NInventory;

public static class Inventory
{
  private static ServerInventory ServerInventory
  {
    get
    {
      return AutoSextant.Instance.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
    }
  }

  public static IList<InventSlotItem> InventoryItems
  {
    get
    {
      return ServerInventory.InventorySlotItems;
    }
  }

  public static InventSlotItem[] GetByName(params string[] names)
  {
    var items = new List<InventSlotItem>();
    var nameSet = new HashSet<string>(names);

    foreach (var item in InventoryItems)
    {
      var baseComponent = item.Item?.GetComponent<Base>();
      if (baseComponent != null && nameSet.Contains(baseComponent.Name))
      {
        items.Add(item);
      }
    }

    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToArray();
  }

  public static InventSlotItem GetSlotAt(int x, int y)
  {
    foreach (var item in InventoryItems)
    {
      if (item.PosX == x && item.PosY == y)
      {
        return item;
      }
    }
    return null;
  }
  public static Item NextFreeInventoryPositinon
  {
    get
    {
      var inventoryPanel = AutoSextant.Instance.GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
      Vector2 inventoryPanelPosition = inventoryPanel.InventoryUIElement.GetClientRect().TopLeft;

      float cellSize = 0;
      for (int x = 0; x < 12; x++)
      {
        for (int y = 0; y < 5; y++)
        {
          var slot = GetSlotAt(x, y);
          if (slot != null && slot.Item != null)
          {
            cellSize = slot.GetClientRect().Height;
            break;
          }
        }
        if (cellSize != 0)
        {
          break;
        }
      }



      for (int x = 0; x < 12; x++)
      {
        for (int y = 0; y < 5; y++)
        {
          var slot = GetSlotAt(x, y);
          if (slot == null)
          {
            inventoryPanelPosition.X += cellSize * x + cellSize / 2f;
            inventoryPanelPosition.Y += cellSize * y + cellSize / 2f;
            return new Item(inventoryPanelPosition);
          }
        }
      }

      return null;
    }
  }
}
