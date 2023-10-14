using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AutoSextant;

public class CompassFileEntry
{
  public string Name { get; set; }
  public float Divine { get; set; }
  public int Chaos { get; set; }
  public bool LowConfidence { get; set; }
  public int Ratio { get; set; }
}
public class CompassFile
{
  public long Timestamp { get; set; }
  public CompassFileEntry[] Data { get; set; }
}

public class TFTData
{
  private static AutoSextant Instance = AutoSextant.Instance;
  private static string DataPath = Path.Join(Instance.DirectoryFullName, "Data", "compassPrices.json");
  public static string DataUrl = "https://raw.githubusercontent.com/The-Forbidden-Trove/tft-data-prices/master/lsc/bulk-compasses.json";

  public static async Task<string> FetchData()
  {
    var result = await DownloadToFile(DataUrl, DataPath);
    if (result != null)
    {
      return result;
    }
    return null;
  }

  public static bool ShouldFecthData()
  {
    var dataPath = Instance.DirectoryFullName + "\\Data";
    if (!Directory.Exists(dataPath))
    {
      return true;
    }

    var files = Directory.GetFiles(dataPath);
    if (files.Length == 0)
    {
      return true;
    }



    var now = DateTime.Now;
    var oldestFile = files.Select(f => new FileInfo(f)).OrderBy(f => f.LastWriteTime).First();
    var timeSinceLastWrite = now - oldestFile.LastWriteTime;
    if (timeSinceLastWrite.TotalMinutes > 60)
    {
      return true;
    }

    return false;
  }

  private static async Task<string> DownloadToFile(string Url, string Path)
  {
    try
    {
      using var client = new HttpClient();
      var response = await client.GetAsync(Url);
      var content = await response.Content.ReadAsStringAsync();

      var dataPath = Instance.DirectoryFullName + "\\Data";
      if (!Directory.Exists(dataPath))
      {
        Directory.CreateDirectory(dataPath);
      }
      await File.WriteAllTextAsync(Path, content);
      return null;
    }
    catch (HttpRequestException e)
    {
      return e.Message;
    }
  }

  public static async Task<Dictionary<string, CompassPrice>> ParseData()
  {
    var prices = new Dictionary<string, CompassPrice>();
    var data = await File.ReadAllTextAsync(DataPath);
    try
    {
      var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<CompassFile>(data);
      foreach (var entry in parsed.Data)
      {
        var price = new CompassPrice
        {
          Name = entry.Name,
          ChaosPrice = entry.Chaos,
          DivinePrice = entry.Divine
        };
        prices.Add(entry.Name, price);
      }
    }
    catch (Exception e)
    {
      Instance.LogError(e.Message);
      return prices;
    }
    return prices;
  }
}
