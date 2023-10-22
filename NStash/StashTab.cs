using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;

namespace AutoSextant.NStash;
public class StashTab
{
    private int _index;

    public StashTab(int index)
    {
        _index = index;
    }

    public StashTab(string name)
    {
        _index = Ex.StashElement.AllStashNames.IndexOf(name);
    }

    public string Name
    {
        get
        {
            return Ex.StashElement.AllStashNames[_index];
        }
    }

    public bool IsSelected
    {
        get
        {
            return Ex.StashElement.IndexInParent == _index;
        }
    }

    public int Capacity
    {
        get
        {
            return (int)(Ex.StashElement.AllInventories[_index].TotalBoxesInInventoryRow * Ex.StashElement.AllInventories[_index].TotalBoxesInInventoryRow);
        }
    }

    public ExileCore.PoEMemory.MemoryObjects.Inventory StashElement
    {
        get
        {
            return Ex.StashElement.AllInventories[_index];
        }
    }
}