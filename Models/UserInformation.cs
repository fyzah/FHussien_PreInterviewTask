using System.ComponentModel.DataAnnotations;

namespace FHussien_PreInterviewTask.Models
{
    public class UserInformation
    {
        [Key]
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Birthdate { get; set; }
    }
}
