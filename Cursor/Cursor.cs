using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace AutoSextant.Cursor;

// If HasItem, but name is null it's a right click which we can't identify
public static class Cursor
{
    public static Element Element
    {
        get
        {
            return AutoSextant.Instance.GameController.Game.IngameState.IngameUi.Cursor;
        }
    }

    public static bool HasItem
    {
        get
        {
            return Element.ChildCount > 0;
        }
    }

    public static Entity ItemEntity
    {
        get
        {
            if (!HasItem)
                return null;
            return Element.GetChildAtIndex(0).Entity;
        }
    }

    public static Base ItemBase
    {
        get
        {
            return ItemEntity?.GetComponent<Base>();
        }
    }

    public static Stack Stack
    {
        get
        {
            return ItemEntity?.GetComponent<Stack>();
        }
    }

    public static string ItemName
    {
        get
        {
            return ItemBase?.Name;
        }
    }

    public static int StackSize
    {
        get
        {
            return Stack?.Size ?? 0;
        }
    }
}
