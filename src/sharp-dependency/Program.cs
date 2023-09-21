using System.Net.Http.Json;

interface IRepositoryManger
{
    Task<IEnumerable<string>> GetRepositoryFilePaths();
    Task<FileContent> GetFileContent(string filePath);
    Task<Commit> EditFile(string branch, string commitMessage, string content, string filePath);
    Task CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description);
}

class RepositoryManager : IRepositoryManger
{
    private readonly HttpClient _httpClient;
    private const int PathsLimit = 1000;

    public RepositoryManager(string baseUrl, string repositoryName, string projectName)
    {
        _httpClient = new HttpClient(){BaseAddress = new Uri($"{baseUrl}/rest/api/latest/projects/{projectName}/repos/{repositoryName}")};
    }

    public async Task<IEnumerable<string>> GetRepositoryFilePaths()
    {
        var response = await _httpClient.GetFromJsonAsync<GetRepositoryFilePathsResponse>($"/files?limit={PathsLimit}");
        return response?.Values ?? Enumerable.Empty<string>();
    }

    public async Task<FileContent> GetFileContent(string filePath)
    {
        var response = await _httpClient.GetFromJsonAsync<GetFileContentResponse>($"/browse/{filePath}");
        return new FileContent() { Lines = response?.Lines.Select(x => x.Text) ?? Enumerable.Empty<string>() };
    }
    
    public async Task<Commit> EditFile(string branch, string commitMessage, string content, string filePath)
    {
        using var request = new MultipartFormDataContent();
        request.Add(new StringContent(branch), "branch");
        request.Add(new StringContent(commitMessage), "message");
        request.Add(new StringContent(content), "content");
        
        var response = await _httpClient.PutAsync($"/browse/{filePath}", request);
        response.EnsureSuccessStatusCode();

        var editFileResponse = await response.Content.ReadFromJsonAsync<EditFileResponse>();
        return new Commit(){ Id = editFileResponse?.Id ?? string.Empty };
    }

    public Task CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description)
    {
        throw new NotImplementedException();
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

internal class FileContent
{
    public IEnumerable<string> Lines { get; set; }
}

internal class Commit
{
    public string Id { get; set; }
}

interface IFetcher
{
    IEnumerable<DependencyFile> FetchFiles();
}



internal class Repository
{
    private string Path { get; set; }
}


internal class DependencyFile
{
}

internal class CsProjFile : DependencyFile
{
    
}