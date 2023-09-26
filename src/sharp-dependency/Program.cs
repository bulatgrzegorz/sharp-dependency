using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGet.Versioning;

Console.WriteLine();


class OnPremiseNugetPackageSourceManger
{
    private static readonly XName XmlEntryName = XName.Get("entry", "http://www.w3.org/2005/Atom");
    private static readonly XName XmlLinkName = XName.Get("link", "http://www.w3.org/2005/Atom");
    private static readonly XName XmlPropertiesName = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
    private static readonly XName XmlVersionName = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");
    
    private readonly HttpClient _httpClient;

    public OnPremiseNugetPackageSourceManger(string address, string token)
    {
        _httpClient = new HttpClient(){BaseAddress = new Uri(address)};
        _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {token}");
    }
    
    public async Task<string?> GetLatestPackageVersionsV2(string packageId, bool includePrerelease = false)
    {
        using var httpResponse = await _httpClient.GetAsync($"FindPackagesById()?id='{packageId}'&semVerLevel=2.0.0");
        await using var responseStream = await httpResponse.Content.ReadAsStreamAsync();

        var xml = await XElement.LoadAsync(responseStream, LoadOptions.None, CancellationToken.None);
        
        var nextLink = xml.Elements(XmlLinkName).FirstOrDefault(x => x.Attribute("rel")?.Value == "next")?.Attribute("href")?.Value;
        while (nextLink is not null)
        {
            using var nextLinkHttpResponse = await _httpClient.GetAsync(nextLink);
            await using var nextLinkResponseStream = await nextLinkHttpResponse.Content.ReadAsStreamAsync();

            var nextLinkXml = await XElement.LoadAsync(nextLinkResponseStream, LoadOptions.None, CancellationToken.None);
            nextLink = nextLinkXml.Elements(XmlLinkName).FirstOrDefault(x => x.Attribute("rel")?.Value == "next")?.Attribute("href")?.Value;

            xml = nextLinkXml;
        }
        
        var versions = xml.Elements(XmlEntryName).Select(x => x.Element(XmlPropertiesName)).Select(x => x?.Element(XmlVersionName)?.Value).ToList();
        if (!includePrerelease)
        {
            versions = versions.Where(x => x is not null && NuGetVersion.TryParse(x, out var nugetVersion) && !nugetVersion.IsPrerelease).ToList();
        }
        
        return versions[^1];
    }
}

class NugetPackageSourceManger
{
    private readonly HttpClient _httpClient;

    public NugetPackageSourceManger()
    {
        _httpClient = new HttpClient(){BaseAddress = new Uri("https://api.nuget.org/")};
    }
    
    public async Task<string?> GetLatestPackageVersions(string packageId, bool includePrerelease = false)
    {
        using var response = await _httpClient.GetAsync($"v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine("ERROR: Could not find package with id: {0} on nuget", packageId);

            return null;
        }

        response.EnsureSuccessStatusCode();

        var getPackageMetadataResponse = await response.Content.ReadFromJsonAsync<GetPackageMetadataResponse>();
        if (getPackageMetadataResponse is null)
        {
            var responseString = response.Content.ReadAsStringAsync();
            Console.WriteLine("ERROR: Could not deserialize response from nuget for package with id: {0}. Response: {1}{2}", packageId, Environment.NewLine, responseString);

            return null;
        }
        
        var versions = getPackageMetadataResponse.Versions.ToList();
        if (!includePrerelease)
        {
            versions = versions.Where(x => NuGetVersion.TryParse(x, out var nugetVersion) && !nugetVersion.IsPrerelease).ToList();
        }

        return versions[^1];
    }
    
    private class GetPackageMetadataResponse
    {
        public IEnumerable<string> Versions { get; set; }
    }
}

public partial class SolutionFileParser
{
    public IReadOnlyCollection<string> GetProjectPaths(FileContent solutionFile)
    {
        var result = new List<string>();
        var solutionFileDirectory = Path.GetDirectoryName(solutionFile.Path);
        foreach (var line in solutionFile.Lines)
        {
            if (!SolutionProjectRegex().IsMatch(line)) continue;
            
            var relativeProjectPath = line.Split("\"")[5];
            if(!ProjectFileExtensionRegex().IsMatch(relativeProjectPath)) continue;
            
            result.Add(string.IsNullOrEmpty(solutionFileDirectory) ? relativeProjectPath : Path.Combine(solutionFileDirectory, relativeProjectPath));
        }
        
        return result;
    }

