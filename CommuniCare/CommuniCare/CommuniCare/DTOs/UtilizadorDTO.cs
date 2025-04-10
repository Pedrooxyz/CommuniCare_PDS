﻿using System.ComponentModel.DataAnnotations;

namespace CommuniCare.DTOs
{
    public class UtilizadorDTO
    {
        [Required]
        public string NomeUtilizador { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } 

        [Required]
        public string Password { get; set; }
    }
}
