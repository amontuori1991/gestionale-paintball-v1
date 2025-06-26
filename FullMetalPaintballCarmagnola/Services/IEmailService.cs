using System.Threading.Tasks;
using Full_Metal_Paintball_Carmagnola.Models; // Assicurati che questo using sia corretto per TesseramentoViewModel
using Microsoft.AspNetCore.Identity.UI.Services; // Necessario per IEmailSender

namespace Full_Metal_Paintball_Carmagnola.Services
{
    // Definisci la tua interfaccia personalizzata.
    // Estende IEmailSender per includere i suoi metodi, più il tuo metodo custom.
    public interface IEmailService : IEmailSender
    {
        // Questo è il metodo personalizzato che il TesseramentoController vuole chiamare.
        Task SendTesseramentoNotification(TesseramentoViewModel model, string firmaAbsoluteUrl);
        // Se hai altri metodi custom per l'email, li aggiungi qui.

    }
}