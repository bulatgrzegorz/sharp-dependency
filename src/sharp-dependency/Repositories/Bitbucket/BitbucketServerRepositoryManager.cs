using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using sharp_dependency.Logger;

namespace sharp_dependency.Repositories.Bitbucket;

//TODO: Need also think about determining if pull request for some specific change was already created and we do not need to create another one.
//There is also possibility that pull request should be updated, because another dependency update has been found 
//https://developer.atlassian.com/server/bitbucket/rest/v811/intro/#about
public class BitbucketServerRepositoryManager : IRepositoryManger
{
    private readonly HttpClient _apiHttpClient;
    private readonly HttpClient _branchesHttpClient;
    private const int PathsLimit = 1000;
    private readonly string _repositoryName;
    private readonly string _projectName;
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    public BitbucketServerRepositoryManager(string baseUrl, string repositoryName, string projectName, string authorizationToken) : this(baseUrl, repositoryName, projectName)
    {
        var authenticationHeader = AuthenticationHeaderValue.Parse($"Bearer {authorizationToken}");
        _apiHttpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
        _branchesHttpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
        //methods that accept multipart/form-data will only process requests with X-Atlassian-Token: no-check header.
        _apiHttpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }
    
    public BitbucketServerRepositoryManager(string baseUrl, string repositoryName, string projectName, (string userName, string password) credentials) : this(baseUrl, repositoryName, projectName)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.userName}:{credentials.password}"));
        var authenticationHeader = AuthenticationHeaderValue.Parse($"Basic {header}");
        _apiHttpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
        _branchesHttpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
        //methods that accept multipart/form-data will only process requests with X-Atlassian-Token: no-check header.
        _apiHttpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }
    
    public BitbucketServerRepositoryManager(string baseUrl, string repositoryName, string projectName)
    {
        Guard.ThrowIfNullOrWhiteSpace(baseUrl);
        Guard.ThrowIfNullOrWhiteSpace(repositoryName);
        Guard.ThrowIfNullOrWhiteSpace(projectName);
        _repositoryName = repositoryName;
        _projectName = projectName;
        
        _branchesHttpClient = new HttpClient(){BaseAddress = new Uri($"{baseUrl}/rest/branch-utils/latest/projects/{projectName}/repos/{repositoryName}/")}; 
        _apiHttpClient = new HttpClient(){BaseAddress = new Uri($"{baseUrl}/rest/api/latest/projects/{projectName}/repos/{repositoryName}/")};
    }

    public async Task<IEnumerable<string>> GetRepositoryFilePaths()
    {
        Log.LogDebug("Getting repository paths for {0} in project {1}", _repositoryName, _projectName);
        //TODO: We should make those as long as there are still files to be collected
        var response = await _apiHttpClient.GetAsync($"files?limit={PathsLimit}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            //TODO: Check why this is happening, error:
            // "message": "refs/heads/master is set as the default branch, but this branch does not exist",
            // "exceptionName": "com.atlassian.bitbucket.repository.NoDefaultBranchException"
            return ArraySegment<string>.Empty;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<GetRepositoryFilePathsResponse>();
        
        return content?.Values ?? Enumerable.Empty<string>();
    }

    public async Task<string> GetFileContentRaw(string filePath)
    {
        var response = await _apiHttpClient.GetAsync($"raw/{filePath}");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<FileContent> GetFileContent(string filePath)
    {
        var response = await _apiHttpClient.GetFromJsonAsync<GetFileContentResponse>($"browse/{filePath}");
        return new FileContent(response?.Lines.Select(x => x.Text) ?? Enumerable.Empty<string>(), filePath);
    }

    private async Task<CreateCommitResponse?> CreateCommit(string branch, string sourceCommitId, string commitMessage, string content, string filePath)
    {
        using var request = new MultipartFormDataContent();
        request.Add(new StringContent(branch), "branch");
        request.Add(new StringContent(commitMessage), "message");
        request.Add(new StringContent(content), "content");
        request.Add(new StringContent(sourceCommitId), "sourceCommitId");
        
        using var response = await _apiHttpClient.PutAsync($"browse/{filePath}", request);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException($"Could not create commit on {branch} with file {filePath}", null);
        }

        return await response.Content.ReadFromJsonAsync<CreateCommitResponse>();
    }
    
    public async Task<Commit> CreateCommit(string branch, string commitMessage, List<UpdatedProject> files)
    {
        string commitId; 
        var branchToCommitOn = await GetBranch(branch);
        if (branchToCommitOn is null)
        {
            var mainTip = await GetDefaultBranch();
            var createdBranch = await CreateBranch(branch, mainTip.Id);

            commitId = createdBranch.LatestCommit;
        }
        else
        {
            commitId = branchToCommitOn.LatestCommit;
        }

        foreach (var updatedProject in files)
        {
            var createCommitResponse = await CreateCommit(branch, commitId, commitMessage, updatedProject.UpdatedContent, updatedProject.Name);
            if (createCommitResponse is null)
            {
                throw CreateException($"Could not create commit on {branch} with file {updatedProject.Name}", null);
            }

            Console.WriteLine($"Change ({updatedProject.Name}) committed on ({createCommitResponse.Id}).");
            commitId = createCommitResponse.Id;
        }

        return new Commit();
    }
    
    private async Task<Branch?> GetBranch(string branchName)
    {
        var branchNameForSearch = ToRefBranchName(branchName);

        var getBranchesTask = new Lazy<Task<IReadOnlyCollection<Branch>>>(GetBranchesInternal);
        var branches = await _memoryCache.GetOrCreate("branches", _ => getBranchesTask)!.Value;

        return branches.SingleOrDefault(x => x.Id.Equals(branchNameForSearch, StringComparison.InvariantCultureIgnoreCase));
    }

    private static string ToRefBranchName(string branchName) => branchName.StartsWith("refs/heads/") ? branchName : $"refs/heads/{branchName}";

    private async Task<Branch> GetDefaultBranch()
    {
        var getBranchesTask = new Lazy<Task<IReadOnlyCollection<Branch>>>(GetBranchesInternal);
        var branches = await _memoryCache.GetOrCreate("branches", _ => getBranchesTask)!.Value;

        var defaultBranch = branches.SingleOrDefault(x => x.IsDefault);
        if (defaultBranch is null)
        {
            throw CreateException($"Could not find default branch", null);
        }

        return defaultBranch;
    }

    private async Task<IReadOnlyCollection<Branch>> GetBranchesInternal()
    {
        var response = await _apiHttpClient.GetFromJsonAsync<GetBranchesResponse>($"branches?limit={PathsLimit}");
        return response?.Values.Where(x => x.IsBranch).ToList() ?? new List<Branch>();
    }

    private async Task<Branch> CreateBranch(string branchName, string fromBranch)
    {
        using var createBranchResponse = await _branchesHttpClient.PostAsJsonAsync("branches", new { name = branchName, startPoint = fromBranch });
        if (createBranchResponse.StatusCode == HttpStatusCode.Created)
        {
            _memoryCache.Remove("branches");
            return (await createBranchResponse.Content.ReadFromJsonAsync<Branch>())!;
        }

        throw CreateException($"Could not create branch {branchName} from start point branch {fromBranch}", null);
    }

    public async Task<PullRequest> CreatePullRequest(string sourceBranch, string targetBranch, string name, string description)
    {
        var repositoryInfo = await GetRepository();

        var response = await _apiHttpClient.PostAsJsonAsync("pull-requests", new
        {
            title = name,
            description,
            state = "OPEN",
            open = true,
            close = false,
            locked = false,
            fromRef = new
            {
                Id = ToRefBranchName(sourceBranch),
                repository = repositoryInfo
            },
            toRef = new
            {
                Id = ToRefBranchName(targetBranch),
                repository = repositoryInfo
            }
        });

        if (!response.IsSuccessStatusCode)
        {
            throw CreateException($"Could not create pull-request ({sourceBranch} -> {targetBranch})", response.ReasonPhrase);
        }

        var content = await response.Content.ReadFromJsonAsync<PullRequest>();

        Console.WriteLine("Created pull-request ({0}) with id ({1})", name, content?.Id);
        
        return content!;
    }

    public async Task<PullRequest> CreatePullRequest(string sourceBranch, string name, string description)
    {
        var defaultBranch = await GetDefaultBranch();
        return await CreatePullRequest(sourceBranch, defaultBranch.Id, name, description);
    }

    public async Task<PullRequest> CreatePullRequest(CreatePullRequest request)
    {
        var defaultBranch = await GetDefaultBranch();
        return await CreatePullRequest(request.SourceBranch, defaultBranch.Id, request.Name, ContentFormatter.FormatPullRequestDescription(request.Description));
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

    private async Task<GetRepositoryResponse> GetRepository()
    {
        var response = await _apiHttpClient.GetAsync((string?)default);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<GetRepositoryResponse>();
            if (content is not null)
            {
                return content;
            }
        }

        throw CreateException($"Could not collect information about repository", response.ReasonPhrase);
    }
    
    private Exception CreateException(string messagePrefix, string? innerMessage)
    {
        var renderedInnerMessage = innerMessage is null ? null : $" {Environment.NewLine}Inner message: {innerMessage}"; 
        throw new Exception($"{messagePrefix} on repository {_repositoryName} in {_projectName}. Sharp-dependency can not proceed.{renderedInnerMessage}");
    }

    // ReSharper disable ClassNeverInstantiated.Local
    private class GetRepositoryResponse
    {
        public string Slug { get; set; } = null!;
        public string Name { get; set; } = null!;
        public ProjectRef Project { get; set; } = null!;
        
        public class ProjectRef
        {
            public string Key { get; set; } = null!;
        }
    }

    private class CreateCommitResponse
    {
        public string Id { get; set; } = null!;
    }
    
    private class GetBranchesResponse
    {
        public IEnumerable<Branch> Values { get; set; } = null!;
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    // ReSharper disable once MemberCanBePrivate.Local
    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    private class Branch
    {
        public string Id { get; set; } = null!;
        public string Type { get; set; } = null!;
        public bool IsDefault { get; set; }
        public string LatestCommit { get; set; } = null!;
        public bool IsBranch => Type.Equals("BRANCH", StringComparison.InvariantCultureIgnoreCase);
    }
    
    private class GetRepositoryFilePathsResponse
    {
        public IEnumerable<string> Values { get; set; } = null!;
    }
    
    private class GetFileContentResponse
    {
        public IEnumerable<GetFileContentResponseLine> Lines { get; set; } = null!;

        // ReSharper disable once ClassNeverInstantiated.Local
        internal class GetFileContentResponseLine
        {
            public string Text { get; set; } = null!;
        }
    }

    public void Dispose()
    {
        _apiHttpClient.Dispose();
        _branchesHttpClient.Dispose();
        _memoryCache.Dispose();
    }
}