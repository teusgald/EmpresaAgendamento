using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Helpers
{
    public static class EmpresaClienteHelper
    {
        // Garante o vínculo Empresa-Cliente sempre que um cliente interage
        // de verdade com a empresa (agenda pelo público ou pelo portal do
        // cliente) — antes, esse vínculo só era criado quando a EMPRESA
        // cadastrava o cliente manualmente, então quem se cadastrava e
        // agendava por conta própria nunca aparecia na lista de Clientes.
        public static async Task GarantirVinculoAsync(ApplicationDbContext context, int empresaId, int clienteId)
        {
            var jaVinculado = await context.EmpresaClientes
                .AnyAsync(ec => ec.EmpresaId == empresaId && ec.ClienteId == clienteId);

            if (!jaVinculado)
            {
                context.EmpresaClientes.Add(new EmpresaCliente
                {
                    EmpresaId = empresaId,
                    ClienteId = clienteId
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
