using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace sharp_dependency;

//https://developer.atlassian.com/cloud/bitbucket/rest/intro
public class BitbucketCloudRepositoryManager: IRepositoryManger
{
    private readonly HttpClient _httpClient;
    private Dictionary<string, string>? _pathsToAddress;

    public BitbucketCloudRepositoryManager(string baseUrl, string workspace, string repository, string authorizationToken) : this(baseUrl, workspace, repository)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);
    }
    
    public BitbucketCloudRepositoryManager(string baseUrl, string workspace, string repository, (string userName, string password) credentials) : this(baseUrl, workspace, repository)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.userName}:{credentials.password}"));
        _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Basic {header}");
    }

    public BitbucketCloudRepositoryManager(string baseUrl, string workspace, string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        _httpClient = CreateHttpClient(baseUrl, workspace, repository);
    }

    //Used in tests
    internal BitbucketCloudRepositoryManager(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private HttpClient CreateHttpClient(string baseUrl, string workspace, string repository) => 
        new() { BaseAddress = new Uri($"{baseUrl}/2.0/repositories/{workspace.ToLowerInvariant()}/{repository.ToLowerInvariant()}/")};

    public async Task<IEnumerable<string>> GetRepositoryFilePaths()
    {
        if (_pathsToAddress is {})
        {
            return _pathsToAddress.Keys;
        }

        _pathsToAddress = new Dictionary<string, string>();
        await GetRepositoryFilePathsInternal("src");

        return _pathsToAddress.Keys;
    }

    private async Task GetRepositoryFilePathsInternal(string url)
    {
        if (url is null or { Length: 0 })
        {
            return;
        }
        
        var response = await GetSrc(_httpClient, url);
        if (response is null or { Values.Count: 0 })
        {
            return;
        }

        foreach (var responseValue in response.Values)
        {
            if (responseValue.IsCommitFile)
            {
                _pathsToAddress?.TryAdd(responseValue.Path, responseValue.Links.Self.Href);
            }
            else
            {
                await GetRepositoryFilePathsInternal(responseValue.Links.Self.Href);
            }
        }
    }

    public async Task<FileContent> GetFileContent(string filePath)
    {
        var content = await GetFileContentRaw(filePath);
        return new FileContent(content.GetLines(), filePath);
    }

    public async Task<string> GetFileContentRaw(string filePath)
    {
        if (_pathsToAddress is {})
        {
            if (!_pathsToAddress.TryGetValue(filePath, out var link))
            {
                throw new ArgumentException($"Given file path ({filePath}) could not be find among repository paths.", nameof(filePath));
            }

            using var response = await _httpClient.GetAsync(link);
            //TODO: Handle error
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        //We didn't collect file paths yet. We will call method to do this, and try again 
        _ = await GetRepositoryFilePaths();
        return await GetFileContentRaw(filePath);
    }

    public async Task<Commit> CreateCommit(string branch, string commitMessage, List<(string filePath, string content)> files)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(commitMessage), "message");
        form.Add(new StringContent(branch), "branch");
        foreach (var (filePath, content) in files)
        {
            form.Add(new StringContent(content), filePath);
        }
        
        using var response = await _httpClient.PostAsync("src", form);
        //TODO: Handle error
        response.EnsureSuccessStatusCode();
        return new Commit();
    }

    public Task<PullRequest> CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description)
    {
        throw new NotImplementedException();
    }

    public Task<PullRequest> CreatePullRequest(string sourceBranch, string name, string description)
    {
        throw new NotImplementedException();
    }

    private static async Task<GetSrcResponse?> GetSrc(HttpClient httpClient, string url)
    {
        using var response = await httpClient.GetAsync(url);
        var r = await response.Content.ReadAsStringAsync();
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await response.Content.ReadFromJsonAsync<GetSrcResponse>(),
            //We need to check it, as requests in v2 are being redirected and http client by design is not sending authentication headers to redirected address. Bitbucket is responding with 404 in such situation.
            HttpStatusCode.NotFound when !url.Equals(response.RequestMessage!.RequestUri?.ToString()) => await GetSrc(httpClient, response.RequestMessage.RequestUri!.ToString()),
            _ => null
        };
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    internal class GetSrcResponse
    {
        public ICollection<Value> Values { get; set; }

        // ReSharper disable once ClassNeverInstantiated.Local
        public class Value
        {
            public string Path { get; set; }
            
            public string Type { get; set; }

            public bool IsCommitFile => Type.Equals("commit_file", StringComparison.InvariantCultureIgnoreCase); 
         
            public Link Links { get; set; }
            
            public class Link
            {
                public Self Self { get; set; }
            }
        }
        
        // ReSharper disable once ClassNeverInstantiated.Local
        public class Self
        {
            public string Href { get; set; }
        }
    }
}