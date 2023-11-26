using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace sharp_dependency.Repositories.Bitbucket;

public class BitbucketCloudProjectManager : IProjectManager
{
    private readonly HttpClient _httpClient;
    private readonly string _workspace;

    public BitbucketCloudProjectManager(string baseUrl, string workspace, string authorizationToken) : this(baseUrl, workspace)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);
    }
    
    public BitbucketCloudProjectManager(string baseUrl, string workspace, (string userName, string password) credentials) : this(baseUrl, workspace)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.userName}:{credentials.password}"));
        _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Basic {header}");
    }

    public BitbucketCloudProjectManager(string baseUrl, string workspace)
    {
        Guard.ThrowIfNullOrWhiteSpace(baseUrl);
        Guard.ThrowIfNullOrWhiteSpace(workspace);
        _workspace = workspace;
        _httpClient = CreateHttpClient(baseUrl, workspace);
    }
    
    public async Task<IEnumerable<string>> GetRepositories()
    {
        var response = await _httpClient.GetFromJsonAsync<GetRepositoriesResponse>("?fields=values.slug");
        return response?.Values.Select(x => x.Slug) ?? Enumerable.Empty<string>();
    }
    
    private HttpClient CreateHttpClient(string baseUrl, string workspace) => 
        new() { BaseAddress = new Uri($"{baseUrl}/2.0/repositories/{workspace.ToLowerInvariant()}/")};
    
    private class GetRepositoriesResponse
    {
        public IEnumerable<Repository> Values { get; set; } = null!;
    }
    
    private class Repository
    {
        public string Slug { get; set; } = null!;
    }
}