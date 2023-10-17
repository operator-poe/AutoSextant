using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AutoSextant.SellAssistant;

public class WhisperItem
{
    public string Name { get; set; }
    public string ModName { get; set; }
    public int Quantity { get; set; }

    public FullfillmentStatus Status
    {
        get
        {
            if (!SellAssistant.CompassCounts.ContainsKey(ModName))
                return FullfillmentStatus.NotAvailable;
            if (Quantity > SellAssistant.CompassCounts[ModName])
                return FullfillmentStatus.NotEnough;
            return FullfillmentStatus.Available;
        }
    }

    public float Price
    {
        get
        {
            if (SellAssistant.CurrentReport != null && SellAssistant.CurrentReport.Prices.TryGetValue(Name, out var price))
                return price * Quantity;
            else
                return CompassList.Prices[Name].ChaosPrice * Quantity;
        }
    }

    public string PriceFormatted
    {
        get
        {
            if (SellAssistant.CurrentReport != null)
                return Util.FormatChaosPrice(Price, SellAssistant.CurrentReport.DivinePrice);
            else
                return Util.FormatChaosPrice(Price);
        }
    }
}
public class Whisper
{
    public static readonly List<(string, (int, int))> WhisperPatterns = new List<(string, (int, int))>{
            (@"wtb(?: ?([0-9]+) ([\w\s-]+) ([\d\.]+[a-z]*)(?: each)?,?)+", (1,2)), // official poestack whisper
            (@"wtb +([a-z8 -]*) +x?([0-9]+)x?",(2,1)),
            (@"([0-9]+)x ([a-z8 -]+)", (1,2)),
            (@"(\b[a-z]+\b(?:\s+\b[a-z]+\b)*) all", (1,2)),
            (@"([0-9]+) ([a-z8 -]+)", (1,2)),
            (@"([a-z8 ]+) ([0-9]+)", (2,1))
        };

    public string PlayerName { get; set; }
    public string Message { get; set; }
    public string Uuid { get; set; }

    public List<WhisperItem> Items { get; set; } = new List<WhisperItem>();

    public bool MultiItem
    {
        get
        {
            return Items.Count > 1;
        }
    }
    public WhisperItem Item
    {
        get
        {
            return Items[0];
        }
    }

    public bool Hidden { get; set; } = false;

    public bool InArea { get; set; } = false;

    public bool HasSentInvite { get; set; } = false;
    public bool HasSentPartial { get; set; } = false;
    public bool HasExtracted { get; set; } = false;
    public bool HasTraded { get; set; } = false;

    public float TotalPrice
    {
        get
        {
            return Items.Sum(x => x.Price);
        }
    }
    public string Price
    {
        get
        {
            if (SellAssistant.CurrentReport != null)
                return Util.FormatChaosPrice(TotalPrice, SellAssistant.CurrentReport.DivinePrice);
            else
                return Util.FormatChaosPrice(TotalPrice);
        }
    }

