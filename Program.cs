using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Headless.Client.Net.Delivery;
using Umbraco.Headless.Client.Net.Management;
using Content = Umbraco.Headless.Client.Net.Management.Models.Content;

const string ProjectAlias = "webhookmaze";
const string ApiKey = "zcJBg3NZmcWeR8Dm4jD5";
const string ContentTypeAlias = "tvShow";
const string IdPropertyAlias = "showId";
const string SummaryPropertyAlias = "summary";
const string AllTvShowsContentNodeKey = "3c0a7d4d-4fc9-4b38-9b24-cea3a487c00d";

ContentManagementService contentManagementService = new(ProjectAlias, ApiKey);
ContentDeliveryService contentDeliveryService = new(ProjectAlias, ApiKey);

var existingIds = await GetExistingTvShowsFromHeartcoreAsync();
await GetALlTvShowsFromTvMazeAsync(existingIds);

async Task GetALlTvShowsFromTvMazeAsync(HashSet<int> existingIds)
{
    int page = 0;
    HttpClient client = new();
    
    while (true)
    {
        var url = $"https://api.tvmaze.com/shows?page={page}";
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            break;
        var jsonStream = await response.Content.ReadAsStreamAsync();
        await TurnTvShowJsonIntoCsharpModels(jsonStream, existingIds);
        page++;
        // await Task.Delay(100);
    }
}

async Task TurnTvShowJsonIntoCsharpModels(Stream jsonStream, HashSet<int> existingIds)
{
    List<TvModel> tvshows = await JsonSerializer.DeserializeAsync<List<TvModel>>(jsonStream);
    var newTvShows = tvshows?.Where(show => !existingIds.Contains(show.Id)).ToList() ?? new List<TvModel>();
    if (newTvShows.Any())
    {
        await CreateTvShowContentAsync(newTvShows);
    }
}

async Task CreateTvShowContentAsync(List<TvModel> tvshows)
{
    string[] cultures = new[] { "en-US", "da-DK", "nl-NL", "fr-FR", "de-DE", "it-IT", "el-GR", "hi-IN", "ja-JP", "vi-VN" };
    foreach (var tvshow in tvshows)
    {
        var content = new Content()
        {
            ContentTypeAlias = ContentTypeAlias,
            ParentId = new Guid(AllTvShowsContentNodeKey)
        };

        foreach (var culture in cultures)
        {
            content.Name.Add(culture, $"{tvshow.Name} ({tvshow.Id})" ?? "No name for this tvshow");
        }
        
        content.Properties.Add(IdPropertyAlias, new Dictionary<string, object>
        {
            {
                "$invariant", tvshow.Id
            }
        });

        var translatedSummary = new Dictionary<string, object>();
        foreach (var culture in cultures)
        {
            translatedSummary[culture] = $"{culture} {tvshow.Summary}" ?? "No summary for this tvshow";
        }
        content.Properties.Add(SummaryPropertyAlias, translatedSummary);

        try
        {
            var createdContent = await contentManagementService.Content.Create(content);

            if (createdContent is null || createdContent.Id == Guid.Empty)
            {
                // Log the content object for inspection
                Console.WriteLine($"Create returned empty Id for '{tvshow.Name}'. Inspecting object:");
                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(content, Newtonsoft.Json.Formatting.Indented));
                throw new InvalidOperationException($"Create returned empty Id for '{tvshow.Name}'.");
            }

            await contentManagementService.Content.Publish(createdContent.Id);

            Console.WriteLine($"Created and published content: {createdContent.Name.First().Value}");
        }
        catch (Refit.ApiException ex)
        {
            // Log full API error response
            Console.WriteLine($"Create failed for '{tvshow.Name}' (HTTP {ex.StatusCode}).");
            Console.WriteLine("API response body:\n" + ex.Content);
    
            // Optionally throw to stop execution or continue with next tvshow
            throw;
        }
        catch (Exception ex)
        {
            // Catch any other unexpected exception
            Console.WriteLine($"Unexpected error creating '{tvshow.Name}': {ex}");
            throw;
        }
        
        // Console.WriteLine($"Created and published content: {createdContent.Name.First().Value}");
    }
}

async Task<HashSet<int>> GetExistingTvShowsFromHeartcoreAsync()
{
    var existingIds = new HashSet<int>();
    var existingChilds = await contentDeliveryService.Content.GetChildren(Guid.Parse(AllTvShowsContentNodeKey));
    var items = existingChilds.Content.Items;
    
    if (items is null)
        return existingIds;

    foreach (var tvshow in items)
    {
        // no fucking clue if this works
        if (tvshow.Properties is not null && tvshow.Properties.TryGetValue(IdPropertyAlias, out var idObj)
            && idObj is Dictionary<string, object> dict
            && dict.TryGetValue("$invariant", out var idVal)
            && int.TryParse(idVal.ToString(), out var id))
        {
            existingIds.Add(id);
        }
    }
    return existingIds;
}

public class TvModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("summary")]
    public string Summary { get; set; }
}