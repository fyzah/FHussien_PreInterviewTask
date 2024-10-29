using System.ComponentModel.DataAnnotations;

namespace FHussien_PreInterviewTask.Models
{
    public class Company
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }
}
