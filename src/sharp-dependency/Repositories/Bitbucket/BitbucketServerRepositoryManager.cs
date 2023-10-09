using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace sharp_dependency.Repositories.Bitbucket;

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
        //TODO: Refactor this, we could create separate http client for branch requests - or use shorter base address 
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

        foreach (var (filePath, content) in files)
        {
            var createCommitResponse = await CreateCommit(branch, commitId, commitMessage, content, filePath);
            if (createCommitResponse is null)
            {
                Console.WriteLine("Something went wrong while creating commit [{0}]({1}) on file: {2}", branch, commitMessage, filePath);
                throw new Exception($"Something went wrong while creating commit [{branch}]({commitMessage}) on file: {filePath}");
            }

            Console.WriteLine($"Change ({filePath}) committed on ({createCommitResponse.Id}).");
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
            throw new Exception($"Could not find default branch in repository {_repositoryName} in project {_projectName}. Sharp-dependency cannot proceed without it.");
        }

        return defaultBranch;
    }

    private async Task<IReadOnlyCollection<Branch>> GetBranchesInternal()
    {
        var response = await _httpClient.GetFromJsonAsync<GetBranchesResponse>($"branches?limit={PathsLimit}");
        return response?.Values.Where(x => x.IsBranch).ToList() ?? new List<Branch>();
    }

    private async Task<Branch> CreateBranch(string branchName, string fromBranch)
    {
        using var createBranchResponse = await _httpClient.PostAsJsonAsync($"{_branchApiAddress}", new { name = branchName, startPoint = fromBranch });
        if (createBranchResponse.StatusCode == HttpStatusCode.Created)
        {
            _memoryCache.Remove("branches");
            return (await createBranchResponse.Content.ReadFromJsonAsync<Branch>())!;
        }

        throw new Exception($"Could not create branch {branchName} from start point branch {fromBranch}. Sharp-dependency can not proceed.");
    }

    public async Task<PullRequest> CreatePullRequest(string sourceBranch, string targetBranch, string name, string description)
    {
        var repositoryInfo = await GetRepository();

        var response = await _httpClient.PostAsJsonAsync("pull-requests", new
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
            throw new Exception($"Could not create pull-request ({sourceBranch} -> {targetBranch}) on repository {_repositoryName} in {_projectName}. Sharp-dependency can not proceed.");
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

    private async Task<GetRepositoryResponse> GetRepository()
    {
        var response = await _httpClient.GetAsync((string?)default);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<GetRepositoryResponse>();
            if (content is not null)
            {
                return content;
            }
        }

        throw new Exception($"Could not collect information about repository {_repositoryName} in project {_projectName}. Sharp-dependency cannot proceed.");
    }

    private class GetRepositoryResponse
    {
        public string Slug { get; set; }
        public string Name { get; set; }
        public ProjectRef Project { get; set; }
        public class ProjectRef
        {
            public string Key { get; set; }
        }
    }

    private class CreateCommitResponse
    {
        public string Id { get; set; }
    }
    
    private class GetBranchesResponse
    {
        public IEnumerable<Branch> Values { get; set; }
    }

    private class Branch
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public bool IsDefault { get; set; }
        public string LatestCommit { get; set; }
        public bool IsBranch => Type.Equals("BRANCH", StringComparison.InvariantCultureIgnoreCase);
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

    public void Dispose()
    {
        _httpClient.Dispose();
        _memoryCache.Dispose();
    }
}