    public static Whisper Create(string whisper)
    {
        var priceNameList = CompassList.PriceToModName.Keys.ToList();

        string username = null;
        string pattern = "@From (<.*> )?(.*):";
        Match match = Regex.Match(whisper, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            username = match.Groups[2].Value;
        }
        if (username == null)
            return null;

        whisper = whisper.Substring(whisper.IndexOf(':') + 1).Trim();


        foreach (var (subPattern, (q, i)) in WhisperPatterns)
        {
            match = Regex.Match(whisper, subPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var items = new List<WhisperItem>();

                for (int x = 0; x < match.Groups[q].Captures.Count; x++)
                {
                    string quantity = match.Groups[q].Captures[x].Value.Trim();
                    string item = match.Groups[i].Captures[x].Value.Trim().ToLower();

                    string closestMatch = priceNameList.Find(x => x.ToLower() == item);
                    if (closestMatch == null)
                    {
                        int minDistance = int.MaxValue;

                        foreach (string candidate in priceNameList)
                        {
                            int distance = Util.LevenshteinDistance(item.ToLower(), candidate.ToLower());
                            int subMinDistance = int.MaxValue;
                            string[] targetWords = candidate.Split(' ');
                            foreach (var word in targetWords)
                            {
                                int ldistance = Util.LevenshteinDistance(item.ToLower(), word.ToLower());
                                subMinDistance = Math.Min(subMinDistance, ldistance);
                            }

                            distance = Math.Min(distance, subMinDistance);

                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                closestMatch = candidate;
                            }
                        }
                    }
                    var mod = CompassList.PriceToModName[closestMatch];
                    if (quantity == "all")
                        quantity = SellAssistant.CompassCounts[mod].ToString();

                    items.Add(new WhisperItem
                    {
                        Name = closestMatch,
                        ModName = mod,
                        Quantity = int.Parse(quantity)
                    });

                }
                return new Whisper
                {
                    PlayerName = username,
                    Items = items,
                    Uuid = Guid.NewGuid().ToString(),
                };
            }
        }
        return null;
    }


    public List<(Keys, string, Action)> GetButtonsForWhisper()
    {
        var buttons = new List<(Keys, string, Action)>();

        int index = 0;
        Keys key;
        if (
            !HasSentInvite &&
            (
                Item.Status == FullfillmentStatus.Available ||
                (Item.Status == FullfillmentStatus.NotEnough && HasSentPartial)
            )
        )
        {
            key = Util.MapIndexToNumPad(++index);
            buttons.Add((key, $"Invite", () =>
            {
                HasSentInvite = true;
                Chat.QueueMessage("/invite " + PlayerName);
            }
            ));
        }
        else if (Item.Status == FullfillmentStatus.NotEnough && !HasSentPartial)
        {
            key = Util.MapIndexToNumPad(++index);
            buttons.Add((key, $"Partial", () =>
            {
                if (SellAssistant.CurrentReport != null)
                {
                    var priceString = SellAssistant.CurrentReport.AmountToString(Item.Name, SellAssistant.CompassCounts[Item.ModName]);
                    if (priceString != null)
                        Chat.QueueMessage($"@{PlayerName} {SellAssistant.CompassCounts[Item.ModName]} {Item.Name} left for {priceString}, still interested?");
                    else
                        Chat.QueueMessage($"@{PlayerName} I only have {SellAssistant.CompassCounts[Item.ModName]} {Item.Name} left, still interested?");
                }
                else
                {
                    Chat.QueueMessage($"@{PlayerName} I only have {SellAssistant.CompassCounts[Item.ModName]} {Item.Name} left, still interested?");
                }
                HasSentPartial = true;
            }
            ));
        }

        if (Item.Status == FullfillmentStatus.NotAvailable)
        {
            key = Util.MapIndexToNumPad(++index);
            buttons.Add((key, $"Sold", () =>
            {
                Chat.QueueMessage($"@{PlayerName} Sorry, all my \"{Item.Name}\" are sold");
                Hidden = true;
            }
            ));
        }

        if (Item.Status != FullfillmentStatus.NotAvailable)
        {
            key = Util.MapIndexToNumPad(++index);
            buttons.Add((key, $"Extract", () =>
            {
                SellAssistant.AddToExtractionQueue(Items.Select(x => (x.ModName, x.Quantity)).ToList());
                HasExtracted = true;
            }
            ));
        }


        key = Util.MapIndexToNumPad(++index);
        buttons.Add((key, $"Trade", () =>
        {
            Chat.QueueMessage("/tradewith " + PlayerName);
            HasTraded = true;
        }
        ));
        key = Util.MapIndexToNumPad(++index);
        buttons.Add((key, $"Trade NEW", () =>
        {
            TradeManager.AddTradeRequest(new TradeRequest
            {
                PlayerName = PlayerName,
                ExpectedValue = TotalPrice,
            });
            HasTraded = true;
        }
        ));

        key = Util.MapIndexToNumPad(++index);
        buttons.Add((key, $"Kick", () =>
        {
            Chat.QueueMessage(new string[] {
                            "/kick " + PlayerName,
                            $"@{PlayerName} ty"
                        });
            Hidden = true;
        }
        ));

        buttons.Add((Keys.NumPad0, "X", () =>
        {
            Hidden = true;
        }
        ));

        return buttons;
    }
}