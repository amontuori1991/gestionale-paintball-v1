using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models;

public sealed class CompanyProfile : IValidatableObject
{
    [Required(ErrorMessage = "Inserisci il nome dell'associazione.")]
    [StringLength(200)]
    [Display(Name = "Nome associazione")]
    public string Name { get; set; } = "";

    [RegularExpression(@"(?:[0-9]{11}|[A-Z0-9]{16})", ErrorMessage = "Inserisci un codice fiscale di 11 cifre oppure 16 caratteri alfanumerici.")]
    [Display(Name = "Codice fiscale")]
    public string? TaxCode { get; set; }

    [EmailAddress(ErrorMessage = "Inserisci un indirizzo email valido.")]
    [StringLength(254)]
    [Display(Name = "Indirizzo email")]
    public string? Email { get; set; }

    [StringLength(300)]
    [Display(Name = "Indirizzo del campo")]
    public string? FieldAddress { get; set; }

    [Phone(ErrorMessage = "Inserisci un numero di telefono valido, comprensivo di prefisso.")]
    [StringLength(40)]
    [Display(Name = "Numero di telefono")]
    public string? Phone { get; set; }

    [RegularExpression(@"@[A-Za-z0-9._]{1,30}", ErrorMessage = "Inserisci il nome account Instagram, non il link (massimo 30 caratteri, lettere, numeri, punti o underscore).")]
    [Display(Name = "Account Instagram")]
    public string? Instagram { get; set; }

    [StringLength(500)]
    [Display(Name = "Link al sito")]
    public string? Website { get; set; }

    [StringLength(200)]
    [Display(Name = "Nome pagina Facebook")]
    public string? FacebookPage { get; set; }

    public void Normalize()
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        Name = Clean(Name) ?? "";
        TaxCode = Clean(TaxCode)?.ToUpperInvariant();
        Email = Clean(Email); FieldAddress = Clean(FieldAddress); Phone = Clean(Phone);
        Instagram = Clean(Instagram);
        if (Instagram != null) Instagram = "@" + Instagram.TrimStart('@');
        Website = Clean(Website);
        if (Website != null && !Website.Contains(':')) Website = "https://" + Website;
        FacebookPage = Clean(FacebookPage);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Website != null && (!Uri.TryCreate(Website, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)))
            yield return new ValidationResult("Inserisci un link al sito valido (https://...).", new[] { nameof(Website) });
    }
}
