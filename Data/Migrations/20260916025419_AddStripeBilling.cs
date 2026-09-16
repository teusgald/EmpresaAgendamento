using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeCouponIdPromocional",
                table: "Planos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceIdAnual",
                table: "Planos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceIdMensal",
                table: "Planos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAnual",
                table: "Planos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssinaturaStatus",
                table: "Empresas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssinaturaValidaAte",
                table: "Empresas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Empresas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Empresas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripeCouponIdPromocional",
                table: "Planos");

            migrationBuilder.DropColumn(
                name: "StripePriceIdAnual",
                table: "Planos");

            migrationBuilder.DropColumn(
                name: "StripePriceIdMensal",
                table: "Planos");

            migrationBuilder.DropColumn(
                name: "ValorAnual",
                table: "Planos");

            migrationBuilder.DropColumn(
                name: "AssinaturaStatus",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "AssinaturaValidaAte",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Empresas");
        }
    }
}
