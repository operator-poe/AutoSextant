using Newtonsoft.Json.Linq;

namespace AutoSextant.PoEStack.Api.Response;

public class ResponseRoot
{
    public ResponseData data { get; set; }
    public JToken errors { get; set; }
}