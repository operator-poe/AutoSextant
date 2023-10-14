using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ImGuiNET;

namespace AutoSextant.SellAssistant;

public enum FullfillmentStatus
{
    Available,
    NotEnough,
    NotAvailable
}

public class Whisper
{
    public string PlayerName { get; set; }
    public string Message { get; set; }

    public int Quantity { get; set; }

    public string ItemName { get; set; }
    public string ModName { get; set; }

    public bool Hidden { get; set; } = false;

    public bool InArea { get; set; } = false;

    public bool HasSentInvite { get; set; } = false;
    public bool HasSentPartial { get; set; } = false;
    public bool HasExtracted { get; set; } = false;
    public bool HasTraded { get; set; } = false;

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

    public string Price
    {
        get
        {
            if (SellAssistant.CurrentReport != null)
            {
                var priceString = SellAssistant.CurrentReport.AmountToString(ItemName, Quantity);
                if (priceString != null)
                    return priceString;
                else
                    return "-";
            }
            var price = AutoSextant.Instance.CompassList.Prices[ItemName];
            var mPrice = (float)(price.DivinePrice > 1.0f ? price.DivinePrice : price.ChaosPrice) * Quantity * SellAssistant.priceMultiplier;
            return price.DivinePrice > 1.0f ? mPrice.ToString("0.0") + "d" : mPrice.ToString("0.0") + "c";
        }
    }

    public static Whisper Create(string whisper)
    {
        var priceNameList = AutoSextant.Instance.CompassList.PriceToModName.Keys.ToList();

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

        List<(string, (int, int))> patterns = new List<(string, (int, int))>{
            (@"wtb ([0-9]+) ([a-z8 -]*)", (1,2)),
            (@"([0-9]+)x ([a-z8 -]+)", (1,2)),
            (@"(\b[a-z]+\b(?:\s+\b[a-z]+\b)*) all", (1,2)),
            (@"([0-9]+) ([a-z8 -]+)", (1,2)),
            (@"([a-z8 ]+) ([0-9]+)", (2,1))
        };

        foreach (var (subPattern, (q, i)) in patterns)
        {

            match = Regex.Match(whisper, subPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string quantity = match.Groups[q].Value;
                string item = match.Groups[i].Value.Trim().ToLower();

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
                var mod = AutoSextant.Instance.CompassList.PriceToModName[closestMatch];
                if (quantity == "all")
                    quantity = SellAssistant.CompassCounts[mod].ToString();

                return new Whisper
                {
                    PlayerName = username,
                    Quantity = int.Parse(quantity),
                    ItemName = closestMatch,
                    ModName = mod
                };
            }
        }
        return null;
    }
}

public static class WhisperManager
{
    public static List<Whisper> Whispers = new List<Whisper>();
    // {
    //     Whisper.Create("@From _______test___________________: wtb 10 copy of beast"),
    // }.Where(x => x != null).Select(x => { x.InArea = true; return x; }).ToList();
    public static long LastMessageCount = ChatBox.TotalMessageCount;

    private static AutoSextant I
    {
        get
        {
            return AutoSextant.Instance;
        }
    }

    private static PoeChatElement ChatBox
    {
        get
        {
            return AutoSextant.Instance.GameController.Game.IngameState.IngameUi.ChatBox;
        }
    }

