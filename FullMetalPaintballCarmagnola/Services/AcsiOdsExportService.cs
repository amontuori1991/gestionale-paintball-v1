using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Hosting;

namespace Full_Metal_Paintball_Carmagnola.Services;

public sealed class AcsiOdsExportService
{
    private const string WorkingSheetName = "working";
    private const string ItalianTemplateFileName = "Modello_tesseramento_ACSI.ods";
    private const string ForeignTemplateFileName = "Modello_tesseramento_ACSI_senza_codice_fiscale_italiano.ods";
    private const string AcsiCode = "107743";
    private const string QualificaDefault = "Socio - 2116";
    private const string AssicurazioneDefault = "Base Sport - 102";
    private const string DisciplinaConi1Default = "Attività sportiva ginnastica finalizzata alla salute ed al fitness - BI001";
    private const string DisciplinaAcsi1Default = "PAINTBALL - 514";

    private static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    private readonly string _templateDirectory;
    private readonly Lazy<Dictionary<string, string>> _countryIso2Map;

    public AcsiOdsExportService(IWebHostEnvironment environment)
    {
        _templateDirectory = Path.Combine(environment.ContentRootPath, "Templates", "Exports");
        _countryIso2Map = new Lazy<Dictionary<string, string>>(BuildCountryIso2Map);
    }

    public byte[] CreateArchive(IReadOnlyCollection<Tesseramento> italiani, IReadOnlyCollection<Tesseramento> esteri)
    {
        var italianiFile = CreateFilledWorkbook(
            Path.Combine(_templateDirectory, ItalianTemplateFileName),
            italiani,
            acsiCodeColumn: 3,
            buildRow: BuildItalianRow);

        var esteriFile = CreateFilledWorkbook(
            Path.Combine(_templateDirectory, ForeignTemplateFileName),
            esteri,
            acsiCodeColumn: 4,
            buildRow: BuildForeignRow);

        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddArchiveEntry(archive, "Tesseramenti_ACSI_Italia.ods", italianiFile);
            AddArchiveEntry(archive, "Tesseramenti_ACSI_Estero.ods", esteriFile);
        }

