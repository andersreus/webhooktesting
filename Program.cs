using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Headless.Client.Net.Delivery;
using Umbraco.Headless.Client.Net.Management;
using webhooktesting;
using Content = Umbraco.Headless.Client.Net.Management.Models.Content;

const string ProjectAlias = "webhookmaze";
const string ApiKey = "zcJBg3NZmcWeR8Dm4jD5";
const string ContentTypeAlias = "tvShow";
const string IdPropertyAlias = "showId";
const string SummaryPropertyAlias = "summary";
const string AllTvShowsContentNodeKey = "3c0a7d4d-4fc9-4b38-9b24-cea3a487c00d";
const string DbPath = "existingtvshows.db";

ContentManagementService contentManagementService = new(ProjectAlias, ApiKey);
ContentDeliveryService contentDeliveryService = new(ProjectAlias, ApiKey);

var repo = new ExistingTvShowIdRepository(DbPath);
var existingDbIds = repo.GetAllIds();
await GetALlTvShowsFromTvMazeAsync(existingDbIds);

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
            await contentManagementService.Content.Publish(createdContent.Id);

            Console.WriteLine($"Created and published tvshow: {createdContent.Name.First().Value}");
            
            repo.AddId(tvshow.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating: '{tvshow.Name}': {ex}");
        }
    }
}

/*async Task<HashSet<int>> GetExistingTvShowsFromHeartcoreAsync()
{
    var existingIds = new HashSet<int>();
    var parentId = Guid.Parse(AllTvShowsContentNodeKey);
    const int pageSize = 100; // Don't think it matters, but 100 nodes is default in Umbraco backoffice per page.
    int page = 1;

    while (true)
    {
        var children = await contentDeliveryService.Content
            .GetChildren(parentId, page: page, pageSize: pageSize);

        var items = children?.Content?.Items;
        if (items == null || !items.Any())
            break;

        foreach (var tvshow in items)
        {
            if (tvshow?.Properties != null &&
                tvshow.Properties.TryGetValue(IdPropertyAlias, out var idObj) &&
                idObj is long l)
            {
                existingIds.Add((int)l);
            }
        }

        if (items.Count() < pageSize)
            break;

        page++;
    }

    return existingIds;
}*/

public class TvModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("summary")]
    public string Summary { get; set; }
}