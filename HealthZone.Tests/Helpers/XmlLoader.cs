using System.Xml.Linq;

public static class XmlLoader
{
    // Vraća sve elemente zadanog imena iz XML fajla u TestData/xml.
    public static IEnumerable<XElement> Ucitaj(string fajl, string element)
    {
        var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "xml", fajl);
        return XDocument.Load(putanja).Descendants(element);
    }
}
