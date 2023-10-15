using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace AutoSextant;

public class Inventory
{
  private static AutoSextant Instance = AutoSextant.Instance;
  private static ServerInventory ServerInventory
  {
    get
    {
      return Instance.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
    }
  }

  public static IList<InventSlotItem> InventoryItems
  {
    get
    {
      return ServerInventory.InventorySlotItems;
    }
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

  public static InventSlotItem[] GetColumn(int x)
  {
    var items = new InventSlotItem[5];
    for (int y = 0; y < 5; y++)
    {
      items[y] = GetSlotAt(x, y);
    }
    return items;
  }

  public static int CountItemsInColumn(int x)
  {
    // sum up item stacks in column
    var items = GetColumn(x);
    return items.Sum(x =>
    {
      if (x == null)
      {
        return 0;
      }

      return x.Item?.GetComponent<Stack>()?.Size ?? 0;
    });
  }

  public static InventSlotItem[] GetByName(string name)
  {
    var items = new List<InventSlotItem>();
    foreach (var item in InventoryItems)
    {
      if (item.Item != null && item.Item.GetComponent<Base>()?.Name == name)
      {
        items.Add(item);
      }
    }
    return items.ToArray();
  }

  public static Item[] ChargedCompasses
  {
    get
    {
      return GetByName("Charged Compass").Select(x => new Item(x)).ToArray();
    }
  }

  public static int TotalChargedCompasses
  {
    get
    {
      return ChargedCompasses.Count();
    }
  }

  public static Item NextCompass
  {
    get
    {
      var compasses = GetByName("Surveyor's Compass");
      try
      {
        return new Item(compasses.First());
      }
      catch
      {
        return null;
      }
    }
  }

  public static Item NextSextant
  {
    get
    {
      var sextants = GetByName("Awakened Sextant");
      try
      {
        return new Item(sextants.First());
      }
      catch
      {
        return null;
      }
    }
  }

  public static Item NextFreeChargedCompassSlot
  {
    get
    {
      var inventoryPanel = Instance.GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
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



      for (int x = 5; x < 12; x++)
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