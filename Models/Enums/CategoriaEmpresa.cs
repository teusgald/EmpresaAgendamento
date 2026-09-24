namespace EmpresaAgendamento.Models.Enums
{
    // Tipo de negócio da empresa (não confundir com Servico — isso aqui é
    // "que tipo de estabelecimento é esse", o catálogo de serviços é próprio
    // de cada empresa). Substitui o antigo Empresa.SegmentoAtuacao (texto
    // livre): lista fixa pra virar filtro de verdade em /empresas em vez de
    // um dropdown com uma opção pra cada jeito diferente que alguém digitou
    // "Salão de beleza". Lista levantada a partir do que Trinks/Booksy/Fresha
    // usam pra negócio de agendamento local, adaptada ao que o cadastro já
    // pedia (ver Helpers/CategoriaEmpresaHelper.cs pros rótulos em pt-BR).
    public enum CategoriaEmpresa
    {
        Barbearia,
        SalaoDeBeleza,
        ManicureEPedicure,
        ClinicaDeEstetica,
        SpaEMassoterapia,
        SobrancelhasECilios,
        Depilacao,
        TatuagemEPiercing,
        Maquiagem,
        AcademiaEPersonalTrainer,
        PilatesEYoga,
        Fisioterapia,
        Odontologia,
        ClinicaMedica,
        PsicologiaETerapia,
        PetShop,
        ConsultoriaEServicosProfissionais,
        Outro
    }
}
