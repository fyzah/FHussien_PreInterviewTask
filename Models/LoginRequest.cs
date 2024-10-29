using System.ComponentModel.DataAnnotations;

namespace FHussien_PreInterviewTask.Models
{
    public class LoginRequest
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
