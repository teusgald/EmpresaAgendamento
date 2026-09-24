using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriaEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "Empresas",
                type: "int",
                nullable: true);

            // Migra o texto livre que já existia em SegmentoAtuacao pra um
            // valor fixo de Categoria, por palavra-chave — mesma lista de
            // Helpers/CategoriaEmpresaHelper.MapearDeTextoLivre, duplicada
            // aqui em SQL de propósito: uma migration tem que continuar
            // rodando do jeito que rodou mesmo que o helper mude depois.
            // Ordem = a mesma do helper (primeira que bater vence, por isso
            // o "AND Categoria IS NULL" em cada UPDATE); os números são a
            // ordem de declaração do enum CategoriaEmpresa (0-based).
            // Empresa sem SegmentoAtuacao preenchido fica com Categoria nula
            // — não força "Outro" em quem nunca informou nada.
            var mapaPalavraChave = new (int Categoria, string[] Palavras)[]
            {
                (0, new[] { "barbear", "barber" }),                                   // Barbearia
                (1, new[] { "salao", "salão", "beleza", "cabelei" }),                  // SalaoDeBeleza
                (2, new[] { "manicur", "pedicur", "unha", "esmalter" }),               // ManicureEPedicure
                (3, new[] { "estetic", "estétic" }),                                  // ClinicaDeEstetica
                (4, new[] { "spa", "massage", "massoter" }),                          // SpaEMassoterapia
                (5, new[] { "sobrancel", "cilio", "cílio", "lash" }),                  // SobrancelhasECilios
                (6, new[] { "depila" }),                                              // Depilacao
                (7, new[] { "tatua", "tattoo", "piercing" }),                         // TatuagemEPiercing
                (8, new[] { "maquiag", "make" }),                                     // Maquiagem
                (9, new[] { "academia", "personal", "fitness", "gym" }),              // AcademiaEPersonalTrainer
                (10, new[] { "pilates", "yoga" }),                                    // PilatesEYoga
                (11, new[] { "fisioterap" }),                                         // Fisioterapia
                (12, new[] { "odont", "dentist" }),                                   // Odontologia
                (13, new[] { "clinic", "clínic", "médic", "medic", "consultorio", "consultório" }), // ClinicaMedica
                (14, new[] { "psicolog", "terapia", "terapeut" }),                    // PsicologiaETerapia
                (15, new[] { "pet", "tosa" }),                                        // PetShop
                (16, new[] { "consultoria", "advocacia", "contab" }),                 // ConsultoriaEServicosProfissionais
            };

            foreach (var (categoria, palavras) in mapaPalavraChave)
            {
                foreach (var palavra in palavras)
                {
                    migrationBuilder.Sql(
                        $"UPDATE Empresas SET Categoria = {categoria} " +
                        $"WHERE Categoria IS NULL AND SegmentoAtuacao IS NOT NULL " +
                        $"AND LOWER(SegmentoAtuacao) LIKE '%{palavra}%';");
                }
            }

            // Sobrou SegmentoAtuacao preenchido sem bater em nenhuma
            // palavra-chave acima → "Outro" (17), em vez de ficar sem
            // categoria — a empresa TINHA informado algo, só não deu pra
            // classificar automaticamente.
            migrationBuilder.Sql(
                "UPDATE Empresas SET Categoria = 17 " +
                "WHERE Categoria IS NULL AND SegmentoAtuacao IS NOT NULL AND LTRIM(RTRIM(SegmentoAtuacao)) <> '';");

            migrationBuilder.DropColumn(
                name: "SegmentoAtuacao",
                table: "Empresas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Empresas");

            migrationBuilder.AddColumn<string>(
                name: "SegmentoAtuacao",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
