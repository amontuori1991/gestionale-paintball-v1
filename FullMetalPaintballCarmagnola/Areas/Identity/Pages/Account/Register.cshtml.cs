using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Full_Metal_Paintball_Carmagnola.Models; // 👈 Importa il tuo ApplicationUser

namespace Full_Metal_Paintball_Carmagnola.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = (IUserEmailStore<ApplicationUser>)userStore;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel? Input { get; set; }

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme>? ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Nome")]
            public string? FirstName { get; set; }

            [Required]
            [Display(Name = "Cognome")]
            public string? LastName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string? Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "La password deve essere lunga almeno {2} caratteri.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string? Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Conferma Password")]
            [Compare("Password", ErrorMessage = "La password e la conferma non corrispondono.")]
            public string? ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                user.FirstName = Input.FirstName;
                user.LastName = Input.LastName;

                // Imposta IsApproved a false all'iscrizione
                user.IsApproved = false;

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Utente registrato con successo.");

                    // Genera token conferma email
                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    // Corpo mail conferma email (utente)
                    var userEmailBody = $@"
                        <html>
                        <body>
                        <h2>Benvenuto su Full Metal Paintball Carmagnola!</h2>
                        <p>Per favore, conferma la tua email cliccando sul link sottostante:</p>
                        <p><a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Conferma la tua registrazione</a></p>
                        <p>Grazie per esserti registrato!</p>
                        </body>
                        </html>";

                    await _emailSender.SendEmailAsync(Input.Email, "Conferma la tua email", userEmailBody);

                    // Corpo mail notifica admin
                    string adminEmail = "paintballcarmagnola@gmail.com";
                    var adminEmailBody = $@"
                        <html>
                        <body>
                        <h2>Nuova Registrazione in Attesa</h2>
                        <p>È stato registrato un nuovo utente con i seguenti dati:</p>
                        <ul>
                            <li>Email: {Input.Email}</li>
                            <li>Nome: {Input.FirstName}</li>
                            <li>Cognome: {Input.LastName}</li>
                        </ul>
                        <p>Ricorda di approvare l’account dal pannello di amministrazione.</p>
                        </body>
                        </html>";

                    await _emailSender.SendEmailAsync(adminEmail, "Nuova registrazione in attesa di approvazione", adminEmailBody);

                    // NON fare login automatico: attendi conferma email + approvazione admin
                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Se fallisce la validazione, ritorna la pagina di registrazione
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Impossibile creare un'istanza di '{nameof(ApplicationUser)}'. Assicurati che abbia un costruttore pubblico senza parametri.");
            }
        }
    }
}
