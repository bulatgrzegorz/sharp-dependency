using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace sharp_dependency.Repositories.Bitbucket;

public class BitbucketServerProjectManager : IProjectManager
{
    private readonly HttpClient _apiHttpClient;
    private const int PathsLimit = 1000;
    private readonly string _projectName;
    
    public BitbucketServerProjectManager(string baseUrl, string projectName, string authorizationToken) : this(baseUrl,projectName)
    {
        var authenticationHeader = AuthenticationHeaderValue.Parse($"Bearer {authorizationToken}");
        _apiHttpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
        //methods that accept multipart/form-data will only process requests with X-Atlassian-Token: no-check header.
        _apiHttpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }
    
    public BitbucketServerProjectManager(string baseUrl, string projectName, (string userName, string password) credentials) : this(baseUrl, projectName)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.userName}:{credentials.password}"));
        var authenticationHeader = AuthenticationHeaderValue.Parse($"Basic {header}");
        _apiHttpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
        //methods that accept multipart/form-data will only process requests with X-Atlassian-Token: no-check header.
        _apiHttpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }
    
    public BitbucketServerProjectManager(string baseUrl, string projectName)
    {
        Guard.ThrowIfNullOrWhiteSpace(baseUrl);
        Guard.ThrowIfNullOrWhiteSpace(projectName);
        _projectName = projectName;
        
        _apiHttpClient = new HttpClient(){BaseAddress = new Uri($"{baseUrl}/rest/api/latest/projects/{projectName}/")};
    }
    
    public async Task<IEnumerable<string>> GetRepositories()
    {
        var response = await _apiHttpClient.GetFromJsonAsync<GetRepositoriesResponse>($"repos?limit={PathsLimit}");
        return response?.Values.Select(x => x.Slug) ?? Enumerable.Empty<string>();
    }
    
    private class GetRepositoriesResponse
    {
        public IEnumerable<Repository> Values { get; set; } = null!;
    }
    
    private class Repository
    {
        public string Slug { get; set; } = null!;
    }
}