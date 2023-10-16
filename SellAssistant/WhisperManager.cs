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

public static class WhisperManager
{
    public static List<Whisper> Whispers = Chat.GetPastMessages("@From", Whisper.WhisperPatterns.Select(x => new Regex(x.Item1)).ToList(), 3).Select(Whisper.Create).Where(x => x != null).ToList();
    // public static List<Whisper> Whispers = new List<Whisper>
    // {
    //     Whisper.Create("@From _______test___________________: wtb 10 copy of beast"),
    //     Whisper.Create("@From _____Test_____________: WTB 3 Strongbox Enraged 274c each, 4 Beyond 74c each. Total 3828c (16 div + 100c)"),
    // }.Where(x => x != null).ToList();//.Select(x => { x.InArea = true; return x; }).ToList();

    private static string chatPointer = Chat.GetPointer();
    public static void Tick()
    {
        foreach (var message in Chat.NewMessages(chatPointer))
        {
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
                whisper.Items.ForEach(x =>
                {
                    ImGui.PushID(i.ToString() + x.Name);
                    if (ImGui.Button("S"))
                    {
                        SellAssistant.selectedAmount = x.Quantity;
                        SellAssistant.selectedMod = x.ModName;
                    }
                    ImGui.SameLine();
                    ImGui.Text(x.Name);

                    ImGui.PopID();
                });
                ImGui.TableNextColumn();
                whisper.Items.ForEach(x => ImGui.Text(x.Quantity.ToString()));
                ImGui.TableNextColumn();
                whisper.Items.ForEach(x => ImGui.Text(x.PriceFormatted));
                if (whisper.MultiItem)
                {
                    ImGui.Text("-----------------");
                    ImGui.Text(whisper.Price);
                }
                ImGui.TableNextColumn();
                var status = whisper.Item.Status;

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
                        whisper.Item.Status == FullfillmentStatus.Available ||
                        (whisper.Item.Status == FullfillmentStatus.NotEnough && whisper.HasSentPartial)
                    )
                )
                {
                    if (ImGui.Button("Invite"))
                    {
                        Chat.QueueMessage("/invite " + whisper.PlayerName);
                        whisper.HasSentInvite = true;
                    }
                }
                else if (whisper.Item.Status == FullfillmentStatus.NotEnough && !whisper.HasSentPartial)
                {
                    if (ImGui.Button("Partial"))
                    {
                        if (SellAssistant.CurrentReport != null)
                        {
                            var priceString = SellAssistant.CurrentReport.AmountToString(whisper.Item.Name, SellAssistant.CompassCounts[whisper.Item.ModName]);
                            if (priceString != null)
                                Chat.QueueMessage($"@{whisper.PlayerName} {SellAssistant.CompassCounts[whisper.Item.ModName]} {whisper.Item.Name} left for {priceString}, still interested?");
                            else
                                Chat.QueueMessage($"@{whisper.PlayerName} I only have {SellAssistant.CompassCounts[whisper.Item.ModName]} {whisper.Item.Name} left, still interested?");
                        }
                        else
                        {
                            Chat.QueueMessage($"@{whisper.PlayerName} I only have {SellAssistant.CompassCounts[whisper.Item.ModName]} {whisper.Item.Name} left, still interested?");
                        }
                        whisper.HasSentPartial = true;
                    }
                }

                if (whisper.Item.Status == FullfillmentStatus.NotAvailable)
                {
                    if (ImGui.Button("Sold"))
                    {
                        Chat.QueueMessage($"@{whisper.PlayerName} Sorry, all my \"{whisper.Item.Name}\" are sold");
                        whisper.Hidden = true;
                    }
                }

                if (whisper.Item.Status != FullfillmentStatus.NotAvailable)
                {
                    GrayButton(SellAssistant.IsAnyRoutineRunning || whisper.HasExtracted);
                    if (ImGui.Button("Extract"))
                    {
                        SellAssistant.AddToExtractionQueue(whisper.Items.Select(x => (x.ModName, x.Quantity)).ToList());
                        whisper.HasExtracted = true;
                    }
                    GrayButtonEnd(SellAssistant.IsAnyRoutineRunning || whisper.HasExtracted);
                }


                GrayButton(whisper.HasTraded);
                if (ImGui.Button("Trade"))
                {
                    Chat.QueueMessage("/tradewith " + whisper.PlayerName);
                    whisper.HasTraded = true;
                }
                if (ImGui.Button("Trade NEW"))
                {
                    TradeManager.AddTradeRequest(new TradeRequest
                    {
                        PlayerName = whisper.PlayerName,
                        ExpectedValue = whisper.TotalPrice,
                    });
                    whisper.HasTraded = true;
                }
                GrayButtonEnd(whisper.HasTraded);

                if (ImGui.Button("Kick"))
                {
                    Chat.QueueMessage(new string[] {
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
