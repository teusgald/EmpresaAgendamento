using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Identity;

namespace EmpresaAgendamento.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? EmpresaId { get; set; } // 🔥 VOLTA ISSO
        public Empresa? Empresa { get; set; }

        public int? ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
    }
}