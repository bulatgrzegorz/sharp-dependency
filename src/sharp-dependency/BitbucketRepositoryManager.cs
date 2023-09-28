﻿using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace sharp_dependency;

//TODO: Need also think about determining if pull request for some specific change was already created and we do not need to create another one.
//There is also possibility that pull request should be updated, because another dependency update has been found 
public class BitbucketRepositoryManager : IRepositoryManger
{
    private readonly HttpClient _httpClient;
    private const int PathsLimit = 1000;

    public BitbucketRepositoryManager(string baseUrl, string authorizationToken, string repositoryName, string projectName)
    {
        Console.WriteLine(Environment.SpecialFolder.LocalApplicationData);
        
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

    public async Task<string> GetFileContentRaw(string filePath)
    {
        var response1 = await _httpClient.GetAsync($"raw/{filePath}");
        return await response1.Content.ReadAsStringAsync();
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