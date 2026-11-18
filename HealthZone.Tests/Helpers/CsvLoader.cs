public static class CsvLoader
{
    // Vraća redove CSV fajla iz TestData/csv kao nizove stringova, bez zaglavlja.
    public static IEnumerable<string[]> Ucitaj(string fajl)
    {
        var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "csv", fajl);
        return File.ReadAllLines(putanja)
                   .Skip(1)
                   .Where(linija => !string.IsNullOrWhiteSpace(linija))
                   .Select(linija => linija.Split(';'));
    }
}