        return archiveStream.ToArray();
    }

    private byte[] CreateFilledWorkbook(
        string templatePath,
        IReadOnlyCollection<Tesseramento> rows,
        int acsiCodeColumn,
        Func<Tesseramento, IReadOnlyList<OdsCellValue>> buildRow)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template ODS non trovato: {templatePath}");
        }

        using var templateStream = File.OpenRead(templatePath);
        using var outputStream = new MemoryStream();
        templateStream.CopyTo(outputStream);
        outputStream.Position = 0;

        using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var contentEntry = archive.GetEntry("content.xml")
                ?? throw new InvalidOperationException("Il template ODS non contiene content.xml.");

            string contentXml;
            using (var reader = new StreamReader(contentEntry.Open(), Encoding.UTF8))
            {
                contentXml = reader.ReadToEnd();
            }

            var document = XDocument.Parse(contentXml, LoadOptions.PreserveWhitespace);
            document.Declaration ??= new XDeclaration("1.0", "UTF-8", null);

            FillWorkingSheet(document, rows, buildRow);
            SetAcsiCode(document, AcsiCode, acsiCodeColumn);

            contentEntry.Delete();
            var newEntry = archive.CreateEntry("content.xml", CompressionLevel.Optimal);
            using var writer = new StreamWriter(newEntry.Open(), new UTF8Encoding(false));
            document.Save(writer, SaveOptions.DisableFormatting);
        }

        return outputStream.ToArray();
    }

    private void FillWorkingSheet(
        XDocument document,
        IReadOnlyCollection<Tesseramento> rows,
        Func<Tesseramento, IReadOnlyList<OdsCellValue>> buildRow)
    {
        var workingTable = document
            .Descendants(TableNs + "table")
            .FirstOrDefault(t => string.Equals((string?)t.Attribute(TableNs + "name"), WorkingSheetName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Foglio '{WorkingSheetName}' non trovato nel template ODS.");

        var rowsInTable = workingTable.Elements(TableNs + "table-row").ToList();
        var headerRowIndex = rowsInTable.FindIndex(IsHeaderRow);
        if (headerRowIndex < 0 || headerRowIndex + 1 >= rowsInTable.Count)
        {
            throw new InvalidOperationException("Intestazione dati non trovata nel template ODS.");
        }

        var templateSourceRow = rowsInTable[headerRowIndex + 1];
        var templateRow = new XElement(templateSourceRow);
        templateRow.Attribute(TableNs + "number-rows-repeated")?.Remove();

        var insertionPoint = templateSourceRow;
        var rowsToRemove = Math.Max(1, rows.Count);
        while (insertionPoint != null && (rowsToRemove > 0 || RowHasUserData(insertionPoint)))
        {
            var nextRow = insertionPoint.ElementsAfterSelf(TableNs + "table-row").FirstOrDefault();
            insertionPoint.Remove();
            insertionPoint = nextRow;
            rowsToRemove--;
        }

        foreach (var tesseramento in rows.OrderBy(t => t.Cognome).ThenBy(t => t.Nome).ThenBy(t => t.DataCreazione))
        {
            var row = new XElement(templateRow);
            PopulateRow(row, buildRow(tesseramento));
            if (insertionPoint == null)
            {
                workingTable.Add(row);
            }
            else
            {
                insertionPoint.AddBeforeSelf(row);
            }
        }
    }

    private static bool IsHeaderRow(XElement row)
    {
        var firstTexts = row
            .Elements(TableNs + "table-cell")
            .Take(3)
            .Select(GetCellText)
            .ToList();

        var isStandardHeader = firstTexts.Count >= 3
            && string.Equals(firstTexts[0], "N.Tessera", StringComparison.OrdinalIgnoreCase)
            && string.Equals(firstTexts[1], "Cognome", StringComparison.OrdinalIgnoreCase)
            && string.Equals(firstTexts[2], "Nome", StringComparison.OrdinalIgnoreCase);

        var cells = row.Elements(TableNs + "table-cell").Take(4).Select(GetCellText).ToList();
        var isForeignHeader = cells.Count >= 4
            && string.Equals(cells[0], "Tesserato senza codice fiscale italiano", StringComparison.OrdinalIgnoreCase)
            && string.Equals(cells[1], "N.Tessera", StringComparison.OrdinalIgnoreCase)
            && string.Equals(cells[2], "Cognome", StringComparison.OrdinalIgnoreCase)
            && string.Equals(cells[3], "Nome", StringComparison.OrdinalIgnoreCase);

        return isStandardHeader || isForeignHeader;
    }

    private static bool RowHasUserData(XElement row)
    {
        return row
            .Elements(TableNs + "table-cell")
            .Take(26)
            .Any(cell => !string.IsNullOrWhiteSpace(GetCellText(cell)));
    }

    private static string GetCellText(XElement cell)
    {
        return string.Concat(cell
            .Descendants(TextNs + "p")
            .Select(p => p.Value))
            .Trim();
    }

    private static void PopulateRow(XElement row, IReadOnlyList<OdsCellValue> values)
    {
        var cells = EnsureWritableCells(row, values.Count);
        if (cells.Count < values.Count)
        {
            throw new InvalidOperationException("Il template ODS non ha abbastanza colonne per i dati richiesti.");
        }

        for (var i = 0; i < values.Count; i++)
        {
            WriteCell(cells[i], values[i]);
        }
    }

    private static List<XElement> EnsureWritableCells(XElement row, int requiredCells)
    {
        var originalCells = row.Elements(TableNs + "table-cell").ToList();
        var rebuiltCells = new List<XElement>();
        var writableCells = new List<XElement>(requiredCells);

        foreach (var originalCell in originalCells)
        {
            var repeat = Math.Max(1, (int?)originalCell.Attribute(TableNs + "number-columns-repeated") ?? 1);
            var consumed = 0;

            while (consumed < repeat && writableCells.Count < requiredCells)
            {
                var writableCell = new XElement(originalCell);
                writableCell.Attribute(TableNs + "number-columns-repeated")?.Remove();
                rebuiltCells.Add(writableCell);
                writableCells.Add(writableCell);
                consumed++;
            }

            var remaining = repeat - consumed;
            if (remaining > 0)
            {
                var remainingCell = new XElement(originalCell);
                if (remaining == 1)
                {
                    remainingCell.Attribute(TableNs + "number-columns-repeated")?.Remove();
                }
                else
                {
                    remainingCell.SetAttributeValue(TableNs + "number-columns-repeated", remaining);
                }

                rebuiltCells.Add(remainingCell);
            }
        }

        row.ReplaceNodes(rebuiltCells);
        return writableCells;
    }

    private static void WriteCell(XElement cell, OdsCellValue value)
    {
        cell.RemoveNodes();

        cell.SetAttributeValue(TableNs + "number-columns-repeated", null);
        cell.SetAttributeValue(OfficeNs + "value", null);
        cell.SetAttributeValue(OfficeNs + "boolean-value", null);
        cell.SetAttributeValue(OfficeNs + "value-type", value.ValueType);
        cell.SetAttributeValue(OfficeNs + "date-value", value.ValueType == "date" ? value.RawValue : null);
        cell.SetAttributeValue(OfficeNs + "string-value", null);
        cell.Add(new XElement(TextNs + "p", value.DisplayValue ?? string.Empty));
    }

    private static void SetAcsiCode(XDocument document, string acsiCode, int acsiCodeColumn)
    {
        var workingTable = document
            .Descendants(TableNs + "table")
            .FirstOrDefault(t => string.Equals((string?)t.Attribute(TableNs + "name"), WorkingSheetName, StringComparison.Ordinal));

        var codeRow = workingTable?.Elements(TableNs + "table-row").Skip(7).FirstOrDefault();
        if (codeRow == null)
        {
            return;
        }

        var cells = EnsureWritableCells(codeRow, acsiCodeColumn);
        WriteCell(cells[acsiCodeColumn - 1], Text(acsiCode));
    }

    private static XElement? FindCellAtColumn(XElement? row, int targetColumn)
    {
        if (row == null || targetColumn < 1)
        {
            return null;
        }

        var currentColumn = 1;

        foreach (var node in row.Elements())
        {
            var repeat = (int?)node.Attribute(TableNs + "number-columns-repeated") ?? 1;
            var span = (int?)node.Attribute(TableNs + "number-columns-spanned") ?? 1;
            var width = Math.Max(repeat, span);

            if (targetColumn >= currentColumn && targetColumn < currentColumn + width)
            {
                return node.Name == TableNs + "table-cell" ? node : null;
            }

            currentColumn += width;
        }

        return null;
    }

    private IReadOnlyList<OdsCellValue> BuildItalianRow(Tesseramento t)
    {
        return
        [
            Text(t.Tessera),
            Text(t.Cognome),
            Text(t.Nome),
            Text(t.CodiceFiscale),
            Text(QualificaDefault),
            Text(t.Email),
            Text(t.Cellulare),
            Text(AssicurazioneDefault),
            Text(DisciplinaConi1Default),
            Text(string.Empty),
            Text(string.Empty),
            Text(DisciplinaAcsi1Default),
            Text(string.Empty),
            Text(string.Empty),
            Text("NO"),
            Text("NO"),
            Text("NO")
        ];
    }

    private IReadOnlyList<OdsCellValue> BuildForeignRow(Tesseramento t)
    {
        return
        [
            Text("SI"),
            Text(t.Tessera),
            Text(t.Cognome),
            Text(t.Nome),
            Text(MapGender(t.Genere)),
            ForeignDate(t.DataNascita),
            Text(ToIso2OrFallback(t.NazioneNascita)),
            Text(string.IsNullOrWhiteSpace(t.CittaNascita) ? t.ComuneNascita : t.CittaNascita),
            Text(ToIso2OrFallback(t.NazioneCittadinanza)),
            Text(ToIso2OrFallback(t.NazioneResidenza)),
            Text(QualificaDefault),
            Text(t.Email),
            Text(t.Cellulare),
            Text(AssicurazioneDefault),
            Text(DisciplinaConi1Default),
            Text(string.Empty),
            Text(string.Empty),
            Text(DisciplinaAcsi1Default),
            Text(string.Empty),
            Text(string.Empty),
            Text(MapForeignDocumentType(t.TipoDocumentoEstero)),
            Text(t.NumeroDocumentoEstero),
            Text("NO"),
            Text("NO"),
            Text("NO")
        ];
    }

    private string ToIso2OrFallback(string? countryName)
    {
        if (string.IsNullOrWhiteSpace(countryName))
        {
            return string.Empty;
        }

        var trimmed = countryName.Trim();
        if (trimmed.Length == 2 && trimmed.All(char.IsLetter))
        {
            return trimmed.ToUpperInvariant();
        }

        var normalized = NormalizeKey(trimmed);
        if (_countryIso2Map.Value.TryGetValue(normalized, out var iso2))
        {
            return iso2;
        }

        var knownIso2 = _countryIso2Map.Value.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isoToken = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(token => token.Length == 2 && token.All(char.IsLetter) && knownIso2.Contains(token));

        return isoToken?.ToUpperInvariant() ?? string.Empty;
    }

    private static string MapForeignDocumentType(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
        {
            return string.Empty;
        }

        var normalized = NormalizeKey(documentType);
        return normalized switch
        {
            "ci" or "carta identita" or "carta d identita" or "identity card" => "CI",
            "ps" or "passaporto" or "passport" => "PS",
            "pg" or "permesso soggiorno" or "permesso di soggiorno" or "residence permit" => "PG",
            _ => documentType.Trim().ToUpperInvariant()
        };
    }

    private static string MapGender(string? genere)
    {
        if (string.IsNullOrWhiteSpace(genere))
        {
            return string.Empty;
        }

        return NormalizeKey(genere) switch
        {
            "maschio" or "m" or "male" => "M",
            "femmina" or "f" or "female" => "F",
            _ => string.Empty
        };
    }

    private static OdsCellValue Text(string? value)
    {
        var safeValue = SanitizeXmlText(value);
        return new(safeValue, safeValue, "string");
    }

    private static OdsCellValue Date(DateTime value) =>
        new(value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "date");

    private static OdsCellValue ForeignDate(DateTime value) =>
        Text(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static void AddArchiveEntry(ZipArchive archive, string fileName, byte[] content)
    {
        var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }

    private static Dictionary<string, string> BuildCountryIso2Map()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                AddCountryAlias(map, region.TwoLetterISORegionName, region.TwoLetterISORegionName);
                AddCountryAlias(map, region.ThreeLetterISORegionName, region.TwoLetterISORegionName);
                AddCountryAlias(map, region.EnglishName, region.TwoLetterISORegionName);
                AddCountryAlias(map, region.NativeName, region.TwoLetterISORegionName);
                AddCountryAlias(map, region.DisplayName, region.TwoLetterISORegionName);
            }
            catch
            {
                // Ignora culture/regioni non valide
            }
        }

        var manualAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["stati uniti"] = "US",
            ["stati uniti d america"] = "US",
            ["usa"] = "US",
            ["regno unito"] = "GB",
            ["gran bretagna"] = "GB",
            ["inghilterra"] = "GB",
            ["corea del sud"] = "KR",
            ["corea del nord"] = "KP",
            ["paesi bassi"] = "NL",
            ["olanda"] = "NL",
            ["repubblica ceca"] = "CZ",
            ["cechia"] = "CZ",
            ["slovacchia"] = "SK",
            ["russia"] = "RU",
            ["moldavia"] = "MD",
            ["vaticano"] = "VA",
            ["santa sede"] = "VA",
            ["costa d avorio"] = "CI",
            ["emirati arabi uniti"] = "AE",
            ["bosnia ed erzegovina"] = "BA",
            ["macedonia del nord"] = "MK",
            ["germania"] = "DE",
            ["deutsch"] = "DE",
            ["deutschland"] = "DE",
            ["francia"] = "FR",
            ["spagna"] = "ES",
            ["portogallo"] = "PT",
            ["svizzera"] = "CH",
            ["austria"] = "AT",
            ["belgio"] = "BE",
            ["polonia"] = "PL",
            ["polen"] = "PL",
            ["romania"] = "RO",
            ["bulgaria"] = "BG",
            ["croazia"] = "HR",
            ["slovenia"] = "SI",
            ["albania"] = "AL",
            ["marocco"] = "MA",
            ["tunisia"] = "TN",
            ["egitto"] = "EG",
            ["russland"] = "RU",
            ["kazakistan"] = "KZ",
            ["kazakhstan"] = "KZ",
            ["kasachstan"] = "KZ",
            ["brasile"] = "BR",
            ["argentina"] = "AR",
            ["cina"] = "CN",
            ["giappone"] = "JP"
        };

        foreach (var alias in manualAliases)
        {
            AddCountryAlias(map, alias.Key, alias.Value);
        }

        AddCommonMultilingualCountryAliases(map);

        return map;
    }

    private static void AddCommonMultilingualCountryAliases(IDictionary<string, string> map)
    {
        AddCountryAliases(map, "AF", "afghanistan", "afghan", "afghanisch", "afgano", "afghana", "afgana");
        AddCountryAliases(map, "AL", "albania", "albanien", "albanie", "albanian", "albanisch", "albanese");
        AddCountryAliases(map, "DZ", "algeria", "algerien", "algerie", "argelia", "algerien", "algerian", "algerisch", "algerino", "algerina");
        AddCountryAliases(map, "AR", "argentina", "argentinien", "argentine", "argentinisch", "argentino", "argentina");
        AddCountryAliases(map, "AM", "armenia", "armenien", "armenie", "armenian", "armenisch", "armeno", "armena");
        AddCountryAliases(map, "AU", "australia", "australien", "australie", "australian", "australisch", "australiano", "australiana");
        AddCountryAliases(map, "AT", "austria", "osterreich", "autriche", "austria", "austrian", "osterreichisch", "austriaco", "austriaca");
        AddCountryAliases(map, "BD", "bangladesh", "bangladesch", "bangladeshi", "bengalese");
        AddCountryAliases(map, "BE", "belgium", "belgien", "belgique", "belgica", "belgio", "belgian", "belgisch", "belga");
        AddCountryAliases(map, "BA", "bosnia", "bosnien", "bosnia herzegovina", "bosnia and herzegovina", "bosnien und herzegowina", "bosnie herzégovine", "bosniaco", "bosniaca");
        AddCountryAliases(map, "BR", "brazil", "brasilien", "bresil", "bresil", "brasile", "brasil", "brazilian", "brasilianisch", "brasiliano", "brasiliana");
        AddCountryAliases(map, "BG", "bulgaria", "bulgarien", "bulgarie", "bulgarian", "bulgarisch", "bulgaro", "bulgara");
        AddCountryAliases(map, "CM", "cameroon", "kamerun", "cameroun", "camerun", "camerunense");
        AddCountryAliases(map, "CA", "canada", "kanada", "canadian", "kanadisch", "canadese");
        AddCountryAliases(map, "CL", "chile", "chilean", "chilenisch", "cileno", "cilena");
        AddCountryAliases(map, "CN", "china", "chine", "cina", "chinese", "chinesisch", "cinese");
        AddCountryAliases(map, "CO", "colombia", "kolumbien", "colombie", "colombian", "kolumbianisch", "colombiano", "colombiana");
        AddCountryAliases(map, "HR", "croatia", "kroatien", "croatie", "croazia", "croatian", "kroatisch", "croato", "croata");
        AddCountryAliases(map, "CU", "cuba", "kubanisch", "cubano", "cubana");
        AddCountryAliases(map, "CZ", "czech republic", "czechia", "tschechien", "tschechische republik", "republique tcheque", "repubblica ceca", "cechia", "czech", "tschechisch", "ceco", "ceca");
        AddCountryAliases(map, "DK", "denmark", "danemark", "danemark", "danmark", "danimarca", "danish", "danisch", "danese");
        AddCountryAliases(map, "DO", "dominican republic", "dominikanische republik", "republique dominicaine", "repubblica dominicana", "dominicano", "dominicana");
        AddCountryAliases(map, "EC", "ecuador", "equateur", "ecuadorian", "ecuadoriano", "ecuadoriana");
        AddCountryAliases(map, "EG", "egypt", "agypten", "egypte", "egitto", "egipto", "egyptian", "agyptisch", "egiziano", "egiziana");
        AddCountryAliases(map, "SV", "el salvador", "salvador", "salvadoregno", "salvadoregna");
        AddCountryAliases(map, "EE", "estonia", "estland", "estonie", "estonian", "estnisch", "estone");
        AddCountryAliases(map, "ET", "ethiopia", "athiopien", "ethiopie", "etiopia", "ethiopian", "athiopisch", "etiope");
        AddCountryAliases(map, "FI", "finland", "finnland", "finlande", "finlandia", "finnish", "finnisch", "finlandese");
        AddCountryAliases(map, "FR", "france", "frankreich", "francia", "frankrijk", "francja", "frances", "francais", "franzosisch", "french", "francese");
        AddCountryAliases(map, "GE", "georgia", "georgien", "georgie", "georgian", "georgisch", "georgiano", "georgiana");
        AddCountryAliases(map, "DE", "germany", "germania", "deutschland", "allemagne", "alemania", "duitsland", "niemcy", "deutsch", "deutsche", "german", "germanisch", "tedesco", "tedesca", "alemao", "alemana");
        AddCountryAliases(map, "GH", "ghana", "ghanaian", "ghanese");
        AddCountryAliases(map, "GR", "greece", "griechenland", "grece", "grecia", "greek", "griechisch", "greco", "greca");
        AddCountryAliases(map, "HU", "hungary", "ungarn", "hongrie", "ungheria", "hungria", "hungarian", "ungarisch", "ungherese");
        AddCountryAliases(map, "IN", "india", "indien", "inde", "indian", "indisch", "indiano", "indiana");
        AddCountryAliases(map, "ID", "indonesia", "indonesien", "indonesie", "indonesian", "indonesisch", "indonesiano", "indonesiana");
        AddCountryAliases(map, "IR", "iran", "iranian", "iranisch", "iraniano", "iraniana");
        AddCountryAliases(map, "IQ", "iraq", "irak", "iraqi", "irakisch", "iracheno", "irachena");
        AddCountryAliases(map, "IE", "ireland", "irland", "irlande", "irlanda", "irish", "irisch", "irlandese");
        AddCountryAliases(map, "IL", "israel", "israele", "israeli", "israelisch", "israeliano", "israeliana");
        AddCountryAliases(map, "IT", "italy", "italien", "italie", "italia", "italian", "italienisch", "italiano", "italiana");
        AddCountryAliases(map, "CI", "ivory coast", "cote d ivoire", "costa d avorio", "elfenbeinkuste", "ivoirien", "ivoriano", "ivoriana");
        AddCountryAliases(map, "JP", "japan", "japon", "giappone", "japanese", "japanisch", "giapponese");
        AddCountryAliases(map, "KZ", "kazakhstan", "kasachstan", "kazakistan", "kazachstan", "kazakh", "kasachisch", "kazako", "kazaka");
        AddCountryAliases(map, "KE", "kenya", "kenian", "kenianisch", "keniota");
        AddCountryAliases(map, "XK", "kosovo", "kosovar", "kosovaro", "kosovara");
        AddCountryAliases(map, "LV", "latvia", "lettland", "lettonie", "lettonia", "latvian", "lettisch", "lettone");
        AddCountryAliases(map, "LB", "lebanon", "libanon", "liban", "libano", "lebanese", "libanesisch", "libanese");
        AddCountryAliases(map, "LT", "lithuania", "litauen", "lituanie", "lituania", "lithuanian", "litauisch", "lituano", "lituana");
        AddCountryAliases(map, "LU", "luxembourg", "luxemburg", "lussemburgo", "luxembourger", "lussemburghese");
        AddCountryAliases(map, "MK", "north macedonia", "mazedonien", "nordmazedonien", "macedoine du nord", "macedonia del nord", "macedonian", "mazedonisch", "macedone");
        AddCountryAliases(map, "ML", "mali", "malian", "maliano", "maliana");
        AddCountryAliases(map, "MA", "morocco", "marokko", "maroc", "marocco", "marruecos", "moroccan", "marokkanisch", "marocchino", "marocchina");
        AddCountryAliases(map, "MD", "moldova", "moldawien", "moldavie", "moldavia", "moldovan", "moldauisch", "moldavo", "moldava");
        AddCountryAliases(map, "ME", "montenegro", "montenegrin", "montenegrino", "montenegrina");
        AddCountryAliases(map, "NL", "netherlands", "niederlande", "pays bas", "paesi bassi", "olanda", "nederland", "dutch", "niederlandisch", "olandese");
        AddCountryAliases(map, "NG", "nigeria", "nigerian", "nigeriano", "nigeriana");
        AddCountryAliases(map, "NO", "norway", "norwegen", "norvege", "norvegia", "norwegian", "norwegisch", "norvegese");
        AddCountryAliases(map, "PK", "pakistan", "pakistani", "pakistanisch", "pakistano", "pakistana");
        AddCountryAliases(map, "PE", "peru", "perou", "peruvian", "peruanisch", "peruviano", "peruviana");
        AddCountryAliases(map, "PH", "philippines", "philippinen", "filippine", "philippine", "filipino", "filipina");
        AddCountryAliases(map, "PL", "poland", "polen", "pologne", "polonia", "polska", "polish", "polnisch", "polacco", "polacca");
        AddCountryAliases(map, "PT", "portugal", "portogallo", "portuguese", "portugiesisch", "portoghese");
        AddCountryAliases(map, "RO", "romania", "rumania", "rumanien", "roumanie", "romanian", "rumanisch", "romeno", "romena");
        AddCountryAliases(map, "RU", "russia", "russland", "russie", "rusia", "rossiya", "russian", "russisch", "russo", "russa");
        AddCountryAliases(map, "SN", "senegal", "senegalese", "senegalesisch");
        AddCountryAliases(map, "RS", "serbia", "serbien", "serbie", "serbian", "serbisch", "serbo", "serba");
        AddCountryAliases(map, "SK", "slovakia", "slowakei", "slovaquie", "slovacchia", "slovak", "slowakisch", "slovacco", "slovacca");
        AddCountryAliases(map, "SI", "slovenia", "slowenien", "slovenie", "slovenian", "slowenisch", "sloveno", "slovena");
        AddCountryAliases(map, "SO", "somalia", "somali", "somalo", "somala");
        AddCountryAliases(map, "ES", "spain", "spanien", "espagne", "espana", "spagna", "spanish", "spanisch", "spagnolo", "spagnola");
        AddCountryAliases(map, "LK", "sri lanka", "srilanka", "sri lankan", "singalese");
        AddCountryAliases(map, "SE", "sweden", "schweden", "suede", "svezia", "swedish", "schwedisch", "svedese");
        AddCountryAliases(map, "CH", "switzerland", "schweiz", "suisse", "suiza", "svizzera", "svizzero", "svizzera", "swiss", "schweizerisch");
        AddCountryAliases(map, "SY", "syria", "syrien", "syrie", "siria", "syrian", "syrisch", "siriano", "siriana");
        AddCountryAliases(map, "TN", "tunisia", "tunesien", "tunisie", "tunisian", "tunesisch", "tunisino", "tunisina");
        AddCountryAliases(map, "TR", "turkey", "turkiye", "turkei", "turquie", "turchia", "turkish", "turkisch", "turco", "turca");
        AddCountryAliases(map, "UA", "ukraine", "ucraina", "ukrainian", "ukrainisch", "ucraino", "ucraina");
        AddCountryAliases(map, "GB", "united kingdom", "great britain", "uk", "england", "regno unito", "gran bretagna", "royaume uni", "vereinigtes konigreich", "british", "britannico", "britannica", "inglese");
        AddCountryAliases(map, "US", "united states", "united states of america", "usa", "u s a", "stati uniti", "stati uniti d america", "etats unis", "vereinigte staaten", "american", "americano", "americana");
        AddCountryAliases(map, "UY", "uruguay", "uruguayan", "uruguayano", "uruguayana");
        AddCountryAliases(map, "VE", "venezuela", "venezuelan", "venezuelano", "venezuelana");
        AddCountryAliases(map, "VN", "vietnam", "viet nam", "vietnamese", "vietnamesisch");
    }

    private static void AddCountryAliases(IDictionary<string, string> map, string iso2, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            AddCountryAlias(map, alias, iso2);
        }
    }

    private static void AddCountryAlias(IDictionary<string, string> map, string? alias, string iso2)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        map[NormalizeKey(alias)] = iso2.ToUpperInvariant();
    }

    private static string NormalizeKey(string input)
    {
        var normalized = input
            .Trim()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsWhiteSpace(ch) || ch is '\'' or '-' or '/')
            {
                builder.Append(' ');
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string SanitizeXmlText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (XmlConvert.IsXmlChar(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private readonly record struct OdsCellValue(string DisplayValue, string RawValue, string ValueType);
}
