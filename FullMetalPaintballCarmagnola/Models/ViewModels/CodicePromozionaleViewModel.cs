namespace Full_Metal_Paintball_Carmagnola.Models.ViewModels
{
    public class CodicePromozionaleViewModel
{
    public int Id { get; set; }
    public string Codice { get; set; }
    public string NomeInstagram { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataScadenza { get; set; }
    public bool Utilizzato { get; set; }
}
}