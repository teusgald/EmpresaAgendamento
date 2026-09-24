using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmpresaAgendamento.Models
{
    public class Empresa
    {
        public int Id { get; set; }

        // ─────────────────────────────────────────────
        // 🧠 IDENTIDADE
        // ─────────────────────────────────────────────

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = null!;            // Razão social

        [StringLength(150)]
        public string? NomeFantasia { get; set; }

        [StringLength(250)]
        public string? Slogan { get; set; }

        [StringLength(5000)]
        public string? Descricao { get; set; }

        // Tipo de negócio, pra filtro em /empresas — ver
        // Models/Enums/CategoriaEmpresa.cs. Era texto livre
        // (SegmentoAtuacao); virou enum fixo pra não gerar um filtro com uma
        // opção pra cada jeito diferente que uma empresa digitou o segmento.
        public Enums.CategoriaEmpresa? Categoria { get; set; }

        public int? AnoFundacao { get; set; }
        public DateTime DataCadastro { get; set; }
    = DateTime.UtcNow;

        // ─────────────────────────────────────────────
        // 🧾 DOCUMENTO (CPF / CNPJ)
        // ─────────────────────────────────────────────

        /// <summary>
        /// CPF ou CNPJ do responsável ou empresa. Opcional no cadastro
        /// inicial (o formulário de registro não coleta esse dado) —
        /// preenchido depois em Configurações da Empresa.
        /// </summary>
        [StringLength(18)]
        public string? DocumentoNumero { get; set; }

        /// <summary>
        /// CPF ou CNPJ (identifica o tipo)
        /// </summary>
        [MaxLength(10)]
        public string? DocumentoTipo { get; set; } // "CPF" ou "CNPJ"

        // ─────────────────────────────────────────────
        // 🖼️ MÍDIA
        // ─────────────────────────────────────────────

        public string? LogoUrl { get; set; }
        public string? CapaBannerUrl { get; set; }

        // ─────────────────────────────────────────────
        // 📍 ENDEREÇO
        // ─────────────────────────────────────────────

        public string? Endereco { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? UF { get; set; }
        public string? CEP { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public string? LinkMaps { get; set; }

        // ─────────────────────────────────────────────
        // ⏰ HORÁRIO
        // ─────────────────────────────────────────────

        public string? HorarioFuncionamento { get; set; }

        // ─────────────────────────────────────────────
        // 📞 CONTATO
        // ─────────────────────────────────────────────

        public string? Telefone { get; set; }
        public string? WhatsApp { get; set; }

        // "Phone number ID" do WhatsApp Business (Meta Cloud API) dedicado
        // dessa empresa — todas compartilham a mesma WABA/token (User Secrets),
        // só o remetente muda. Nulo = ainda não provisionado; envio cai pro
        // número global enquanto isso (ver WhatsAppService).
        [StringLength(40)]
        public string? WhatsAppPhoneNumberId { get; set; }

        public string? Email { get; set; }
        public string? EmailContato { get; set; }

        // ─────────────────────────────────────────────
        // 🌐 REDES SOCIAIS
        // ─────────────────────────────────────────────

        public string? Instagram { get; set; }
        public string? TikTok { get; set; }
        public string? Facebook { get; set; }
        public string? YouTube { get; set; }
        public string? Site { get; set; }

        // ─────────────────────────────────────────────
        // 💳 PAGAMENTOS
        // ─────────────────────────────────────────────

        public bool AceitaPix { get; set; }
        public bool AceitaDinheiro { get; set; }
        public bool AceitaCartaoDebito { get; set; }
        public bool AceitaCartaoCredito { get; set; }
        public int? ParcelamentoMaximo { get; set; }

        // ─────────────────────────────────────────────
        // 🧰 COMODIDADES
        // ─────────────────────────────────────────────

        public bool TemWifi { get; set; }
        public bool TemEstacionamento { get; set; }
        public bool TemAcessibilidade { get; set; }
        public bool TemArCondicionado { get; set; }
        public bool AtendimentoOnline { get; set; }

        public string? ComodidadesExtras { get; set; }

        // ─────────────────────────────────────────────
        // 📊 AVALIAÇÕES
        // ─────────────────────────────────────────────

        public double? NotaMedia { get; set; }
        public int? TotalAvaliacoes { get; set; }

        // ─────────────────────────────────────────────
        // 🚀 LANDING PAGE
        // ─────────────────────────────────────────────

        public string? Slug { get; set; }
        public string? LinkAgendamentoExterno { get; set; }

        // Cor de marca da empresa (hex, ex.: "#111111"), usada na página
        // pública, login/cadastro e modal de agendamento do cliente. Nula =
        // usa a cor padrão do Simpli Time (preto/branco).
        [StringLength(7)]
        public string? CorPrimaria { get; set; }

        [StringLength(7)]
        public string? CorSecundaria { get; set; }

        // ─────────────────────────────────────────────
        // ⚙️ CONTROLE
        // ─────────────────────────────────────────────

        public bool Ativo { get; set; } = true;

        // ─────────────────────────────────────────────
        // 💳 ASSINATURA (STRIPE)
        // ─────────────────────────────────────────────

        [StringLength(60)]
        public string? StripeCustomerId { get; set; }

        [StringLength(60)]
        public string? StripeSubscriptionId { get; set; }

        // Espelha o status da assinatura no Stripe (active, trialing,
        // past_due, canceled, incomplete...) — atualizado via webhook.
        [StringLength(30)]
        public string? AssinaturaStatus { get; set; }

        public DateTime? AssinaturaValidaAte { get; set; }

        // "mensal", "semestral" ou "anual" — escolhido na página de vendas
        // antes do cadastro; usado para já abrir o checkout certo no
        // primeiro login.
        [StringLength(20)]
        public string? TipoPlanoEscolhido { get; set; }

        // "Start", "Pro" ou "Business" — qual tier a pessoa escolheu na
        // página de vendas, antes de existir o Plano.Id (empresa ainda nem
        // tinha sido criada). Usado junto com TipoPlanoEscolhido pra saber
        // qual Plano/preço abrir no primeiro checkout.
        [StringLength(30)]
        public string? NomePlanoEscolhido { get; set; }

        // Liberado manualmente pelo painel do dono do sistema (SuperAdmin)
        // para testers/parceiros iniciais — nunca é bloqueado pelo paywall,
        // independente do status da assinatura no Stripe.
        public bool VipAcesso { get; set; }

        // Libera o Simpli AI mesmo sem o plano permitir — allowlist manual do
        // SuperAdmin pra empresa de teste, enquanto o provedor de IA estiver
        // no tier gratuito (não pode expor cliente pagante de verdade a ele).
        public bool SimpliAiBetaAtivo { get; set; }

        // ─────────────────────────────────────────────
        // 🔗 RELACIONAMENTOS
        // ─────────────────────────────────────────────

        public int? PlanoId { get; set; }
        public Plano? Plano { get; set; }
      
        public ICollection<Funcionario> Funcionarios { get; set; }
     = new List<Funcionario>();

        public ICollection<ApplicationUser> Usuarios { get; set; }
            = new List<ApplicationUser>();

        public ICollection<Cliente> Clientes { get; set; }
            = new List<Cliente>();

        public ICollection<Servico> Servicos { get; set; }
            = new List<Servico>();

        public ICollection<Produto> Produtos { get; set; }
            = new List<Produto>();

        public ICollection<Agendamento> Agendamentos { get; set; }
            = new List<Agendamento>();

        public ICollection<EmpresaCliente> EmpresaClientes { get; set; }
            = new List<EmpresaCliente>();

        // ─────────────────────────────────────────────
        // 💰 FINANCEIRO
        // ─────────────────────────────────────────────

        public ICollection<CategoriaFinanceira> CategoriasFinanceiras { get; set; }
            = new List<CategoriaFinanceira>();

        public ICollection<ContaReceber> ContasReceber { get; set; }
            = new List<ContaReceber>();

        public ICollection<ContaPagar> ContasPagar { get; set; }
            = new List<ContaPagar>();

        public ICollection<MovimentacaoFinanceira> MovimentacoesFinanceiras { get; set; }
            = new List<MovimentacaoFinanceira>();

        public ICollection<EmpresaFoto> Fotos { get; set; }
            = new List<EmpresaFoto>();

        public ICollection<Avaliacao> Avaliacoes { get; set; }
            = new List<Avaliacao>();

        public ICollection<PlanoServico> PlanosServico { get; set; }
            = new List<PlanoServico>();

        public ICollection<EmpresaHorario> Horarios { get; set; }
            = new List<EmpresaHorario>();
    }
}