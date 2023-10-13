using System.Net;
using System.Net.Http.Json;
using RichardSzalay.MockHttp;
using sharp_dependency.Repositories.Bitbucket;

namespace sharp_dependency.UnitTests;

public class BitbucketCloudRepositoryManagerTests
{
    private const string WorkspaceName = "workspace";
    private const string RepositoryName = "example";

    [Fact]
    public async Task GetFileContentRaw_WillFindSpecificPath_AndCallHttpClientWithLink()
    {
        var link = "https://example/link";

        var response = CreateSrcResponse("link/file", "commit_file", link);

        var responseContent = Guid.NewGuid().ToString();
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(link)
            .Respond(HttpStatusCode.OK, new StringContent(responseContent));

        var baseAddress = "https://example.com/";
        mockHttp
            .When($"{baseAddress}src")
            .Respond(HttpStatusCode.OK, JsonContent.Create(response));

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(baseAddress);
        var manager = new BitbucketCloudRepositoryManager(WorkspaceName, RepositoryName, httpClient);
        var content = await manager.GetFileContentRaw("link/file");
        
        Assert.Equal(responseContent, content);
    }
    
    [Fact]
    public async Task GetRepositoryFilePaths_WillCollectAllPaths()
    {
        var mockHttp = new MockHttpMessageHandler();
        var baseAddress = "https://example.com/";

        var firstLevelResponse = CreateSrcResponse(("link/file", "commit_file", "https://link/file"), ("proj", "commit_directory", "https://example.com/proj"));
        mockHttp
            .When($"{baseAddress}src")
            .Respond(HttpStatusCode.OK, JsonContent.Create(firstLevelResponse));

        var secondLevelResponse = CreateSrcResponse("proj/file1", "commit_file", "https://example.com/proj/file1");
        mockHttp
            .When("https://example.com/proj")
            .Respond(HttpStatusCode.OK, JsonContent.Create(secondLevelResponse));

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(baseAddress);
        var manager = new BitbucketCloudRepositoryManager(WorkspaceName, RepositoryName, httpClient);
        var paths = await manager.GetRepositoryFilePaths();
        
        Assert.Equal(new[]{"link/file", "proj/file1"}, paths);
    }

    private static BitbucketCloudRepositoryManager.GetSrcResponse CreateSrcResponse(string path, string type, string link)
    {
        return new BitbucketCloudRepositoryManager.GetSrcResponse()
        {
            Values = new List<BitbucketCloudRepositoryManager.GetSrcResponse.Value>()
            {
                new()
                {
                    Path = path,
                    Type = type,
                    Links = new BitbucketCloudRepositoryManager.GetSrcResponse.Value.Link()
                    {
                        Self = new BitbucketCloudRepositoryManager.GetSrcResponse.Self()
                        {
                            Href = link
                        }
                    }
                }
            }
        };
    }
    
    private static BitbucketCloudRepositoryManager.GetSrcResponse CreateSrcResponse((string path, string type, string link) val1, (string path, string type, string link) val2)
    {
        return new BitbucketCloudRepositoryManager.GetSrcResponse()
        {
            Values = new List<BitbucketCloudRepositoryManager.GetSrcResponse.Value>()
            {
                new()
                {
                    Path = val1.path,
                    Type = val1.type,
                    Links = new BitbucketCloudRepositoryManager.GetSrcResponse.Value.Link()
                    {
                        Self = new BitbucketCloudRepositoryManager.GetSrcResponse.Self()
                        {
                            Href = val1.link
                        }
                    }
                },
                new()
                {
                    Path = val2.path,
                    Type = val2.type,
                    Links = new BitbucketCloudRepositoryManager.GetSrcResponse.Value.Link()
                    {
                        Self = new BitbucketCloudRepositoryManager.GetSrcResponse.Self()
                        {
                            Href = val2.link
                        }
                    }
                }
            }
        };
    }
}