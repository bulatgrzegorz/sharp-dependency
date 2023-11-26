namespace sharp_dependency.Repositories;

public interface IRepositoryManger : IDisposable
{
    Task<IEnumerable<string>> GetRepositoryFilePaths();
    Task<FileContent> GetFileContent(string filePath);
    Task<string> GetFileContentRaw(string filePath);
    Task<Commit> CreateCommit(string branch, string commitMessage, List<UpdatedProject> files);
    Task<PullRequest> CreatePullRequest(CreatePullRequest request);
    Task<PullRequest> CreatePullRequest(string name, string branch, string commitMessage, List<UpdatedProject> updatedProjects);
}