using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaFullProfileV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EmailContato",
                table: "Empresas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<bool>(
                name: "AceitaCartaoCredito",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AceitaCartaoDebito",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AceitaDinheiro",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AceitaPix",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AnoFundacao",
                table: "Empresas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AtendimentoOnline",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CEP",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapaBannerUrl",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComodidadesExtras",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Complemento",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoNumero",
                table: "Empresas",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoTipo",
                table: "Empresas",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Facebook",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instagram",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Empresas",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkAgendamentoExterno",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkMaps",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Empresas",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeFantasia",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NotaMedia",
                table: "Empresas",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Numero",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelamentoMaximo",
                table: "Empresas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegmentoAtuacao",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Site",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slogan",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TemAcessibilidade",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TemArCondicionado",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TemEstacionamento",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TemWifi",
                table: "Empresas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TikTok",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalAvaliacoes",
                table: "Empresas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UF",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsApp",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YouTube",
                table: "Empresas",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AceitaCartaoCredito",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "AceitaCartaoDebito",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "AceitaDinheiro",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "AceitaPix",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "AnoFundacao",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "AtendimentoOnline",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CEP",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CapaBannerUrl",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "ComodidadesExtras",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Complemento",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "DocumentoNumero",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "DocumentoTipo",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Facebook",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Instagram",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LinkAgendamentoExterno",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LinkMaps",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "NomeFantasia",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "NotaMedia",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "ParcelamentoMaximo",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "SegmentoAtuacao",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Site",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Slogan",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "TemAcessibilidade",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "TemArCondicionado",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "TemEstacionamento",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "TemWifi",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "TikTok",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "TotalAvaliacoes",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "UF",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "WhatsApp",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "YouTube",
                table: "Empresas");

            migrationBuilder.AlterColumn<string>(
                name: "EmailContato",
                table: "Empresas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);
        }
    }
}
