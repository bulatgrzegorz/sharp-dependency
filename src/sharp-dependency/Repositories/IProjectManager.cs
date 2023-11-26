namespace sharp_dependency.Repositories;

public interface IProjectManager
{
    Task<IEnumerable<string>> GetRepositories();
}