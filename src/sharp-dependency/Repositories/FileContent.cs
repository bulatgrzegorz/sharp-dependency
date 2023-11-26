namespace sharp_dependency.Repositories;

public class FileContent
{
    public FileContent(IEnumerable<string> lines, string path)
    {
        Lines = lines;
        Path = path;
    }

    public static FileContent CreateFromLocalPath(string path)
    {
        return new FileContent(File.ReadAllLines(path), path);
    }
    
    public IEnumerable<string> Lines { get; }
    public string Path { get; }
}