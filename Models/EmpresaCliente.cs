using System.ComponentModel.DataAnnotations.Schema;

namespace EmpresaAgendamento.Models
{
    public class EmpresaCliente
    {
        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; }

        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; }
    }
}