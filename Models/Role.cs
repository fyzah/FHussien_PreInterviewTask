﻿using System.ComponentModel.DataAnnotations;

namespace FHussien_PreInterviewTask.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
