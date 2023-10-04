using System.Net;
using System.Net.Http.Json;
using RichardSzalay.MockHttp;

namespace sharp_dependency.UnitTests;

public class BitbucketCloudRepositoryManagerTests
{
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
        var manager = new BitbucketCloudRepositoryManager(httpClient);
        var content = await manager.GetFileContentRaw("link/file");
        
        Assert.Equal(responseContent, content);
    }
    
    [Fact]
    public async Task GetFileContentRaw_WillFindSpecificPath_WhenDifferentSlash_AndCallHttpClientWithLink()
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
        var manager = new BitbucketCloudRepositoryManager(httpClient);
        var content = await manager.GetFileContentRaw("link\\file");
        
        Assert.Equal(responseContent, content);
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
}