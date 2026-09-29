using System.Text.Json;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models;

public record ModelId : IComparable<ModelId>
{
    public string Category { get; }

    public string Entry { get; }

    public static readonly ModelId none = new ModelId("NONE", "NONE");

    private const string _bannedSuffix = "_MODEL";

    public ModelId(string category, string entry)
    {
        if (category.EndsWith(_bannedSuffix))
        {
            throw new ArgumentException("Category cannot end with '_MODEL'.", nameof(category));
        }
        Category = category;
        Entry = entry;
    }

    public static ModelId Deserialize(string json)
    {
        string[] array = json.Split('.');
        if (array.Length != 2)
        {
            throw new JsonException("'" + json + "' does not match the expected ModelId form.");
        }
        return new ModelId(array[0], array[1]);
    }

    public override string ToString()
    {
        return Category + "." + Entry;
    }

    public int CompareTo(ModelId? other)
    {
        int num = string.Compare(Category, other?.Category, StringComparison.Ordinal);
        if (num != 0)
        {
            return num;
        }
        return string.Compare(Entry, other?.Entry, StringComparison.Ordinal);
    }

    public static string SlugifyCategory<T>()
    {
        return SlugifyCategory(typeof(T).Name);
    }

    public static string SlugifyCategory(string category)
    {
        string text = StringHelper.Slugify(category);
        if (text.EndsWith(_bannedSuffix))
        {
            text = text.Substring(0, text.Length - _bannedSuffix.Length);
        }
        return text;
    }
}
