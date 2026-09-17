using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpresaAgendamento.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReciboTokenContaReceber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReciboToken",
                table: "ContasReceber",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_ReciboToken",
                table: "ContasReceber",
                column: "ReciboToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContasReceber_ReciboToken",
                table: "ContasReceber");

            migrationBuilder.DropColumn(
                name: "ReciboToken",
                table: "ContasReceber");
        }
    }
}
