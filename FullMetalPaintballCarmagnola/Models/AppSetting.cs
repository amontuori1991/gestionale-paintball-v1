using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class AppSetting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Value { get; set; }
    }
}
