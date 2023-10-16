using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
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
    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToArray();
  }
}
