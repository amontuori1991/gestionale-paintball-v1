using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Services
{
    public class StaffRegistryService
    {
        private const string SettingKey = "StaffMembers";

        private static readonly string[] DefaultStaff =
        {
            "Simone",
            "Davide",
            "Andrea",
            "Federico",
            "Enrico"
        };

        private readonly TesseramentoDbContext _context;

        public StaffRegistryService(TesseramentoDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetStaffAsync()
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(x => x.Key == SettingKey);
            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                await SaveStaffAsync(DefaultStaff);
                return DefaultStaff.OrderBy(x => x).ToList();
            }

            try
            {
                var names = JsonSerializer.Deserialize<List<string>>(setting.Value) ?? new List<string>();
                var normalized = NormalizeNames(names);

                if (normalized.Count == 0)
                {
                    await SaveStaffAsync(DefaultStaff);
                    return DefaultStaff.OrderBy(x => x).ToList();
                }

                return normalized;
            }
            catch (JsonException)
            {
                await SaveStaffAsync(DefaultStaff);
                return DefaultStaff.OrderBy(x => x).ToList();
            }
        }

        public async Task<bool> AddStaffAsync(string name)
        {
            name = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var staff = await GetStaffAsync();
            if (staff.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                return false;

            staff.Add(name);
            await SaveStaffAsync(staff);
            return true;
        }

        public async Task<bool> RemoveStaffAsync(string name)
        {
            name = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var staff = await GetStaffAsync();
            var updated = staff
                .Where(x => !string.Equals(x, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (updated.Count == staff.Count)
                return false;

            await SaveStaffAsync(updated);
            return true;
        }

        private async Task SaveStaffAsync(IEnumerable<string> names)
        {
            var normalized = NormalizeNames(names);
            var json = JsonSerializer.Serialize(normalized);
            var setting = await _context.AppSettings.FirstOrDefaultAsync(x => x.Key == SettingKey);

            if (setting == null)
            {
                _context.AppSettings.Add(new AppSetting
                {
                    Key = SettingKey,
                    Value = json
                });
            }
            else
            {
                setting.Value = json;
            }

            await _context.SaveChangesAsync();
        }

        private static List<string> NormalizeNames(IEnumerable<string> names)
        {
            return names
                .Select(NormalizeName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        private static string NormalizeName(string? name)
        {
            return string.Join(" ", (name ?? string.Empty)
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
