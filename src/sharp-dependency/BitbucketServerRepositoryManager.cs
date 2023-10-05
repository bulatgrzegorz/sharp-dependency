using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace sharp_dependency;

//TODO: Need also think about determining if pull request for some specific change was already created and we do not need to create another one.
//There is also possibility that pull request should be updated, because another dependency update has been found 
//https://developer.atlassian.com/server/bitbucket/rest/v811/intro/#about
public class BitbucketServerRepositoryManager : IRepositoryManger
{
    private readonly HttpClient _httpClient;
    private const int PathsLimit = 1000;
    private readonly string _branchApiAddress;
    private readonly string _repositoryName;
    private readonly string _projectName;
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    public BitbucketServerRepositoryManager(string baseUrl, string repositoryName, string projectName, string authorizationToken) : this(baseUrl, repositoryName, projectName)
    {
        _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {authorizationToken}");
        //methods that accept multipart/form-data will only process requests with X-Atlassian-Token: no-check header.
        _httpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }
    
    public BitbucketServerRepositoryManager(string baseUrl, string repositoryName, string projectName, (string userName, string password) credentials) : this(baseUrl, repositoryName, projectName)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.userName}:{credentials.password}"));
        _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Basic {header}");
        //methods that accept multipart/form-data will only process requests with X-Atlassian-Token: no-check header.
        _httpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }
    
    public BitbucketServerRepositoryManager(string baseUrl, string repositoryName, string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        _repositoryName = repositoryName;
        _projectName = projectName;
        _branchApiAddress = $"{baseUrl}/rest/branch-utils/latest/projects/{projectName}/repos/{repositoryName}/branches/";
        _httpClient = new HttpClient(){BaseAddress = new Uri($"{baseUrl}/rest/api/latest/projects/{projectName}/repos/{repositoryName}/")};
    }

    public async Task<IEnumerable<string>> GetRepositoryFilePaths()
    {
        //TODO: We should make those as long as there are still files to be collected
        var response = await _httpClient.GetFromJsonAsync<GetRepositoryFilePathsResponse>($"files?limit={PathsLimit}");
        return response?.Values ?? Enumerable.Empty<string>();
    }

    public async Task<string> GetFileContentRaw(string filePath)
    {
        var response = await _httpClient.GetAsync($"raw/{filePath}");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<FileContent> GetFileContent(string filePath)
    {
        var response = await _httpClient.GetFromJsonAsync<GetFileContentResponse>($"browse/{filePath}");
        return new FileContent(response?.Lines.Select(x => x.Text) ?? Enumerable.Empty<string>(), filePath);
    }

    private async Task<CreateCommitResponse?> CreateCommit(string branch, string sourceCommitId, string commitMessage, string content, string filePath)
    {
        using var request = new MultipartFormDataContent();
        request.Add(new StringContent(branch), "branch");
        request.Add(new StringContent(commitMessage), "message");
        request.Add(new StringContent(content), "content");
        request.Add(new StringContent(sourceCommitId), "sourceCommitId");
        
        using var response = await _httpClient.PutAsync($"browse/{filePath}", request);
        //TODO: Handle error
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CreateCommitResponse>();
    }
    
    public async Task<Commit> CreateCommit(string branch, string commitMessage, List<(string filePath, string content)> files)
    {
        var mainTip = await GetDefaultBranch();
        //TODO: What if branch already exists? Maybe we should create branch at beginning (if not exists) and then just do everything on it 
        await CreateBranchIfNotExists(branch, mainTip.Id);

        var commitId = mainTip.LatestCommit;
        foreach (var (filePath, content) in files)
        {
            var createCommitResponse = await CreateCommit(branch, commitId, commitMessage, content, filePath);
            if (createCommitResponse is null)
            {
                Console.WriteLine("Something went wrong while creating commit [{0}]({1}) on file: {2}", branch, commitMessage, filePath);
                throw new Exception($"Something went wrong while creating commit [{branch}]({commitMessage}) on file: {filePath}");
            }
            
            commitId = createCommitResponse.Id;
        }

        return new Commit();
    }
    
    private async Task<GetBranchesResponse.Branch?> GetBranch(string branchName)
    {
        var branchNameForSearch = branchName.StartsWith("refs/heads/") ? branchName : $"refs/heads/{branchName}";

        var getBranchesTask = new Lazy<Task<IReadOnlyCollection<GetBranchesResponse.Branch>>>(GetBranchesInternal);
        var branches = await _memoryCache.GetOrCreate("branches", _ => getBranchesTask)!.Value;

        return branches.SingleOrDefault(x => x.Id.Equals(branchNameForSearch, StringComparison.InvariantCultureIgnoreCase));
    }

    private async Task<GetBranchesResponse.Branch> GetDefaultBranch()
    {
        var getBranchesTask = new Lazy<Task<IReadOnlyCollection<GetBranchesResponse.Branch>>>(GetBranchesInternal);
        var branches = await _memoryCache.GetOrCreate("branches", _ => getBranchesTask)!.Value;

        var defaultBranch = branches.SingleOrDefault(x => x.IsDefault);
        if (defaultBranch is null)
        {
            throw new Exception($"Could not find default branch in repository {_repositoryName} in project {_projectName}. Sharp-dependency cannot proceed without it.");
        }

        return defaultBranch;
    }

    private async Task<IReadOnlyCollection<GetBranchesResponse.Branch>> GetBranchesInternal()
    {
        var response = await _httpClient.GetFromJsonAsync<GetBranchesResponse>($"branches?limit={PathsLimit}");
        return response?.Values.Where(x => x.IsBranch).ToList() ?? new List<GetBranchesResponse.Branch>();
    }

    private async Task CreateBranchIfNotExists(string branchName, string fromBranch)
    {
        using var branchResponse = await _httpClient.GetAsync($"{_branchApiAddress}info/{branchName}");
        if (branchResponse.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        using var createBranchResponse = await _httpClient.PostAsJsonAsync($"{_branchApiAddress}", new { name = branchName, startPoint = fromBranch });
        if (createBranchResponse.StatusCode == HttpStatusCode.Created)
        {
            return;
        }

        //TODO: Handle error
        createBranchResponse.EnsureSuccessStatusCode();
    }

    public Task CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description)
    {
        throw new NotImplementedException();
    }

    private class CreateCommitResponse
    {
        public string Id { get; set; }
    }
    
    private class GetBranchesResponse
    {
        public IEnumerable<Branch> Values { get; set; }

        public class Branch
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public bool IsDefault { get; set; }
            public string LatestCommit { get; set; }
            public bool IsBranch => Type.Equals("BRANCH", StringComparison.InvariantCultureIgnoreCase);
        }
    }
    
    private class GetRepositoryFilePathsResponse
    {
        public IEnumerable<string> Values { get; set; }
    }
    
    private class GetFileContentResponse
    {
        public IEnumerable<GetFileContentResponseLine> Lines { get; set; }

        internal class GetFileContentResponseLine
        {
            public string Text { get; set; }
        }
    }
    
    private class EditFileResponse
    {
        public string Id { get; set; }
    }
}