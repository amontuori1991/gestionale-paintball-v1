using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{

    [Table("codicipromozionali")]
public class CodicePromozionale
{
    [Key]
    [Column("id")] // <--- questa è la chiave della soluzione!
    public int Id { get; set; }

    [Column("instagramhandle")]
    public string InstagramAccount { get; set; }

    [Column("codice")]
    public string Codice { get; set; }
       
        
        [Column("alias")]
        public string Alias { get; set; }



        [Column("datagenerazione")]
    public DateTime DataCreazione { get; set; }

    [Column("datascadenza")]
    public DateTime DataScadenza { get; set; }

    [Column("utilizzato")]
    public bool Utilizzato { get; set; }
}
}