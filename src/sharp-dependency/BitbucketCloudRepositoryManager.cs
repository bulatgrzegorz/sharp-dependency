using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Unicode;
using NuGet.Packaging;

namespace sharp_dependency;

//https://developer.atlassian.com/cloud/bitbucket/rest/intro
public class BitbucketCloudRepositoryManager: IRepositoryManger
{
    private HttpClient _httpClient;

    public BitbucketCloudRepositoryManager(string workspace, string repository, string authorizationToken)
    {
        _httpClient = CreateHttpClient(workspace, repository);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);
    }
    
    public BitbucketCloudRepositoryManager(string workspace, string repository, (string userName, string password) credentials)
    {
        _httpClient = CreateHttpClient(workspace, repository);
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.userName}:{credentials.password}"));
        _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Basic {header}");
    }

    public BitbucketCloudRepositoryManager(string workspace, string repository) => CreateHttpClient(workspace, repository);

    private HttpClient CreateHttpClient(string workspace, string repository) => 
        new() { BaseAddress = new Uri($"https://api.bitbucket.org/2.0/repositories/{workspace.ToLowerInvariant()}/{repository.ToLowerInvariant()}/")};

    public async Task<IEnumerable<string>> GetRepositoryFilePaths()
    {
        var filePaths = new ConcurrentDictionary<string, byte>();
        await GetRepositoryFilePathsInternal("src", filePaths);

        return filePaths.Keys;
    }

    private async Task GetRepositoryFilePathsInternal(string url, ConcurrentDictionary<string, byte> paths)
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
                paths.TryAdd(responseValue.Path, 0);
            }
            else
            {
                await GetRepositoryFilePathsInternal(responseValue.Links.Self.Href, paths);
            }
        }
    }

    public Task<FileContent> GetFileContent(string filePath)
    {
        throw new NotImplementedException();
    }

    public Task<Commit> EditFile(string branch, string commitMessage, string content, string filePath)
    {
        throw new NotImplementedException();
    }

    public Task CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description)
    {
        throw new NotImplementedException();
    }

    private static async Task<GetSrcResponse?> GetSrc(HttpClient httpClient, string url)
    {
        using var response = await httpClient.GetAsync(url);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await response.Content.ReadFromJsonAsync<GetSrcResponse>(),
            //We need to check it, as requests in v2 are being redirected and http client by design is not sending authentication headers to redirected address. Bitbucket is responding with 404 in such situation.
            HttpStatusCode.NotFound when !url.Equals(response.RequestMessage!.RequestUri?.ToString()) => await GetSrc(httpClient, response.RequestMessage.RequestUri!.ToString()),
            _ => null
        };
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class GetSrcResponse
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