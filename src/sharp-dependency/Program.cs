using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NuGet.Versioning;

//TODO: Some configuration file, where it will be possible to configure which dependencies and how should be updated
//TODO: CLI tool - update remote repository (with PR)
//TODO: Implementations as Bitbucket should be more generic, allowing user to configure many of them with auth method
//TODO: Support directory.build.props files

public class Dependency
{
    public Dependency(string name, string currentVersion, Action<string> updateVersionMethod)
    {
        Name = name;
        CurrentVersion = currentVersion;
        UpdateVersionMethod = updateVersionMethod;
        CurrentNugetVersion = NuGetVersion.Parse(CurrentVersion);
        VersionRange = new VersionRange(CurrentNugetVersion, new FloatRange(NuGetVersionFloatBehavior.Major, CurrentNugetVersion));
    }

    public bool UpdateVersionIfPossible(IReadOnlyCollection<NuGetVersion> allVersions, [NotNullWhen(true)] out NuGetVersion? newVersion)
    {
        newVersion = null;
        var versionToUpdate = VersionRange.FindBestMatch(allVersions);
        if (versionToUpdate is null)
        {
            return false;
        }

        if (!VersionRange.IsBetter(CurrentNugetVersion, versionToUpdate))
        {
            return false;
        }

        newVersion = versionToUpdate;
        UpdateVersionMethod(versionToUpdate.ToNormalizedString());

        return true;
    }

    public string Name { get; }
    public string CurrentVersion { get; }
    private Action<string> UpdateVersionMethod { get; }
    private VersionRange VersionRange { get; }
    private NuGetVersion CurrentNugetVersion { get; }
}

interface IFileParser
{
    Task<ProjectFile> Parse();
    Task<string> Generate();
}

interface IRepositoryManger
{
    Task<IEnumerable<string>> GetRepositoryFilePaths();
    Task<FileContent> GetFileContent(string filePath);
    Task<Commit> EditFile(string branch, string commitMessage, string content, string filePath);
    Task CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description);
}

//TODO: Need also think about determining if pull request for some specific change was already created and we do not need to create another one.
//There is also possibility that pull request should be updated, because another dependency update has been found 
class BitbucketRepositoryManager : IRepositoryManger
{
    private readonly HttpClient _httpClient;
    private const int PathsLimit = 1000;

    public BitbucketRepositoryManager(string baseUrl, string authorizationToken, string repositoryName, string projectName)
    {
        _httpClient = new HttpClient(){BaseAddress = new Uri($"{baseUrl}/rest/api/latest/projects/{projectName}/repos/{repositoryName}/")};
        _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {authorizationToken}");
        //methods that accept multipart/form-data will only process requests with X-Atlassian-Token: no-check header.
        _httpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }

    public async Task<IEnumerable<string>> GetRepositoryFilePaths()
    {
        //TODO: We should make those as long as there are still files to be collected
        var response = await _httpClient.GetFromJsonAsync<GetRepositoryFilePathsResponse>($"files?limit={PathsLimit}");
        return response?.Values ?? Enumerable.Empty<string>();
    }

    public async Task<FileContent> GetFileContent(string filePath)
    {
        var response = await _httpClient.GetFromJsonAsync<GetFileContentResponse>($"browse/{filePath}");
        return new FileContent(response?.Lines.Select(x => x.Text) ?? Enumerable.Empty<string>(), filePath);
    }
    
    public async Task<Commit> EditFile(string branch, string commitMessage, string content, string filePath)
    {
        using var request = new MultipartFormDataContent();
        request.Add(new StringContent(branch), "branch");
        request.Add(new StringContent(commitMessage), "message");
        request.Add(new StringContent(content), "content");
        
        var response = await _httpClient.PutAsync($"browse/{filePath}", request);
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

public class ProjectFile
{
    public ProjectFile(IReadOnlyCollection<Dependency> dependencies, IReadOnlyCollection<string> targetFrameworks)
    {
        Dependencies = dependencies;
        TargetFrameworks = targetFrameworks;
    }

    public IReadOnlyCollection<Dependency> Dependencies { get; private set; }
    public IReadOnlyCollection<string> TargetFrameworks { get; private set; }
}

public class FileContent
{
    public FileContent(IEnumerable<string> lines, string path)
    {
        Lines = lines;
        Path = path;
    }

    public static FileContent Create(string path)
    {
        return new FileContent(File.ReadAllLines(path), path);
    } 
    
    public IEnumerable<string> Lines { get; private set; }
    public string Path { get; private set; }
}

internal class Commit
{
    public string Id { get; set; }
}