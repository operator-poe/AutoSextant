using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoSextant.SellAssistant;

public class PoeStackReport
{
    public float DivinePrice { get; set; }
    public Dictionary<string, float> Prices { get; set; }

    public PoeStackReport(string report)
    {
        Regex divineRegex = new Regex(@":divine: = ([\d\.]+) :chaos:");
        Regex lineRegex = new Regex(@"(\d*)x (.*) (\d*\.?\d?)c / each");

        Match divineMatch = divineRegex.Match(report);
        if (divineMatch.Success)
        {
            bool result = float.TryParse(divineMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float divinePrice);
            Log.Debug($"Divine Price: {divinePrice.ToString("0.0")}");
            if (result)
                DivinePrice = divinePrice;
            else
                throw new Exception($"Invalid PoE Stack Report: {divineMatch.Groups[1].Value} is not a valid price");
        }
        else
        {
            throw new Exception("Invalid PoE Stack Report: Divine price not found");
        }


        Prices = new Dictionary<string, float>();
        MatchCollection matches = lineRegex.Matches(report);
        foreach (Match match in matches)
        {
            // string quantity = match.Groups[1].Value;
            string itemName = match.Groups[2].Value;

            if (!CompassList.PriceToModName.ContainsKey(itemName))
                throw new Exception($"Invalid PoE Stack Report: {itemName} not found in Compass List");
            bool result = float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float price);
            if (!result)
                throw new Exception($"Invalid PoE Stack Report: {match.Groups[3].Value} is not a valid price");

            Prices.Add(itemName, price);
            // string divines = match.Groups[4].Value;
            // string totalChaos = match.Groups[5].Value;
        }
    }

    public string AmountToString(string name, int amount)
    {
        if (Prices.ContainsKey(name))
        {
            float price = Prices[name];
            float total = price * amount;
            if (total < DivinePrice)
                return $"{total.ToString("0.0")}c";
            int divines = (int)(total / DivinePrice);
            float chaos = total % DivinePrice;
            return $"{divines} div + {chaos}c";
        }
        else
        {
            return null;
        }
    }
}
