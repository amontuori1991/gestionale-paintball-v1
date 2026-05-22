using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Lavaggi")]
    public class LavaggiController : Controller
    {
        private const string LavaggiSettingKey = "LavaggiTrackerState";
        private const string LavaggiHistorySettingKey = "LavaggiTrackerHistory";
        private const string CaschiCategoryKey = "caschi";
        private const string PettorineCategoryKey = "pettorine";
        private const string CaschiPersonName = "Montuo";
        private const string PettorinePersonName = "Flavio";
        private const string UndoResolutionMerge = "merge";
        private const string UndoResolutionReplace = "replace";
        private readonly TesseramentoDbContext _dbContext;

        public LavaggiController(TesseramentoDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var state = await LoadStateAsync();
            var history = await LoadHistoryAsync();
            return View(new LavaggiDashboardViewModel
            {
                State = state,
                History = history
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] LavaggiTrackerState state)
        {
            var existingState = await LoadStateAsync();
            NormalizeState(state, existingState);
            await SaveStateAsync(state);

            return Json(new
            {
                success = true,
                lastUpdated = state.LastUpdatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                caschiTotal = state.CaschiCount * state.CaschiUnitPrice,
                pettorineTotal = state.PettorineCount * state.PettorineUnitPrice,
                grandTotal = (state.CaschiCount * state.CaschiUnitPrice) + (state.PettorineCount * state.PettorineUnitPrice),
                caschiPeriodStart = state.CaschiPeriodStartUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                pettorinePeriodStart = state.PettorinePeriodStartUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetCategory([FromForm] string category)
        {
            var normalizedCategory = NormalizeCategory(category);
            if (normalizedCategory == null)
            {
                return BadRequest();
            }

            var state = await LoadStateAsync();
            var history = await LoadHistoryAsync();
            var nowUtc = DateTime.UtcNow;
            var historyEntry = BuildHistoryEntry(state, normalizedCategory, nowUtc);

            if (historyEntry != null)
            {
                history.Insert(0, historyEntry);
                await SaveHistoryAsync(history);
            }

            if (normalizedCategory == CaschiCategoryKey)
            {
                state.CaschiCount = 0;
                state.CaschiPeriodStartUtc = nowUtc;
            }
            else
            {
                state.PettorineCount = 0;
                state.PettorinePeriodStartUtc = nowUtc;
            }

            state.LastUpdatedUtc = nowUtc;
            await SaveStateAsync(state);

            return Json(new
            {
                success = true,
                resetCategory = normalizedCategory,
                lastUpdated = state.LastUpdatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                caschiTotal = state.CaschiCount * state.CaschiUnitPrice,
                pettorineTotal = state.PettorineCount * state.PettorineUnitPrice,
                grandTotal = (state.CaschiCount * state.CaschiUnitPrice) + (state.PettorineCount * state.PettorineUnitPrice),
                history = history.Select(MapHistoryEntry).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoHistoryEntry([FromForm] Guid entryId, [FromForm] string? resolution)
        {
            if (entryId == Guid.Empty)
            {
                return BadRequest();
            }

            var state = await LoadStateAsync();
            var history = await LoadHistoryAsync();
            var entry = history.FirstOrDefault(x => x.EntryId == entryId);
            if (entry == null)
            {
                return NotFound();
            }

            var hasConflict = HasActivePartial(state, entry.CategoryKey);
            var normalizedResolution = NormalizeUndoResolution(resolution);

            if (hasConflict && normalizedResolution == null)
            {
                return Json(new
                {
                    success = false,
                    requiresDecision = true,
                    entryId = entry.EntryId,
                    categoryKey = entry.CategoryKey,
                    categoryName = entry.CategoryName,
                    personName = entry.PersonName,
                    pieceCount = entry.PieceCount,
                    unitPrice = entry.UnitPrice,
                    totalAmount = entry.TotalAmount,
                    currentCount = entry.CategoryKey == CaschiCategoryKey ? state.CaschiCount : state.PettorineCount,
                    currentUnitPrice = entry.CategoryKey == CaschiCategoryKey ? state.CaschiUnitPrice : state.PettorineUnitPrice
                });
            }

            history.Remove(entry);
            RestoreEntryToState(state, entry, normalizedResolution ?? UndoResolutionMerge);
            state.LastUpdatedUtc = DateTime.UtcNow;

            await SaveHistoryAsync(history);
            await SaveStateAsync(state);

            return Json(new
            {
                success = true,
                lastUpdated = state.LastUpdatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                caschiCount = state.CaschiCount,
                pettorineCount = state.PettorineCount,
                caschiUnitPrice = state.CaschiUnitPrice,
                pettorineUnitPrice = state.PettorineUnitPrice,
                caschiTotal = state.CaschiCount * state.CaschiUnitPrice,
                pettorineTotal = state.PettorineCount * state.PettorineUnitPrice,
                grandTotal = (state.CaschiCount * state.CaschiUnitPrice) + (state.PettorineCount * state.PettorineUnitPrice),
                caschiPeriodStart = state.CaschiPeriodStartUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                pettorinePeriodStart = state.PettorinePeriodStartUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                history = history.Select(MapHistoryEntry).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaidStatus([FromForm] Guid entryId, [FromForm] bool isPaid)
        {
            if (entryId == Guid.Empty)
            {
                return BadRequest();
            }

            var history = await LoadHistoryAsync();
            var entry = history.FirstOrDefault(x => x.EntryId == entryId);
            if (entry == null)
            {
                return NotFound();
            }

            entry.IsPaid = isPaid;
            await SaveHistoryAsync(history);

            return Json(new
            {
                success = true,
                entryId = entry.EntryId,
                isPaid = entry.IsPaid,
                history = history.Select(MapHistoryEntry).ToList()
            });
        }

        private async Task<LavaggiTrackerState> LoadStateAsync()
        {
            var rawValue = await _dbContext.AppSettings
                .Where(s => s.Key == LavaggiSettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new LavaggiTrackerState();
            }

            try
            {
                var state = JsonSerializer.Deserialize<LavaggiTrackerState>(rawValue) ?? new LavaggiTrackerState();
                EnsurePeriodStartDates(state);
                return state;
            }
            catch
            {
                return new LavaggiTrackerState();
            }
        }

        private async Task<List<LavaggioHistoryEntry>> LoadHistoryAsync()
        {
            var rawValue = await _dbContext.AppSettings
                .Where(s => s.Key == LavaggiHistorySettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new List<LavaggioHistoryEntry>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<LavaggioHistoryEntry>>(rawValue) ?? new List<LavaggioHistoryEntry>();
            }
            catch
            {
                return new List<LavaggioHistoryEntry>();
            }
        }

        private async Task SaveStateAsync(LavaggiTrackerState state)
        {
            var setting = await _dbContext.AppSettings
                .FirstOrDefaultAsync(s => s.Key == LavaggiSettingKey);

            if (setting == null)
            {
                setting = new AppSetting { Key = LavaggiSettingKey };
                _dbContext.AppSettings.Add(setting);
            }

            setting.Value = JsonSerializer.Serialize(state);
            await _dbContext.SaveChangesAsync();
        }

        private async Task SaveHistoryAsync(List<LavaggioHistoryEntry> history)
        {
            var setting = await _dbContext.AppSettings
                .FirstOrDefaultAsync(s => s.Key == LavaggiHistorySettingKey);

            if (setting == null)
            {
                setting = new AppSetting { Key = LavaggiHistorySettingKey };
                _dbContext.AppSettings.Add(setting);
            }

            setting.Value = JsonSerializer.Serialize(history);
            await _dbContext.SaveChangesAsync();
        }

        private static void NormalizeState(LavaggiTrackerState state, LavaggiTrackerState existingState)
        {
            state.CaschiCount = Math.Max(0, state.CaschiCount);
            state.PettorineCount = Math.Max(0, state.PettorineCount);
            state.CaschiUnitPrice = Math.Max(0m, state.CaschiUnitPrice);
            state.PettorineUnitPrice = Math.Max(0m, state.PettorineUnitPrice);
            state.CaschiPeriodStartUtc = existingState.CaschiPeriodStartUtc;
            state.PettorinePeriodStartUtc = existingState.PettorinePeriodStartUtc;
            state.LastUpdatedUtc = DateTime.UtcNow;
            EnsurePeriodStartDates(state);
        }

        private static void EnsurePeriodStartDates(LavaggiTrackerState state)
        {
            if (state.CaschiPeriodStartUtc == default)
            {
                state.CaschiPeriodStartUtc = state.LastUpdatedUtc == default ? DateTime.UtcNow : state.LastUpdatedUtc;
            }

            if (state.PettorinePeriodStartUtc == default)
            {
                state.PettorinePeriodStartUtc = state.LastUpdatedUtc == default ? DateTime.UtcNow : state.LastUpdatedUtc;
            }

            if (state.LastUpdatedUtc == default)
            {
                state.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        private static string? NormalizeCategory(string? category)
        {
            return category?.Trim().ToLowerInvariant() switch
            {
                CaschiCategoryKey => CaschiCategoryKey,
                PettorineCategoryKey => PettorineCategoryKey,
                _ => null
            };
        }

        private static LavaggioHistoryEntry? BuildHistoryEntry(LavaggiTrackerState state, string category, DateTime nowUtc)
        {
            if (category == CaschiCategoryKey)
            {
                if (state.CaschiCount <= 0 && state.CaschiUnitPrice <= 0)
                {
                    return null;
                }

                return new LavaggioHistoryEntry
                {
                    EntryId = Guid.NewGuid(),
                    CategoryKey = CaschiCategoryKey,
                    CategoryName = "Caschi",
                    PersonName = CaschiPersonName,
                    PieceCount = state.CaschiCount,
                    UnitPrice = state.CaschiUnitPrice,
                    TotalAmount = state.CaschiCount * state.CaschiUnitPrice,
                    PeriodStartUtc = state.CaschiPeriodStartUtc,
                    PeriodEndUtc = nowUtc,
                    ResetAtUtc = nowUtc
                };
            }

            if (state.PettorineCount <= 0 && state.PettorineUnitPrice <= 0)
            {
                return null;
            }

            return new LavaggioHistoryEntry
            {
                EntryId = Guid.NewGuid(),
                CategoryKey = PettorineCategoryKey,
                CategoryName = "Pettorine",
                PersonName = PettorinePersonName,
                PieceCount = state.PettorineCount,
                UnitPrice = state.PettorineUnitPrice,
                TotalAmount = state.PettorineCount * state.PettorineUnitPrice,
                PeriodStartUtc = state.PettorinePeriodStartUtc,
                PeriodEndUtc = nowUtc,
                ResetAtUtc = nowUtc
            };
        }

        private static object MapHistoryEntry(LavaggioHistoryEntry entry)
        {
            return new
            {
                entryId = entry.EntryId,
                categoryKey = entry.CategoryKey,
                categoryName = entry.CategoryName,
                personName = entry.PersonName,
                pieceCount = entry.PieceCount,
                unitPrice = entry.UnitPrice,
                totalAmount = entry.TotalAmount,
                periodStart = entry.PeriodStartUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                periodEnd = entry.PeriodEndUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                resetAt = entry.ResetAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                isPaid = entry.IsPaid
            };
        }

        private static void RestoreEntryToState(LavaggiTrackerState state, LavaggioHistoryEntry entry, string resolution)
        {
            if (entry.CategoryKey == CaschiCategoryKey)
            {
                var shouldReplace = resolution == UndoResolutionReplace;
                var hadExistingCount = state.CaschiCount > 0 && !shouldReplace;
                state.CaschiCount = shouldReplace ? entry.PieceCount : state.CaschiCount + entry.PieceCount;
                state.CaschiUnitPrice = entry.UnitPrice;
                state.CaschiPeriodStartUtc = hadExistingCount
                    ? MinDate(state.CaschiPeriodStartUtc, entry.PeriodStartUtc)
                    : entry.PeriodStartUtc;
                return;
            }

            var shouldReplacePettorine = resolution == UndoResolutionReplace;
            var hadExistingPettorine = state.PettorineCount > 0 && !shouldReplacePettorine;
            state.PettorineCount = shouldReplacePettorine ? entry.PieceCount : state.PettorineCount + entry.PieceCount;
            state.PettorineUnitPrice = entry.UnitPrice;
            state.PettorinePeriodStartUtc = hadExistingPettorine
                ? MinDate(state.PettorinePeriodStartUtc, entry.PeriodStartUtc)
                : entry.PeriodStartUtc;
        }

        private static bool HasActivePartial(LavaggiTrackerState state, string categoryKey)
        {
            return categoryKey == CaschiCategoryKey
                ? state.CaschiCount > 0
                : state.PettorineCount > 0;
        }

        private static string? NormalizeUndoResolution(string? resolution)
        {
            return resolution?.Trim().ToLowerInvariant() switch
            {
                UndoResolutionMerge => UndoResolutionMerge,
                UndoResolutionReplace => UndoResolutionReplace,
                _ => null
            };
        }

        private static DateTime MinDate(DateTime first, DateTime second)
        {
            if (first == default)
            {
                return second;
            }

            if (second == default)
            {
                return first;
            }

            return first <= second ? first : second;
        }
    }
}