    public static void Tick()
    {
        if (ChatBox == null)
        {
            return;
        }
        if (ChatBox.TotalMessageCount == LastMessageCount)
        {
            return;
        }
        var diff = ChatBox.TotalMessageCount - LastMessageCount;
        LastMessageCount = ChatBox.TotalMessageCount;


        for (long i = ChatBox.TotalMessageCount - diff; i < ChatBox.TotalMessageCount; i++)
        {
            var message = ChatBox.Messages[(int)i];

            if (message.StartsWith("@From"))
            {
                var whisper = Whisper.Create(message);
                if (whisper != null)
                {
                    Whispers.Add(whisper);
                }
            }

            if (message.Contains("has joined the area"))
            {
                string pattern = "(<.*> )?(.*) has joined the area";
                Match match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string username = match.Groups[2].Value;
                    // update all whispers with this username
                    foreach (var whisper in Whispers.Where(x => x.PlayerName == username))
                    {
                        whisper.InArea = true;
                    }
                }
            }
            if (message.Contains("has left the area"))
            {
                string pattern = "(<.*> )?(.*) has left the area";
                Match match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string username = match.Groups[2].Value;
                    // update all whispers with this username
                    foreach (var whisper in Whispers.Where(x => x.PlayerName == username))
                    {
                        whisper.InArea = false;
                    }
                }
            }
        }
    }

    public static void Render()
    {
        float tableWidth = ImGui.GetContentRegionAvail().X;
        if (ImGui.BeginTable("WhisperManagerTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Buyer", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, tableWidth * 0.05f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, tableWidth * 0.1f);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            var whispers = Whispers.Where(x => !x.Hidden).ToList();
            for (int i = 0; i < whispers.Count; i++)
            {
                var whisper = whispers[i];

                ImGui.PushID(i);

                ImGui.TableNextRow();

                if (whisper.InArea)
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0, 1, 0, 0.3f)));
                }

                ImGui.TableNextColumn();
                ImGui.Text(whisper.PlayerName);
                ImGui.TableNextColumn();
                ImGui.Text(whisper.ItemName);
                ImGui.TableNextColumn();
                ImGui.Text(whisper.Quantity.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(whisper.Price);
                ImGui.TableNextColumn();
                var status = whisper.Status;

                // display red or green or orange
                if (status == FullfillmentStatus.Available)
                    ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Ok");
                else if (status == FullfillmentStatus.NotEnough)
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "Partial");
                else
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "N/A");

                ImGui.TableNextColumn();

                if (
                    !whisper.HasSentInvite &&
                    (
                        whisper.Status == FullfillmentStatus.Available ||
                        (whisper.Status == FullfillmentStatus.NotEnough && whisper.HasSentPartial)
                    )
                )
                {
                    if (ImGui.Button("Invite"))
                    {
                        Chat.SendChatMessage("/invite " + whisper.PlayerName);
                        whisper.HasSentInvite = true;
                    }
                }
                else if (whisper.Status == FullfillmentStatus.NotEnough && !whisper.HasSentPartial)
                {
                    if (ImGui.Button("Partial"))
                    {
                        if (SellAssistant.CurrentReport != null)
                        {
                            var priceString = SellAssistant.CurrentReport.AmountToString(whisper.ItemName, SellAssistant.CompassCounts[whisper.ModName]);
                            if (priceString != null)
                                Chat.SendChatMessage($"@{whisper.PlayerName} {SellAssistant.CompassCounts[whisper.ModName]} {whisper.ItemName} left for {priceString}, still want them?");
                            else
                                Chat.SendChatMessage($"@{whisper.PlayerName} I only have {SellAssistant.CompassCounts[whisper.ModName]} {whisper.ItemName} left, still want them?");
                        }
                        else
                        {
                            Chat.SendChatMessage($"@{whisper.PlayerName} I only have {SellAssistant.CompassCounts[whisper.ModName]} {whisper.ItemName} left, still want them?");
                        }
                        whisper.HasSentPartial = true;
                    }
                }

                if (whisper.Status == FullfillmentStatus.NotAvailable)
                {
                    if (ImGui.Button("Sold"))
                    {
                        Chat.SendChatMessage($"@{whisper.PlayerName} Sorry, {whisper.ItemName} are sold");
                        whisper.Hidden = true;
                    }
                }

                if (whisper.Status != FullfillmentStatus.NotAvailable)
                {
                    GrayButton(whisper.HasExtracted);
                    if (ImGui.Button("Select"))
                    {
                        SellAssistant.selectedAmount = Math.Min(Math.Min(whisper.Quantity, SellAssistant.CompassCounts[whisper.ModName]), 60);
                        SellAssistant.selectedMod = whisper.ModName;
                    }
                    // if (ImGui.Button("Extract"))
                    // {
                    //     SellAssistant.TakeFromStash(whisper.ModName, whisper.Quantity);
                    //     SellAssistant.selectedAmount = Math.Min(Math.Min(whisper.Quantity, SellAssistant.CompassCounts[whisper.ModName]), 60);
                    //     SellAssistant.selectedMod = whisper.ModName;
                    //     whisper.HasExtracted = true;
                    // }
                    GrayButtonEnd(whisper.HasExtracted);
                }


                GrayButton(whisper.HasTraded);
                if (ImGui.Button("Trade"))
                {
                    Chat.SendChatMessage("/tradewith " + whisper.PlayerName);
                    whisper.HasTraded = true;
                }
                GrayButtonEnd(whisper.HasTraded);

                if (ImGui.Button("Kick"))
                {
                    Chat.SendChatMessage(new string[] {
                            "/kick " + whisper.PlayerName,
                            $"@{whisper.PlayerName} ty"
                        });
                    whisper.Hidden = true;
                }

                ImGui.SameLine();
                if (ImGui.Button("X"))
                {
                    whisper.Hidden = true;
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

    }

    private static void GrayButton(bool condition)
    {
        if (condition)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 0.5f)));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 0.5f)));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 0.5f)));
        }
    }
    private static void GrayButtonEnd(bool condition)
    {
        if (condition)
        {
            ImGui.PopStyleColor(3);
        }
    }
}
