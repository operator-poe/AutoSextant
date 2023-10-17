using System.Collections.Generic;
using System.Data;
using System.Linq;
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

}
