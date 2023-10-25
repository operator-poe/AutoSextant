using System;
using System.Windows.Forms;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;

namespace AutoSextant;

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

    public static System.Collections.IEnumerator ReleaseItemOnCursor(Action callback = null)
    {
        yield return Input.Delay();
        if (!HasItem)
        {
            callback?.Invoke();
            yield break;
        }

        Log.Debug($"Dumping '{ItemName}' x{StackSize} on cursor");
        if (ItemName == null)
        {
            while (HasItem)
            {
                yield return Input.Delay();
                Input.Click(MouseButtons.Right);
                yield return Input.Delay();
            }
        }
        else
        {
            while (HasItem)
            {
                if (ItemName == "Awakened Sextant" || ItemName == "Surveyor's Compass")
                {
                    yield return NStash.Stash.SelectTab(AutoSextant.Instance.Settings.RestockSextantFrom.Value);
                    if (ItemName == "Awakened Sextant")
                    {
                        var nextSextant = Stash.NextSextant;
                        yield return Input.ClickElement(nextSextant.Position);
                        yield return new WaitTime(30);
                    }
                    else if (ItemName == "Surveyor's Compass")
                    {
                        var nextSextant = Stash.NextCompass;
                        yield return Input.ClickElement(nextSextant.Position);
                        yield return new WaitTime(30);
                    }

                }
                else if (ItemName == "Charged Compass")
                {
                    var nextFreeSlot = Inventory.NextFreeChargedCompassSlot;
                    yield return Input.ClickElement(nextFreeSlot.Position);
                    yield return new WaitTime(30);
                }
            }
        }
        callback?.Invoke();
    }
}
