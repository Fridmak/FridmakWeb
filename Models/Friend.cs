using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models
{
    public class Friend
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? Contact { get; set; }
        public bool Approved { get; set; }
    }
}
