using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
// Rimuoviamo questo using diretto per IEmailSender perché sarà gestito tramite IEmailService:
// using Microsoft.AspNetCore.Identity.UI.Services; 
using Full_Metal_Paintball_Carmagnola.Models;

namespace Full_Metal_Paintball_Carmagnola.Services
{
    // MODIFICATO QUI: Ora implementa la tua interfaccia IEmailService
    // I metodi di IEmailSender (SendEmailAsync) sono inclusi perché IEmailService estende IEmailSender
    public class EmailSender : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Metodo generico per inviare email (conferme, notifiche, etc)
        // Questo metodo è richiesto da IEmailSender (tramite IEmailService)
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            using var smtpClient = new SmtpClient(smtpSettings["Host"], int.Parse(smtpSettings["Port"]))
            {
                Credentials = new NetworkCredential(smtpSettings["User"], smtpSettings["Password"]),
                EnableSsl = bool.Parse(smtpSettings["EnableSsl"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpSettings["User"]),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
        }

        // Metodo specifico per inviare notifica di nuovo tesseramento
        // Questo metodo è richiesto dalla tua interfaccia IEmailService
        public async Task SendTesseramentoNotification(TesseramentoViewModel model, string firmaPath)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            using var smtpClient = new SmtpClient(smtpSettings["Host"], int.Parse(smtpSettings["Port"]))
            {
                Credentials = new NetworkCredential(smtpSettings["User"], smtpSettings["Password"]),
                EnableSsl = bool.Parse(smtpSettings["EnableSsl"])
            };

            var subject = $"Nuovo tesseramento ricevuto: {model.Nome} {model.Cognome}";

            var bodyHtml = $@"
<html>
<head>
  <style>
    body {{
      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      background-color: #f8f9fa;
      color: #212529;
      padding: 30px;
      margin: 0;
    }}
    .container {{
      max-width: 600px;
      background: white;
      border-radius: 8px;
      padding: 25px 30px;
      box-shadow: 0 4px 10px rgba(0,0,0,0.1);
      margin: auto;
      text-align: center;
    }}
    .header {{
      margin-bottom: 20px;
    }}
    .header img {{
      width: auto;
      max-width: 180px;
      height: auto;
      max-height: 180px;
      object-fit: contain;
      display: block;
      margin: 0 auto 15px auto;
    }}
    h1 {{
      color: #0d6efd;
      font-weight: 700;
      font-size: 1.8rem;
      margin: 10px 0 25px 0;
      text-align: center;
    }}
    table.info-table {{
      margin: 0 auto;
      border-collapse: collapse;
      width: 100%;
      max-width: 500px;
      text-align: left;
    }}
    table.info-table th, table.info-table td {{
      padding: 10px 12px;
      border-bottom: 1px solid #dee2e6;
      vertical-align: middle;
    }}
    table.info-table th {{
      width: 35%;
      font-weight: 600;
      color: #495057;
      text-align: right;
      white-space: nowrap;
    }}
    table.info-table td {{
      color: #3c3c3c;
    }}
    table.info-table tr:last-child td {{
      border-bottom: none;
    }}
    .firma-img {{
      display: block;
      max-width: 280px;
      max-height: 100px;
      width: auto;
      height: auto;
      margin: 25px auto 0 auto;
      border: 1px solid #dee2e6;
      border-radius: 4px;
    }}
    .footer {{
      text-align: center;
      font-size: 0.85rem;
      color: #6c757d;
      margin-top: 30px;
      border-top: 1px solid #dee2e6;
      padding-top: 15px;
    }}
    a {{
      color: #0d6efd;
      text-decoration: none;
    }}
    a:hover {{
      text-decoration: underline;
    }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <img src='https://i.imgur.com/K9Ugseg.gif' alt='Logo Full Metal Paintball' />
      <h1>Nuovo Tesseramento Ricevuto</h1>
    </div>

    <table class='info-table'>
      <tr><th>Nome:</th><td>{model.Nome}</td></tr>
      <tr><th>Cognome:</th><td>{model.Cognome}</td></tr>
      <tr><th>Data di Nascita:</th><td>{model.DataNascita:dd/MM/yyyy}</td></tr>
      <tr><th>Genere:</th><td>{model.Genere}</td></tr>
      <tr><th>Comune di Nascita:</th><td>{model.ComuneNascita}</td></tr>
      <tr><th>Comune di Residenza:</th><td>{model.ComuneResidenza}</td></tr>
      <tr><th>Email:</th><td><a href='mailto:{model.Email}'>{model.Email}</a></td></tr>
      <tr><th>Codice Fiscale:</th><td>{model.CodiceFiscale}</td></tr>
      <tr><th>Minorenne:</th><td>{model.Minorenne}</td></tr>
      {(model.Minorenne == "Sì" ? $"<tr><th>Genitore:</th><td>{model.NomeGenitore} {model.CognomeGenitore}</td></tr>" : "")}
      <tr><th>Data Creazione:</th><td>{model.DataCreazione:dd/MM/yyyy HH:mm}</td></tr>
    </table>

    {(string.IsNullOrEmpty(firmaPath) ? "" : $"<img src='{firmaPath}' alt='Firma' class='firma-img' />")}

    <div class='footer'>
      <p>Grazie per aver scelto A.S.D. Full Metal Paintball Carmagnola.</p>
      <p>Questo messaggio è generato automaticamente, ti preghiamo di non rispondere.</p>
    </div>
  </div>
</body>
</html>";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpSettings["User"]),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            mailMessage.To.Add(model.Email); // destinatario principale: il tesserato
            mailMessage.CC.Add("paintballcarmagnola@gmail.com"); // copia a te

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}