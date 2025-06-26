using System;
using System.Linq;
using System.Threading.Tasks;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services; // Per IEmailService
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Per UserManager (se invii email agli utenti registrati)
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // Per AdminNotifications

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    // Applicheremo una policy di autorizzazione per questa funzionalità
    [Authorize(Policy = "ToDoList")]
    public class ToDoListController : Controller
    {
        private readonly TesseramentoDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public ToDoListController(TesseramentoDbContext dbContext, IEmailService emailService, UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _emailService = emailService;
            _userManager = userManager;
            _configuration = configuration;
        }

        // Metodo privato per inviare notifiche a tutti gli utenti (copiato da PartiteController)
        private async Task SendNotificationToAllUsers(string subject, string messageHtml)
        {
            var users = await _userManager.Users.ToListAsync();
            var adminEmails = _configuration.GetSection("AdminNotifications").Get<string[]>();

            foreach (var user in users)
            {
                if (!string.IsNullOrEmpty(user.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, messageHtml);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Errore nell'invio email a {user.Email}: {ex.Message}");
                    }
                }
            }

            if (adminEmails != null && adminEmails.Any())
            {
                foreach (var adminEmail in adminEmails)
                {
                    if (!string.IsNullOrEmpty(adminEmail))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(adminEmail, subject, messageHtml);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore nell'invio email all'admin {adminEmail}: {ex.Message}");
                        }
                    }
                }
            }
        }


        // GET: ToDoList/Index
        public async Task<IActionResult> Index()
        {
            var topics = await _dbContext.Topics.OrderBy(t => t.Name).ToListAsync();
            var toDoItems = await _dbContext.ToDoItems
                                    .Include(ti => ti.Topic)
                                    .OrderBy(ti => ti.CreatedDate)
                                    .ToListAsync();

            var model = new ToDoListIndexViewModel
            {
                Topics = topics,
                ToDoItemsByTopic = new Dictionary<string, List<ToDoItemViewModel>>(),
                CompletedItems = toDoItems.Where(ti => ti.IsCompleted).Select(ti => new ToDoItemViewModel
                {
                    Id = ti.Id,
                    Description = ti.Description,
                    IsCompleted = ti.IsCompleted,
                    CreatedDate = ti.CreatedDate,
                    TopicId = ti.TopicId,
                    TopicName = ti.Topic?.Name ?? "N/A",
                    Notes = ti.Notes
                }).ToList()
            };

            // Popola le attività per topic (non completate)
            foreach (var topic in topics)
            {
                model.ToDoItemsByTopic[topic.Name] = toDoItems.Where(ti => ti.TopicId == topic.Id && !ti.IsCompleted).Select(ti => new ToDoItemViewModel
                {
                    Id = ti.Id,
                    Description = ti.Description,
                    IsCompleted = ti.IsCompleted,
                    CreatedDate = ti.CreatedDate,
                    TopicId = ti.TopicId,
                    TopicName = ti.Topic?.Name ?? "N/A",
                    Notes = ti.Notes
                }).ToList();
            }

            return View(model);
        }

        // POST: ToDoList/CreateItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateItem(ToDoListIndexViewModel model)
        {
            if (ModelState.IsValid)
            {
                var newToDoItem = new ToDoItem
                {
                    Description = model.NewItem.Description,
                    TopicId = model.NewItem.TopicId,
                    CreatedDate = DateTime.UtcNow,
                    IsCompleted = false,
                    Notes = model.NewItem.Notes
                };

                _dbContext.ToDoItems.Add(newToDoItem);
                await _dbContext.SaveChangesAsync();

                // Invia email di notifica
                var topic = await _dbContext.Topics.FindAsync(newToDoItem.TopicId);
                var subject = $"Nuova Attività ToDo: [{topic?.Name}] {newToDoItem.Description}";
                var messageHtml = $@"
                    <html><body>
                        <p>Ciao,</p>
                        <p>È stata aggiunta una nuova attività alla ToDo List:</p>
                        <p><strong>Topic:</strong> {topic?.Name}</p>
                        <p><strong>Attività:</strong> {newToDoItem.Description}</p>
                        <p><strong>Note:</strong> {newToDoItem.Notes ?? "Nessuna"}</p>
                        <p>Data di creazione: {newToDoItem.CreatedDate:dd/MM/yyyy HH:mm}</p>
                        <p>Verifica la ToDo List per maggiori dettagli.</p>
                    </body></html>";
                await SendNotificationToAllUsers(subject, messageHtml);


                TempData["SuccessMessage"] = "Attività aggiunta con successo!";
                return RedirectToAction(nameof(Index));
            }

            // Se il ModelState non è valido, ricarica la pagina Index con i dati esistenti
            // e il modello di creazione per mostrare gli errori di validazione.
            return await Index(); // Ricarica Index per mostrare errori
        }

        // POST: ToDoList/MarkAsCompleted/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsCompleted(int id)
        {
            var toDoItem = await _dbContext.ToDoItems.FindAsync(id);
            if (toDoItem == null)
            {
                return NotFound();
            }

            toDoItem.IsCompleted = true;
            await _dbContext.SaveChangesAsync();

            // Invia email di notifica (opzionale: notifica completamento)
            var topic = await _dbContext.Topics.FindAsync(toDoItem.TopicId);
            var subject = $"Attività ToDo Completata: [{topic?.Name}] {toDoItem.Description}";
            var messageHtml = $@"
                <html><body>
                    <p>Ciao,</p>
                    <p>Un'attività è stata contrassegnata come completata nella ToDo List:</p>
                    <p><strong>Topic:</strong> {topic?.Name}</p>
                    <p><strong>Attività:</strong> {toDoItem.Description}</p>
                    <p>Data completamento: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                    <p>Verifica la sezione 'Completate' della ToDo List.</p>
                </body></html>";
            await SendNotificationToAllUsers(subject, messageHtml);

            TempData["SuccessMessage"] = "Attività contrassegnata come completata!";
            return RedirectToAction(nameof(Index));
        }

        // POST: ToDoList/AddNote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string note)
        {
            var toDoItem = await _dbContext.ToDoItems.FindAsync(id);
            if (toDoItem == null)
            {
                return NotFound();
            }

            toDoItem.Notes = note;
            await _dbContext.SaveChangesAsync();

            // Invia email di notifica (per aggiunta/modifica nota)
            var topic = await _dbContext.Topics.FindAsync(toDoItem.TopicId);
            var subject = $"Nota Aggiunta a ToDo: [{topic?.Name}] {toDoItem.Description}";
            var messageHtml = $@"
                <html><body>
                    <p>Ciao,</p>
                    <p>È stata aggiunta/modificata una nota a un'attività nella ToDo List:</p>
                    <p><strong>Topic:</strong> {topic?.Name}</p>
                    <p><strong>Attività:</strong> {toDoItem.Description}</p>
                    <p><strong>Nuova Nota:</strong> {toDoItem.Notes}</p>
                    <p>Verifica la ToDo List per maggiori dettagli.</p>
                </body></html>";
            await SendNotificationToAllUsers(subject, messageHtml);

            TempData["SuccessMessage"] = "Nota aggiunta con successo!";
            return RedirectToAction(nameof(Index));
        }

        // POST: ToDoList/DeleteItem/5 (soft delete o hard delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var toDoItem = await _dbContext.ToDoItems.FindAsync(id);
            if (toDoItem == null)
            {
                return NotFound();
            }

            _dbContext.ToDoItems.Remove(toDoItem); // Elimina fisicamente
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Attività eliminata con successo!";
            return RedirectToAction(nameof(Index));
        }
    }
}