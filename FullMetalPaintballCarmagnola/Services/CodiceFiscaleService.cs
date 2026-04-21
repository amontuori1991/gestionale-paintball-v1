using System.Globalization;
using System.Text;

namespace Full_Metal_Paintball_Carmagnola.Services
{
    public static class CodiceFiscaleService
    {
        private static readonly Dictionary<int, char> MonthCodes = new()
        {
            [1] = 'A',
            [2] = 'B',
            [3] = 'C',
            [4] = 'D',
            [5] = 'E',
            [6] = 'H',
            [7] = 'L',
            [8] = 'M',
            [9] = 'P',
            [10] = 'R',
            [11] = 'S',
            [12] = 'T'
        };

        private static readonly Dictionary<char, int> OddValues = new()
        {
            ['0'] = 1, ['1'] = 0, ['2'] = 5, ['3'] = 7, ['4'] = 9,
            ['5'] = 13, ['6'] = 15, ['7'] = 17, ['8'] = 19, ['9'] = 21,
            ['A'] = 1, ['B'] = 0, ['C'] = 5, ['D'] = 7, ['E'] = 9,
            ['F'] = 13, ['G'] = 15, ['H'] = 17, ['I'] = 19, ['J'] = 21,
            ['K'] = 2, ['L'] = 4, ['M'] = 18, ['N'] = 20, ['O'] = 11,
            ['P'] = 3, ['Q'] = 6, ['R'] = 8, ['S'] = 12, ['T'] = 14,
            ['U'] = 16, ['V'] = 10, ['W'] = 22, ['X'] = 25, ['Y'] = 24,
            ['Z'] = 23
        };

        public static string Calcola(string cognome, string nome, DateTime dataNascita, string sesso, string codiceCatastale)
        {
            var partial = new StringBuilder();
            partial.Append(CodificaCognome(cognome));
            partial.Append(CodificaNome(nome));
            partial.Append(dataNascita.Year.ToString("00", CultureInfo.InvariantCulture)[^2..]);
            partial.Append(MonthCodes[dataNascita.Month]);

            var giorno = dataNascita.Day + (IsFemmina(sesso) ? 40 : 0);
            partial.Append(giorno.ToString("00", CultureInfo.InvariantCulture));
            partial.Append(NormalizeCode(codiceCatastale));
            partial.Append(CalcolaCarattereControllo(partial.ToString()));

            return partial.ToString();
        }

        public static bool IsValidShape(string codiceFiscale)
        {
            if (string.IsNullOrWhiteSpace(codiceFiscale))
                return false;

            var cf = codiceFiscale.Trim().ToUpperInvariant();
            return cf.Length == 16 && cf.All(char.IsLetterOrDigit);
        }

        private static string CodificaCognome(string value)
        {
            var normalized = NormalizeName(value);
            return BuildCode(GetConsonants(normalized) + GetVowels(normalized));
        }

        private static string CodificaNome(string value)
        {
            var normalized = NormalizeName(value);
            var consonants = GetConsonants(normalized);

            if (consonants.Length >= 4)
                return $"{consonants[0]}{consonants[2]}{consonants[3]}";

            return BuildCode(consonants + GetVowels(normalized));
        }

        private static string BuildCode(string value)
        {
            return (value + "XXX")[..3];
        }

        private static string GetConsonants(string value)
        {
            return new string(value.Where(c => char.IsLetter(c) && !"AEIOU".Contains(c)).ToArray());
        }

        private static string GetVowels(string value)
        {
            return new string(value.Where(c => "AEIOU".Contains(c)).ToArray());
        }

        private static string NormalizeName(string value)
        {
            var formD = (value ?? string.Empty).ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var c in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetter(c))
                    builder.Append(c);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string NormalizeCode(string codiceCatastale)
        {
            return (codiceCatastale ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static bool IsFemmina(string sesso)
        {
            var value = (sesso ?? string.Empty).Trim().ToUpperInvariant();
            return value is "F" or "FEMMINA" or "FEMALE";
        }

        private static char CalcolaCarattereControllo(string partial)
        {
            var sum = 0;

            for (var i = 0; i < partial.Length; i++)
            {
                var c = partial[i];
                sum += i % 2 == 0
                    ? OddValues[c]
                    : char.IsDigit(c) ? c - '0' : c - 'A';
            }

            return (char)('A' + (sum % 26));
        }
    }
}
