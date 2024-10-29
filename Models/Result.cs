using System.ComponentModel.DataAnnotations;

namespace FHussien_PreInterviewTask.Models
{
    public class Result
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string? Salt { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string Company { get; set; }
        public string FullName { get; set; }
        public DateTime Birthdate { get; set; }
    }
}
