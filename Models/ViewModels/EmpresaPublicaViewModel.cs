using EmpresaAgendamento.Models;
using System.Collections.Generic;

namespace EmpresaAgendamento.ViewModels
{
    public class EmpresaPublicaViewModel
    {
        // 🏢 Empresa principal
        public Empresa Empresa { get; set; }

        // 💇 Serviços da empresa
        public List<Servico> Servicos { get; set; } = new();

        // 📅 (futuro) Agendamentos do dia / destaque
        public List<Agendamento> Agendamentos { get; set; } = new();

        // ⭐ (futuro) avaliações
        public double? NotaMedia { get; set; }
        public int TotalAvaliacoes { get; set; }

        // 🧠 flags auxiliares (evita lógica na View)
        public bool TemServicos => Servicos != null && Servicos.Count > 0;

        public bool TemEndereco =>
            !string.IsNullOrEmpty(Empresa?.Endereco);

        public bool TemContato =>
            !string.IsNullOrEmpty(Empresa?.Telefone) ||
            !string.IsNullOrEmpty(Empresa?.WhatsApp) ||
            !string.IsNullOrEmpty(Empresa?.Email);
    }
}