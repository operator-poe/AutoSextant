using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ExileCore;
using Newtonsoft.Json;
using AutoSextant.PoEStack.Api.Request;
using AutoSextant.PoEStack.Api.Response;
using System.Globalization;

namespace AutoSextant.PoEStack;

public class PriceFetcher
{
  private List<string> _tags = new List<string>
  {
      "compass",
  };
  private List<(string, string)> _searchTerms = new List<(string, string)>
  {
      ("compass", "runic"),
      ("currency", "awakened sextant"),
      ("currency", "divine orb"),
  };

  private const string DefaultQuery =
    "query Query($search: LivePricingSummarySearch!) {livePricingSummarySearch(search: $search) {entries {itemGroup {key,displayName,properties}valuation{value}stockValuation{value}}}}";

  public async Task<(Dictionary<string, PoeStackPrice>, Dictionary<string, PoeStackPrice>)> Fetch()
  {
    Log.Debug("Fetching data from poestack.com");
    try
    {
      using var client = new HttpClient();

      var entries = new List<Entry>();
      foreach (var _tag in _tags)
      {
        var fromOffset = 0;
        while (true)
        {
          Log.Debug($"Fetching data from poestack.com, offset: {fromOffset} tag: {_tag}");
          var responseObject = await GetEntriesAsync(client, _tag, "", fromOffset);
          entries.AddRange(responseObject);
          if (responseObject.Count() == 0 || responseObject.Count() < 40)
          {
            break;
          }

          fromOffset += responseObject.Count();
        }
      }
      foreach (var searchTerm in _searchTerms)
      {
        var fromOffset = 0;
        while (true)
        {
          DebugWindow.LogMsg($"Fetching data from poestack.com, offset: {fromOffset} search: {searchTerm}");
          var responseObject = await GetEntriesAsync(client, searchTerm.Item1, searchTerm.Item2, fromOffset);
          entries.AddRange(responseObject);
          if (responseObject.Count() == 0 || responseObject.Count() < 40)
          {
            break;
          }

          fromOffset += responseObject.Count();
        }
      }

      var prices = entries
          .Where(x => x.itemGroup != null && x.valuation != null)
          // TODO (supporte elevated sextants)
          .Where(x => x.itemGroup.properties != null && x.itemGroup.properties.Length > 0)
          .Where(x => x.itemGroup.properties[0].value == "4")
          .GroupBy(x => x.itemGroup.displayName)
          .Select(x => x.First())
          .Select(x =>
          {
            return new PoeStackPrice
            {
              Key = x.itemGroup.key,
              Name = x.itemGroup.displayName,
              Value = x.valuation.value,
              StockValue = x.stockValuation?.value ?? 0,
              Uses = int.Parse(x.itemGroup.properties[0].value),
            };
          })
          .ToDictionary(x => x.Name, x => x);

      var currencyValues = entries
          .Where(x => x.itemGroup != null && x.valuation != null)
          .Where(x => x.itemGroup.key == "divine orb" || x.itemGroup.key == "awakened sextant")
          .GroupBy(x => x.itemGroup.key)
          .Select(x => x.First())
          .Select(x =>
          {
            return new PoeStackPrice
            {
              Key = x.itemGroup.key,
              Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.itemGroup.key),
              Value = x.valuation.value,
              StockValue = x.stockValuation?.value ?? 0,
              Uses = 1,
            };
          })
          .ToDictionary(x => x.Name, x => x);

      var dataPath = AutoSextant.Instance.DirectoryFullName + "\\Data\\data.json";
      await SaveToFile(JsonConvert.SerializeObject(prices), dataPath);
      dataPath = AutoSextant.Instance.DirectoryFullName + "\\Data\\currency.json";
      await SaveToFile(JsonConvert.SerializeObject(currencyValues), dataPath);

      Log.Debug("Data update complete");
      return (prices, currencyValues);
    }
    catch (Exception ex)
    {
      Log.Error(ex.ToString());
      return (null, null);
    }
  }

  private async Task<List<Entry>> GetEntriesAsync(HttpClient client, string tag, string search, int fromOffset)
  {
    var response = await client.PostAsync("https://api.poestack.com/graphql",
      new StringContent(JsonConvert.SerializeObject(new RequestRoot
      {
        operationName = "Query",
        variables = new Variables
        {
          search = new Search
          {
            // TODO
            league = "Ancestor" ?? throw new Exception("Please configure the league"),
            offSet = fromOffset,
            searchString = search,
            quantityMin = 25,
            tag = tag
          },
        },
        query = DefaultQuery,
      }), Encoding.Default, "application/json"));
    response.EnsureSuccessStatusCode();
    var str = await response.Content.ReadAsStringAsync();
    var responseObject = JsonConvert.DeserializeObject<ResponseRoot>(str);
    if (responseObject.errors != null)
    {
      Log.Error($"Request returned errors: {responseObject.errors}");
      throw new Exception("Request returned errors");
    }
    var entries = new List<Entry>();
    entries.AddRange(responseObject.data.livePricingSummarySearch.entries);
    return entries;
  }

  public async Task<(Dictionary<string, PoeStackPrice>, Dictionary<string, PoeStackPrice>)> Load(bool ignoreFileAge = false)
  {
    try
    {
      var dataPath = AutoSextant.Instance.DirectoryFullName + "\\Data\\data.json";
      if (!File.Exists(dataPath) || File.GetLastWriteTime(dataPath) < DateTime.Now.AddDays(-1) || ignoreFileAge)
      {
        return await Fetch();
      }

      var data = await File.ReadAllTextAsync(dataPath);
      dataPath = AutoSextant.Instance.DirectoryFullName + "\\Data\\currency.json";
      var data2 = await File.ReadAllTextAsync(dataPath);
      return (JsonConvert.DeserializeObject<Dictionary<string, PoeStackPrice>>(data), JsonConvert.DeserializeObject<Dictionary<string, PoeStackPrice>>(data2));
    }
    catch (Exception ex)
    {
      Log.Error(ex.ToString());
      return (null, null);
    }
  }

  private async Task<string> SaveToFile(string Content, string Path)
  {
    try
    {
      using var client = new HttpClient();

      var dataPath = AutoSextant.Instance.DirectoryFullName + "\\Data";
      if (!Directory.Exists(dataPath))
      {
        Directory.CreateDirectory(dataPath);
      }
      await File.WriteAllTextAsync(Path, Content);
      return null;
    }
    catch (HttpRequestException e)
    {
      return e.Message;
    }
  }
}