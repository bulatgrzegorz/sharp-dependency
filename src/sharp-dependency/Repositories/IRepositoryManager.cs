﻿namespace sharp_dependency.Repositories;

public interface IRepositoryManger : IDisposable
{
    Task<IEnumerable<string>> GetRepositoryFilePaths();
    Task<FileContent> GetFileContent(string filePath);
    Task<string> GetFileContentRaw(string filePath);
    Task<Commit> CreateCommit(string branch, string commitMessage, List<(string filePath, string content)> files);
    Task<PullRequest> CreatePullRequest(CreatePullRequest request);
}