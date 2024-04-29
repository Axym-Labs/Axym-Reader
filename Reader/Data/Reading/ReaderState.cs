using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reader.Data.Product;
using Reader.Data.ProductExceptions;
using Reader.Data.Storage;
using Reader.Modules;
using Reader.Modules.Logging;
using Reader.Modules.Product;

namespace Reader.Data.Reading;

public class ReaderState
{
    public string Title;
    public int Position = 0;
    public DateTime LastRead;
    public string Text;
    public ReaderStateSource Source;
    public string? SourceDescription;

    public ReaderState(string title, string text, ReaderStateSource source, string? sourceDescription = null, DateTime? lastRead = null)
    {
        Title = title;
        Text = text;
        Source = source;
        SourceDescription = sourceDescription;
        LastRead = lastRead ?? DateTime.Now;
    }

    public static ReaderState ImportFromJson(string json, ReaderStateSource sourceBackup, string? sourceDescriptionBackup = null, string? version = null)
    {
        JObject? stateObj = JsonConvert.DeserializeObject<JObject>(json);

        if (stateObj == null)
        {
            throw new InvalidOperationException("Empty Json string");
        }
        if (stateObj["Text"] == null || stateObj["Text"]!.Value<string>() == null)
        {
            throw new InvalidOperationException("Text is missing but required");
        }

        string text = stateObj["Text"]!.Value<string>()!;
        DateTime lastRead = stateObj["LastRead"]?.Value<DateTime>() ?? DateTime.Now;
        ReaderStateSource source;
        if (stateObj["Source"] != null)
        {
            source = stateObj["Source"]!.Value<ReaderStateSource>();
        } else
        {
            source = sourceBackup;
        }
        string? sourceDescription = stateObj["SourceDescription"]?.Value<string>() ?? sourceDescriptionBackup;

        string title = stateObj["Title"]?.Value<string>() ?? GetNew(source, sourceDescription).Title;

        var state = new ReaderState(text, title, source, sourceDescription, lastRead);

        Log.Information("Imported ReaderState from Json", new { source, sourceDescription, version });

        return state;
    }

    public static async Task<ReaderState> Scrape(ScrapeInputs inputs)
    {
        var websiteInfo = new WebExtractor();
        await websiteInfo.Load(inputs.Url);
        var title = websiteInfo.GetTitle();

        var text = inputs.NewTextInputMethod switch
        {
            NewTextInputMethod.LargestArticleSubsection => websiteInfo.GetLargestArticleNode().InnerText,
            NewTextInputMethod.XPath =>
            inputs.XPathInputs.SelectAll
            ? websiteInfo.GetAllNodesByXPath(inputs.XPathInputs.XPath).Select(node => node.InnerText).Aggregate(TextHelper.JoinSections)
            : websiteInfo.GetNodeByXPath(inputs.XPathInputs.XPath).InnerText,
            _ => throw new ScrapingException("Invalid new text input method")
        };

        var state = GetNew(ReaderStateSource.WebsiteExtract, $"Extracted from {inputs.Url}");
        state.Title = title;
        state.Text = text;

        return state;
    }


    public static ReaderState GetDemo(ReaderStateSource source, string? sourceDescription = null)
    {
        return new ReaderState(ProductConstants.DemoTitle, ProductConstants.DemoText, source, sourceDescription, DateTime.Now);
    }

    public static ReaderState GetNew(ReaderStateSource source, string? sourceDescription = null)
    {
        return new ReaderState(ProductConstants.DefaultNewTitle, ProductConstants.DefaultNewText, source, sourceDescription, DateTime.Now);
    }
}
