using System.Collections.Generic;
using System.Text.Json;

namespace Aion2DPSViewer.Api;

public static class JsonElementExtensions
{
    public static JsonElement GetProp(this JsonElement el, string name)
    {
        JsonElement jsonElement;
        return el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out jsonElement) ? new JsonElement() : jsonElement;
    }

    public static string? GetString(this JsonElement el, string name)
    {
        JsonElement jsonElement;
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out jsonElement))
            return null;
        string str;
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.String:
                str = jsonElement.GetString();
                break;
            case JsonValueKind.Number:
                str = jsonElement.GetRawText();
                break;
            default:
                str = null;
                break;
        }
        return str;
    }

    public static int GetInt(this JsonElement el, string name)
    {
        JsonElement jsonElement;
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out jsonElement))
            return 0;
        int num1;
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.String:
                int num2;
                num1 = int.TryParse(jsonElement.GetString(), out num2) ? num2 : 0;
                break;
            case JsonValueKind.Number:
                num1 = jsonElement.GetInt32();
                break;
            default:
                num1 = 0;
                break;
        }
        return num1;
    }

    public static List<JsonElement> GetPropArray(this JsonElement el, string obj, string arr)
    {
        List<JsonElement> propArray = new List<JsonElement>();
        JsonElement prop = el.GetProp(obj);
        JsonElement jsonElement;
        if (prop.ValueKind != JsonValueKind.Object || !prop.TryGetProperty(arr, out jsonElement) || jsonElement.ValueKind != JsonValueKind.Array)
            return propArray;
        foreach (JsonElement enumerate in jsonElement.EnumerateArray())
            propArray.Add(enumerate);
        return propArray;
    }
}
