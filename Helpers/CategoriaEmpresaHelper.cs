using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Helpers
{
    // Rótulos em pt-BR pro enum CategoriaEmpresa, e a lista/ordem usada em
    // todo <select> de categoria do site (cadastro, Configurações da
    // Empresa, filtro de /empresas) — um lugar só, pra não repetir a mesma
    // lista de opções em 3 telas diferentes como acontecia com o texto livre.
    public static class CategoriaEmpresaHelper
    {
        private static readonly Dictionary<CategoriaEmpresa, string> Nomes = new()
        {
            [CategoriaEmpresa.Barbearia] = "Barbearia",
            [CategoriaEmpresa.SalaoDeBeleza] = "Salão de Beleza",
            [CategoriaEmpresa.ManicureEPedicure] = "Manicure e Pedicure",
            [CategoriaEmpresa.ClinicaDeEstetica] = "Clínica de Estética",
            [CategoriaEmpresa.SpaEMassoterapia] = "Spa e Massoterapia",
            [CategoriaEmpresa.SobrancelhasECilios] = "Sobrancelhas e Cílios",
            [CategoriaEmpresa.Depilacao] = "Depilação",
            [CategoriaEmpresa.TatuagemEPiercing] = "Estúdio de Tatuagem e Piercing",
            [CategoriaEmpresa.Maquiagem] = "Maquiagem",
            [CategoriaEmpresa.AcademiaEPersonalTrainer] = "Academia e Personal Trainer",
            [CategoriaEmpresa.PilatesEYoga] = "Pilates e Yoga",
            [CategoriaEmpresa.Fisioterapia] = "Fisioterapia",
            [CategoriaEmpresa.Odontologia] = "Odontologia",
            [CategoriaEmpresa.ClinicaMedica] = "Clínica Médica",
            [CategoriaEmpresa.PsicologiaETerapia] = "Psicologia e Terapia",
            [CategoriaEmpresa.PetShop] = "Pet Shop",
            [CategoriaEmpresa.ConsultoriaEServicosProfissionais] = "Consultoria e Serviços Profissionais",
            [CategoriaEmpresa.Outro] = "Outro",
        };

        public static string NomeExibicao(CategoriaEmpresa? categoria) =>
            categoria.HasValue && Nomes.TryGetValue(categoria.Value, out var nome) ? nome : "Sem categoria";

        // Ícone (classe Bootstrap Icons) pro chip de categoria em /empresas —
        // ver Views/Publico/Empresas.cshtml.
        private static readonly Dictionary<CategoriaEmpresa, string> Icones = new()
        {
            [CategoriaEmpresa.Barbearia] = "bi-scissors",
            [CategoriaEmpresa.SalaoDeBeleza] = "bi-stars",
            [CategoriaEmpresa.ManicureEPedicure] = "bi-hand-index-thumb",
            [CategoriaEmpresa.ClinicaDeEstetica] = "bi-magic",
            [CategoriaEmpresa.SpaEMassoterapia] = "bi-flower1",
            [CategoriaEmpresa.SobrancelhasECilios] = "bi-eye",
            [CategoriaEmpresa.Depilacao] = "bi-droplet",
            [CategoriaEmpresa.TatuagemEPiercing] = "bi-pencil",
            [CategoriaEmpresa.Maquiagem] = "bi-palette",
            [CategoriaEmpresa.AcademiaEPersonalTrainer] = "bi-heart-pulse",
            [CategoriaEmpresa.PilatesEYoga] = "bi-person-arms-up",
            [CategoriaEmpresa.Fisioterapia] = "bi-bandaid",
            [CategoriaEmpresa.Odontologia] = "bi-emoji-smile",
            [CategoriaEmpresa.ClinicaMedica] = "bi-hospital",
            [CategoriaEmpresa.PsicologiaETerapia] = "bi-chat-heart",
            [CategoriaEmpresa.PetShop] = "bi-heart",
            [CategoriaEmpresa.ConsultoriaEServicosProfissionais] = "bi-briefcase",
            [CategoriaEmpresa.Outro] = "bi-three-dots",
        };

        public static string Icone(CategoriaEmpresa categoria) =>
            Icones.TryGetValue(categoria, out var icone) ? icone : "bi-shop";

        // Ordem alfabética pelo rótulo (não pelo nome do enum) — "Outro"
        // sempre por último, não faz sentido ele competir na ordem alfabética.
        public static IReadOnlyList<CategoriaEmpresa> Ordem { get; } = Nomes.Keys
            .Where(c => c != CategoriaEmpresa.Outro)
            .OrderBy(c => Nomes[c])
            .Append(CategoriaEmpresa.Outro)
            .ToList();

        public static IReadOnlyList<(CategoriaEmpresa Valor, string Nome)> Opcoes { get; } =
            Ordem.Select(c => (c, Nomes[c])).ToList();

        // Palavra-chave por categoria (case-insensitive, contains) — usado só
        // uma vez, na migration que converte Empresa.SegmentoAtuacao (texto
        // livre, removido) pra Empresa.Categoria. Primeira que bater vence;
        // sem match nenhum = null (a migration decide o fallback, ver
        // Data/Migrations/*_AddCategoriaEmpresa.cs).
        private static readonly (CategoriaEmpresa Categoria, string[] Palavras)[] PalavrasChave =
        {
            (CategoriaEmpresa.Barbearia, new[] { "barbear", "barber" }),
            (CategoriaEmpresa.SalaoDeBeleza, new[] { "salao", "salão", "beleza", "cabelei" }),
            (CategoriaEmpresa.ManicureEPedicure, new[] { "manicur", "pedicur", "unha", "esmalter" }),
            (CategoriaEmpresa.ClinicaDeEstetica, new[] { "estetic", "estétic" }),
            (CategoriaEmpresa.SpaEMassoterapia, new[] { "spa", "massage", "massoter" }),
            (CategoriaEmpresa.SobrancelhasECilios, new[] { "sobrancel", "cilio", "cílio", "lash" }),
            (CategoriaEmpresa.Depilacao, new[] { "depila" }),
            (CategoriaEmpresa.TatuagemEPiercing, new[] { "tatua", "tattoo", "piercing" }),
            (CategoriaEmpresa.Maquiagem, new[] { "maquiag", "make" }),
            (CategoriaEmpresa.AcademiaEPersonalTrainer, new[] { "academia", "personal", "fitness", "gym" }),
            (CategoriaEmpresa.PilatesEYoga, new[] { "pilates", "yoga" }),
            (CategoriaEmpresa.Fisioterapia, new[] { "fisioterap" }),
            (CategoriaEmpresa.Odontologia, new[] { "odont", "dentist" }),
            (CategoriaEmpresa.ClinicaMedica, new[] { "clinic", "clínic", "médic", "medic", "consultorio", "consultório" }),
            (CategoriaEmpresa.PsicologiaETerapia, new[] { "psicolog", "terapia", "terapeut" }),
            (CategoriaEmpresa.PetShop, new[] { "pet", "tosa" }),
            (CategoriaEmpresa.ConsultoriaEServicosProfissionais, new[] { "consultoria", "advocacia", "contab" }),
        };

        public static CategoriaEmpresa? MapearDeTextoLivre(string? textoLivre)
        {
            if (string.IsNullOrWhiteSpace(textoLivre))
                return null;

            var normalizado = textoLivre.Trim().ToLowerInvariant();

            foreach (var (categoria, palavras) in PalavrasChave)
            {
                if (palavras.Any(p => normalizado.Contains(p)))
                    return categoria;
            }

            return null;
        }
    }
}
