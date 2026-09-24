namespace EmpresaAgendamento.Helpers
{
    // Lista de UF pros filtros de busca de empresa (Views/Publico/Empresas.cshtml
    // e Views/ClienteInicio/Index.cshtml) — evita duplicar o mesmo array de 27
    // estados em cada tela, e vira um <select> de verdade em vez de um <input
    // maxlength="2"> livre (sem validação, fácil de digitar sigla errada).
    public static class EstadoBrasilHelper
    {
        public static readonly (string Sigla, string Nome)[] Estados =
        {
            ("AC", "Acre"), ("AL", "Alagoas"), ("AP", "Amapá"), ("AM", "Amazonas"), ("BA", "Bahia"),
            ("CE", "Ceará"), ("DF", "Distrito Federal"), ("ES", "Espírito Santo"), ("GO", "Goiás"),
            ("MA", "Maranhão"), ("MT", "Mato Grosso"), ("MS", "Mato Grosso do Sul"), ("MG", "Minas Gerais"),
            ("PA", "Pará"), ("PB", "Paraíba"), ("PR", "Paraná"), ("PE", "Pernambuco"), ("PI", "Piauí"),
            ("RJ", "Rio de Janeiro"), ("RN", "Rio Grande do Norte"), ("RS", "Rio Grande do Sul"),
            ("RO", "Rondônia"), ("RR", "Roraima"), ("SC", "Santa Catarina"), ("SP", "São Paulo"),
            ("SE", "Sergipe"), ("TO", "Tocantins"),
        };
    }
}
