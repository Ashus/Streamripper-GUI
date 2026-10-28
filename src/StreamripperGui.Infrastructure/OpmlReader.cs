namespace StreamripperGui.Infrastructure;

public static class OpmlReader
{
    public static IReadOnlyList<Core.Models.OpmlStation> Read(Stream stream)
    {
        var settings = new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 16 * 1024 * 1024,
            CloseInput = false
        };
        using var reader = System.Xml.XmlReader.Create(stream, settings);
        var document = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.SetLineInfo);
        if (document.Root?.Name != "opml") throw new InvalidDataException("The file is not an OPML document.");
        var version = (string?)document.Root.Attribute("version");
        if (version is not ("1.0" or "1.1" or "2.0")) throw new InvalidDataException("Supported OPML versions are 1.0, 1.1 and 2.0.");
        var bodies = document.Root.Elements("body").ToArray();
        if (bodies.Length != 1) throw new InvalidDataException("OPML must contain exactly one body element.");
        var result = new List<Core.Models.OpmlStation>();
        foreach (var outline in bodies[0].Descendants("outline"))
        {
            var urlAttribute = outline.Attribute("url");
            var type = (string?)outline.Attribute("type");
            if (urlAttribute is null && !string.Equals(type, "link", StringComparison.OrdinalIgnoreCase)) continue;
            var line = ((System.Xml.IXmlLineInfo)outline).LineNumber;
            if (type is not null && !string.Equals(type, "link", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Line {line}: unsupported station outline type '{type}'.");
            var name = ((string?)outline.Attribute("text"))?.Trim();
            var url = ((string?)urlAttribute)?.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                throw new InvalidDataException($"Line {line}: station text and url are required.");
            try { Core.Models.StationCatalog.Edit(new(), name, url, []); }
            catch (ArgumentException exception) { throw new InvalidDataException($"Line {line}, '{name}': {exception.Message}", exception); }
            result.Add(new(name, url, (string?)outline.Attribute("description"), (string?)outline.Attribute("category"), line, result.Count));
        }
        if (result.Count == 0) throw new InvalidDataException("The OPML file contains no supported station links.");
        return result;
    }
}
