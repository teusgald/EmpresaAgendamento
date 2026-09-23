namespace EmpresaAgendamento.Models.ViewModels
{
    public class SuperAdminUsuarioResultViewModel
    {
        public string UserId { get; set; } = null!;
        public string? Nome { get; set; }
        public string? Email { get; set; }

        // "Empresa", "Funcionario" ou "Cliente" — vem da role no Identity.
        public string Tipo { get; set; } = null!;

        public bool Ativo { get; set; }

        public int? EmpresaId { get; set; }
        public string? EmpresaNome { get; set; }
        public string? PlanoNome { get; set; }
        public bool EmpresaVip { get; set; }
    }

    public class SuperAdminUsuariosBuscaViewModel
    {
        public string? Termo { get; set; }
        public string? Tipo { get; set; }

        public bool Buscou { get; set; }
        public List<SuperAdminUsuarioResultViewModel> Resultados { get; set; } = new();
    }

    public class SuperAdminEditarEmpresaViewModel
    {
        public int EmpresaId { get; set; }
        public string EmpresaNome { get; set; } = null!;

        public int? PlanoId { get; set; }
        public bool VipAcesso { get; set; }

        public List<Plano> PlanosDisponiveis { get; set; } = new();
    }
}
