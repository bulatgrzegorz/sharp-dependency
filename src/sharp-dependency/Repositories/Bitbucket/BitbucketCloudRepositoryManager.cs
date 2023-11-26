using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace sharp_dependency.Repositories.Bitbucket;

//https://developer.atlassian.com/cloud/bitbucket/rest/intro
public class BitbucketCloudRepositoryManager: IRepositoryManger
{
    private readonly HttpClient _httpClient;
    private Dictionary<string, string>? _pathsToAddress;
    private readonly string _repositoryName;
    private readonly string _workspace;

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
        Guard.ThrowIfNullOrWhiteSpace(baseUrl);
        Guard.ThrowIfNullOrWhiteSpace(workspace);
        Guard.ThrowIfNullOrWhiteSpace(repository);
        _repositoryName = repository;
        _workspace = workspace;
        _httpClient = CreateHttpClient(baseUrl, workspace, repository);
    }

    //Used in tests
    internal BitbucketCloudRepositoryManager(string workspace, string repository, HttpClient httpClient)
    {
        _workspace = workspace;
        _repositoryName = repository;
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
            if (!response.IsSuccessStatusCode)
            {
                throw CreateException($"Could not collect file content {filePath})", response.ReasonPhrase);
            }

            return await response.Content.ReadAsStringAsync();
        }

        //We didn't collect file paths yet. We will call method to do this, and try again 
        _ = await GetRepositoryFilePaths();
        return await GetFileContentRaw(filePath);
    }

    public async Task<Commit> CreateCommit(string branch, string commitMessage, List<UpdatedProject> files)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(commitMessage), "message");
        form.Add(new StringContent(branch), "branch");
        foreach (var updatedProject in files)
        {
            form.Add(new StringContent(updatedProject.UpdatedContent), updatedProject.Name);
        }
        
        using var response = await _httpClient.PostAsync("src", form);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException($"Could not create commit (on branch {branch} with message: {commitMessage})", response.ReasonPhrase);
        }
        
        return new Commit();
    }

    public async Task<PullRequest> CreatePullRequest(string sourceBranch, string targetBranch, string name, string description)
    {
        var response = await _httpClient.PostAsJsonAsync("pullrequests", new
        {
            title = name,
            description,
            close_source_branch = true,
            source = new
            {
                branch = new
                {
                    name = sourceBranch
                }
            },
            destination = new
            {
                branch = new
                {
                    name = targetBranch
                }
            }
        });
        
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException($"Could not create pull-request (from branch {sourceBranch})", response.ReasonPhrase);
        }

        var content = await response.Content.ReadFromJsonAsync<PullRequest>();

        Console.WriteLine("Created pull-request ({0}) with id ({1})", name, content?.Id);
        
        return content!;
    }

    public async Task<PullRequest> CreatePullRequest(string sourceBranch, string name, string description)
    {
        var response = await _httpClient.PostAsJsonAsync("pullrequests", new
        {
            rendered = new
            {
                description = new
                {
                    raw = description,
                    markup = "markdown"
                }
            },
            title = name,
            description,
            close_source_branch = true,
            source = new
            {
                branch = new
                {
                    name = sourceBranch
                }
            }
        });
        
        if (!response.IsSuccessStatusCode)
        {
            var r = await response.Content.ReadAsStringAsync();
            throw CreateException($"Could not create pull-request (from branch {sourceBranch})", response.ReasonPhrase);
        }

        var content = await response.Content.ReadFromJsonAsync<PullRequest>();

        Console.WriteLine("Created pull-request ({0}) with id ({1})", name, content?.Id);
        
        return content!;
    }

    public Task<PullRequest> CreatePullRequest(CreatePullRequest request)
    {
        return CreatePullRequest(request.SourceBranch, request.Name, ContentFormatter.FormatPullRequestDescription(request.Description));
    }

    public async Task<PullRequest> CreatePullRequest(string name, string branch, string commitMessage, List<UpdatedProject> updatedProjects)
    {
        await CreateCommit(branch, commitMessage, updatedProjects);
        
        var description = new Description()
        {
            UpdatedProjects = updatedProjects
        };

        //TODO: Refactor pull request name
        return await CreatePullRequest(new CreatePullRequest() { Name = name, SourceBranch = branch, Description = description });
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

    private Exception CreateException(string messagePrefix, string? innerMessage)
    {
        var renderedInnerMessage = innerMessage is null ? null : $" {Environment.NewLine}Inner message: {innerMessage}"; 
        throw new Exception($"{messagePrefix} on repository {_repositoryName} in {_workspace}. Sharp-dependency can not proceed.{renderedInnerMessage}");
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    internal class GetSrcResponse
    {
        public ICollection<Value> Values { get; set; } = null!;

        // ReSharper disable once ClassNeverInstantiated.Local
        public class Value
        {
            public string Path { get; set; } = null!;

            public string Type { get; set; } = null!;

            public bool IsCommitFile => Type.Equals("commit_file", StringComparison.InvariantCultureIgnoreCase); 
         
            public Link Links { get; set; } = null!;

            public class Link
            {
                public Self Self { get; set; } = null!;
            }
        }
        
        // ReSharper disable once ClassNeverInstantiated.Local
        public class Self
        {
            public string Href { get; set; } = null!;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}