using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Headless.Client.Net.Management;
using Umbraco.Headless.Client.Net.Management.Models;

const string ProjectAlias = "webhookmaze";
const string ApiKey = "zcJBg3NZmcWeR8Dm4jD5";
const string ContentTypeAlias = "tvShow";
const string IdPropertyAlias = "showId";
const string SummaryPropertyAlias = "summary";
const string AllTvShowsContentNodeKey = "3c0a7d4d-4fc9-4b38-9b24-cea3a487c00d";

ContentManagementService contentManagementService = new(ProjectAlias, ApiKey);

await GetALlTvShowsFromTvMazeAsync();

// Need to pass in ID so it can continue where it left off.
async Task GetALlTvShowsFromTvMazeAsync()
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
        TurnTvShowJsonIntoCsharpModels(jsonStream);
        page++;
        // await Task.Delay(100);
    }
}

async Task TurnTvShowJsonIntoCsharpModels(Stream jsonStream)
{
    List<TvModel> tvshows = await JsonSerializer.DeserializeAsync<List<TvModel>>(jsonStream);
    CreateTvShowContentAsync(tvshows).GetAwaiter().GetResult();
}

async Task CreateTvShowContentAsync(List<TvModel> tvshows)
{
    // Go to backoffice and add all languages to the string array
    string[] cultures = new[] { "en-US", "da-DK" };
    foreach (var tvshow in tvshows)
    {
        var content = new Content
        {
            ContentTypeAlias = ContentTypeAlias,
            ParentId = new Guid(AllTvShowsContentNodeKey)
        };
        content.Name.Add("en-US", tvshow.Name ?? "No name for this tvshow");
        content.Properties.Add(IdPropertyAlias, new Dictionary<string, object>
        {
            {
                "$invariant", tvshow.Id
            }
        });
        foreach (var culture in cultures)
        {
            content.Properties.Add(SummaryPropertyAlias, new Dictionary<string, object>
            {
                {
                    culture, tvshow.Summary ?? "No summary for this tvshow"
                }
            });
        }
        
        var createdContent = await contentManagementService.Content.Create(content);
        await contentManagementService.Content.Publish(createdContent.Id);
        
        Console.WriteLine($"Created and published content: {createdContent.Name.First().Value}");
    }
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