    [GeneratedRegex(@".+\.[a-z]{2}proj$")]
    private static partial Regex ProjectFileExtensionRegex();
    
    [GeneratedRegex(@"^\s*Project\(")]
    private static partial Regex SolutionProjectRegex();
}

public class ProjectFileParser : IFileParser, IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new UTF8Encoding(false);
    private readonly MemoryStream _fileContent;
    private XElement _xmlFile = null!;
    
    public ProjectFileParser(string content)
    {
        _fileContent = new MemoryStream(Utf8EncodingWithoutBom.GetBytes(content));
    }

    private async Task Init()
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        _xmlFile ??= await XElement.LoadAsync(_fileContent, LoadOptions.PreserveWhitespace, CancellationToken.None);
    }
    
    public async Task<string> Generate()
    {
        using var memoryStream = new MemoryStream();
        await using var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings() { Encoding = Utf8EncodingWithoutBom, OmitXmlDeclaration = true, Async = true});
        
        await _xmlFile.WriteToAsync(xmlWriter, CancellationToken.None);
        await xmlWriter.FlushAsync();

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
    
    public async Task<IReadOnlyCollection<Dependency>> Parse()
    {
        await Init();

        var packageReferences = _xmlFile.XPathSelectElements("ItemGroup/PackageReference").ToList();

        return packageReferences.Select(ParseDependency).Where(x => x is not null).ToList()!;
    }

    private Dependency? ParseDependency(XElement element)
    {
        var name = element.Attribute("Include")?.Value;
        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("Could not determine name of dependency: {0}", element);

            return null;
        }

        (string currentVersion, Action<string> updateMethod) ParseVersion()
        {
            var versionAttribute = element.Attribute("Version");
            if (versionAttribute is not null)
            {
                return (versionAttribute.Value, version => versionAttribute.Value = version);
            }
            
            var versionElement = element.Element("Version");
            if (versionElement is not null)
            {
                return (versionElement.Value, version => versionElement.Value = version);
            }
            
            var versionAttributeLower = element.Attribute("version");
            if (versionAttributeLower is not null)
            {
                return (versionAttributeLower.Value, version => versionAttributeLower.Value = version);
            }
            
            var versionElementLower = element.Element("version");
            if (versionElementLower is not null)
            {
                return (versionElementLower.Value, version => versionElementLower.Value = version);
            }

            return (null, null)!;
        }

        var (currentVersion, updateVersionMethod) = ParseVersion();
        if (string.IsNullOrEmpty(currentVersion))
        {
            Console.WriteLine("Could not determine version of dependency: {0}", element);

            return null;
        }

        return new Dependency(name, currentVersion, updateVersionMethod);
    }
    
    public ValueTask DisposeAsync()
    {
        return _fileContent.DisposeAsync();
    }
}

public class Dependency
{
    public Dependency(string name, string currentVersion, Action<string> updateVersionMethod)
    {
        Name = name;
        CurrentVersion = currentVersion;
        UpdateVersionMethod = updateVersionMethod;
    }

    public void UpdateVersion(string version) => UpdateVersionMethod(version);
    public string Name { get; }
    public string CurrentVersion { get; }
    private Action<string> UpdateVersionMethod { get; }
}

interface IFileParser
{
    Task<IReadOnlyCollection<Dependency>> Parse();
    Task<string> Generate();
}

interface IRepositoryManger
{
    Task<IEnumerable<string>> GetRepositoryFilePaths();
    Task<FileContent> GetFileContent(string filePath);
    Task<Commit> EditFile(string branch, string commitMessage, string content, string filePath);
    Task CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description);
}

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
        var response = await _httpClient.GetFromJsonAsync<GetRepositoryFilePathsResponse>($"files?limit={PathsLimit}");
        return response?.Values ?? Enumerable.Empty<string>();
    }

    public async Task<FileContent> GetFileContent(string filePath)
    {
        var response = await _httpClient.GetFromJsonAsync<GetFileContentResponse>($"browse/{filePath}");
        return new FileContent() { Lines = response?.Lines.Select(x => x.Text) ?? Enumerable.Empty<string>(), Path = filePath };
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

public class FileContent
{
    public IEnumerable<string> Lines { get; set; }
    public string Path { get; set; }
}

internal class Commit
{
    public string Id { get; set; }
}