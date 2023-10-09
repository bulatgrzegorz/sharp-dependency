using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace sharp_dependency.Parsers;

public class ProjectFileParser : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new(false);
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
        //There is no way (other then cloning repository I think) to know if file was saved with BOM or not.
        //Because of it we basically had to choose neither to add byte-order marker or not.
        //Based on conversations like: https://github.com/dotnet/aspnetcore/issues/28697 we are not going to add it.
        await using var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings() { Encoding = Utf8EncodingWithoutBom, OmitXmlDeclaration = true, Async = true});
        
        await _xmlFile.WriteToAsync(xmlWriter, CancellationToken.None);
        await xmlWriter.FlushAsync();

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
    
    public async Task<ProjectFile> Parse()
    {
        await Init();

        List<string> ParseTargetFramework()
        {
            var result = new List<string>();
            
            var targetFramework = _xmlFile.XPathSelectElement("PropertyGroup/TargetFramework")?.Value;
            if (targetFramework is not null)
            {
                result.Add(targetFramework);

                return result;
            }

            var targetFrameworks = _xmlFile.XPathSelectElement("PropertyGroup/TargetFrameworks")?.Value;
            if (targetFrameworks is not null)
            {
                return targetFrameworks.Split(';').ToList();
            }

            return result;
        }

        var targetFrameworks = ParseTargetFramework();
        var packageReferences = _xmlFile.XPathSelectElements("ItemGroup/PackageReference").ToList();

        var dependencies = packageReferences.Select(ParseDependency).Where(x => x is not null).ToList();

        return new ProjectFile(dependencies!, targetFrameworks);
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