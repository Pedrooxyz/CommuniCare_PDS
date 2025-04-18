﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommuniCare.Models;
using CommuniCare.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CommuniCare.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PedidosAjudaController : ControllerBase
    {
        private readonly CommuniCareContext _context;

        public PedidosAjudaController(CommuniCareContext context)
        {
            _context = context;
        }

        // GET: api/PedidoAjudas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PedidoAjuda>>> GetPedidoAjuda()
        {
            return await _context.PedidosAjuda.ToListAsync();
        }

        // GET: api/PedidoAjudas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PedidoAjuda>> GetPedidoAjuda(int id)
        {
            var pedidoAjuda = await _context.PedidosAjuda.FindAsync(id);

            if (pedidoAjuda == null)
            {
                return NotFound();
            }

            return pedidoAjuda;
        }

        // PUT: api/PedidoAjudas/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPedidoAjuda(int id, PedidoAjuda pedidoAjuda)
        {
            if (id != pedidoAjuda.PedidoId)
            {
                return BadRequest();
            }

            _context.Entry(pedidoAjuda).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PedidoAjudaExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/PedidoAjudas
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<PedidoAjuda>> PostPedidoAjuda(PedidoAjuda pedidoAjuda)
        {
            _context.PedidosAjuda.Add(pedidoAjuda);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPedidoAjuda", new { id = pedidoAjuda.PedidoId }, pedidoAjuda);
        }

        // DELETE: api/PedidoAjudas/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePedidoAjuda(int id)
        {
            var pedidoAjuda = await _context.PedidosAjuda.FindAsync(id);
            if (pedidoAjuda == null)
            {
                return NotFound();
            }

            _context.PedidosAjuda.Remove(pedidoAjuda);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PedidoAjudaExists(int id)
        {
            return _context.PedidosAjuda.Any(e => e.PedidoId == id);
        }

        [HttpPost("pedir")]
        [Authorize]
        public async Task<IActionResult> CriarPedidoAjuda([FromBody] PedidoAjudaDTO pedidoData)
        {
            if (pedidoData == null ||
                string.IsNullOrWhiteSpace(pedidoData.DescPedido) ||
                pedidoData.NHoras <= 0 ||
                pedidoData.NPessoas <= 0)
            {
                return BadRequest("Dados inválidos.");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var utilizador = await _context.Utilizadores.FindAsync(utilizadorId);
            if (utilizador == null)
            {
                return NotFound("Utilizador não encontrado.");
            }

            var pedido = new PedidoAjuda
            {
                DescPedido = pedidoData.DescPedido,
                HorarioAjuda = pedidoData.HorarioAjuda,
                NHoras = pedidoData.NHoras,
                NPessoas = pedidoData.NPessoas,
                UtilizadorId = utilizadorId,
                Estado = EstadoPedido.Pendente
            };

            _context.PedidosAjuda.Add(pedido);
            await _context.SaveChangesAsync();

            var administradores = await _context.Utilizadores
                .Where(u => u.TipoUtilizadorId == 2)
                .ToListAsync();

            var notificacoes = administradores.Select(admin => new Notificacao
            {
                Mensagem = $"Foi criado um novo pedido de ajuda por {utilizador.NomeUtilizador}.",
                Lida = 0,
                DataMensagem = DateTime.Now,
                PedidoId = pedido.PedidoId,
                UtilizadorId = admin.UtilizadorId,
                ItemId = null
            }).ToList();

            _context.Notificacaos.AddRange(notificacoes);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Mensagem = "Pedido de ajuda criado com sucesso. Notificações enviadas aos administradores."
            });
        }


        [HttpPost("{pedidoId}/voluntariar")]
        [Authorize]
        public async Task<IActionResult> Voluntariar(int pedidoId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var pedido = await _context.PedidosAjuda.FindAsync(pedidoId);
            if (pedido == null || pedido.Estado != EstadoPedido.Aberto)
            {
                return BadRequest("Pedido não encontrado ou já fechado.");
            }

            bool jaVoluntariado = await _context.Voluntariados
                .AnyAsync(v => v.PedidoId == pedidoId && v.UtilizadorId == utilizadorId);

            if (jaVoluntariado)
            {
                return BadRequest("Utilizador já se voluntariou para este pedido.");
            }

            var voluntariado = new Voluntariado
            {
                PedidoId = pedidoId,
                UtilizadorId = utilizadorId
            };

            _context.Voluntariados.Add(voluntariado);
            await _context.SaveChangesAsync();

            var admins = await _context.Utilizadores
                .Where(u => u.TipoUtilizadorId == 2)
                .ToListAsync();

            var utilizador = await _context.Utilizadores.FindAsync(utilizadorId);

            foreach (var admin in admins)
            {
                var notificacao = new Notificacao
                {
                    Mensagem = $"{utilizador?.NomeUtilizador ?? "Um utilizador"} voluntariou-se para o pedido #{pedidoId}.",
                    Lida = 0,
                    DataMensagem = DateTime.Now,
                    PedidoId = pedidoId,
                    UtilizadorId = admin.UtilizadorId,
                    ItemId = null 
                };

                _context.Notificacaos.Add(notificacao);
            }

            await _context.SaveChangesAsync();

            return Ok("Pedido de voluntariado registado com sucesso. Aguardando aprovação do administrador.");
        }


        #region Administrador


        //É NECESSÁRIO VER COMO FAZEMOS PARA SE FOREM VÁRIOS VOLUNTARIOS E NAO SO 1
        [HttpPost("{pedidoId}/aceitar-voluntario")]
        [Authorize]
        public async Task<IActionResult> AceitarVoluntario(int pedidoId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var utilizador = await _context.Utilizadores.FindAsync(utilizadorId);
            if (utilizador == null || utilizador.TipoUtilizadorId != 2)
            {
                return Forbid("Apenas administradores podem aceitar voluntários.");
            }

            var pedido = await _context.PedidosAjuda
                .Include(p => p.Voluntariados)
                .Include(p => p.Utilizador)
                .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

            if (pedido == null || pedido.Estado != EstadoPedido.Aberto)
            {
                return BadRequest("Pedido não encontrado ou já fechado.");
            }

            var voluntariado = pedido.Voluntariados.FirstOrDefault();
            if (voluntariado == null)
            {
                return BadRequest("Nenhum voluntário disponível para este pedido.");
            }

            pedido.Estado = EstadoPedido.EmProgresso;

            var notificacaoVoluntario = new Notificacao
            {
                Mensagem = $"Foste aceite como voluntário para o pedido #{pedido.PedidoId}.",
                Lida = 0,
                DataMensagem = DateTime.Now,
                PedidoId = pedido.PedidoId,
                UtilizadorId = voluntariado.UtilizadorId,
                ItemId = null
            };
            _context.Notificacaos.Add(notificacaoVoluntario);

            var notificacaoRequisitante = new Notificacao
            {
                Mensagem = $"Um voluntário foi aceite para o teu pedido #{pedido.PedidoId}.",
                Lida = 0,
                DataMensagem = DateTime.Now,
                PedidoId = pedido.PedidoId,
                UtilizadorId = pedido.UtilizadorId,
                ItemId = null
            };
            _context.Notificacaos.Add(notificacaoRequisitante);

            await _context.SaveChangesAsync();

            return Ok("Voluntário aceite com sucesso e pedido atualizado para 'Em Progresso'.");
        }

        [HttpPost("{pedidoId}/rejeitar-voluntario")]
        [Authorize]
        public async Task<IActionResult> RejeitarVoluntario(int pedidoId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var utilizador = await _context.Utilizadores.FindAsync(utilizadorId);
            if (utilizador == null || utilizador.TipoUtilizadorId != 2)
            {
                return Forbid("Apenas administradores podem rejeitar voluntários.");
            }

            var pedido = await _context.PedidosAjuda
                .Include(p => p.Voluntariados)
                .Include(p => p.Utilizador) // Requisitante
                .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

            if (pedido == null || pedido.Estado != EstadoPedido.Aberto)
            {
                return BadRequest("Pedido não encontrado ou já fechado.");
            }

            var voluntariado = pedido.Voluntariados.FirstOrDefault();
            if (voluntariado == null)
            {
                return BadRequest("Nenhum voluntário disponível para este pedido.");
            }

            var notificacaoVoluntario = new Notificacao
            {
                Mensagem = $"A tua candidatura como voluntário para o pedido #{pedido.PedidoId} foi rejeitada.",
                Lida = 0,
                DataMensagem = DateTime.Now,
                PedidoId = pedido.PedidoId,
                UtilizadorId = voluntariado.UtilizadorId,
                ItemId = null
            };
            _context.Notificacaos.Add(notificacaoVoluntario);

            await _context.SaveChangesAsync();

            return Ok("Voluntário rejeitado com sucesso e pedido atualizado para 'Pendente'.");
        }


        [HttpPost("{pedidoId}/rejeitar-pedido")]
        [Authorize]
        public async Task<IActionResult> RejeitarPedidoAjuda(int pedidoId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var utilizador = await _context.Utilizadores.FindAsync(utilizadorId);
            if (utilizador == null || utilizador.TipoUtilizadorId != 2)
            {
                return Forbid("Apenas administradores podem validar pedidos de ajuda.");
            }

            var pedido = await _context.PedidosAjuda
                .Include(p => p.Utilizador)
                .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

            if (pedido == null)
            {
                return NotFound("Pedido de ajuda não encontrado.");
            }

            if (pedido.Estado != EstadoPedido.Pendente)
            {
                return BadRequest("Este pedido já foi validado ou está em progresso/concluído.");
            }

            pedido.Estado = EstadoPedido.Rejeitado;

            var notificacao = new Notificacao
            {
                Mensagem = $"O teu pedido de ajuda #{pedido.PedidoId} foi rejeitado por um administrador por ser considerado inválido.",
                Lida = 0,
                DataMensagem = DateTime.Now,
                PedidoId = pedido.PedidoId,
                UtilizadorId = pedido.UtilizadorId,
                ItemId = null
            };
            _context.Notificacaos.Add(notificacao);

            await _context.SaveChangesAsync();

            return Ok("Pedido de ajuda rejeitado com sucesso.");
        }

        [HttpPost("{pedidoId}/validar-pedido")]
        [Authorize]
        public async Task<IActionResult> ValidarPedidoAjuda(int pedidoId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var utilizador = await _context.Utilizadores.FindAsync(utilizadorId);
            if (utilizador == null || utilizador.TipoUtilizadorId != 2)
            {
                return Forbid("Apenas administradores podem validar pedidos de ajuda.");
            }

            var pedido = await _context.PedidosAjuda.FindAsync(pedidoId);
            if (pedido == null)
            {
                return NotFound("Pedido de ajuda não encontrado.");
            }

            if (pedido.Estado != EstadoPedido.Pendente)
            {
                return BadRequest("Este pedido já foi validado ou está em progresso/concluído.");
            }

            pedido.Estado = EstadoPedido.Aberto;

            await _context.SaveChangesAsync();

            var outrosUtilizadores = await _context.Utilizadores
                .Where(u => u.UtilizadorId != utilizadorId)
                .ToListAsync();

            var notificacoes = outrosUtilizadores.Select(u => new Notificacao
            {
                Mensagem = $"O utilizador {utilizador.NomeUtilizador} criou um novo pedido de ajuda.",
                Lida = 0,
                DataMensagem = DateTime.Now,
                PedidoId = pedido.PedidoId,
                UtilizadorId = pedido.UtilizadorId,
                ItemId = null
            }).ToList();

            _context.Notificacaos.AddRange(notificacoes);
            await _context.SaveChangesAsync();

            return Ok("Pedido de ajuda validado com sucesso e colocado como 'Aberto'.");
        }

        [HttpPost("concluir/{pedidoId}")]
        [Authorize]
        public async Task<IActionResult> ConcluirPedidoAjuda(int pedidoId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var pedido = await _context.PedidosAjuda
                .Include(p => p.Utilizador)
                .Include(p => p.Voluntariados)
                .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

            if (pedido == null)
            {
                return NotFound("Pedido de ajuda não encontrado.");
            }

            if (pedido.UtilizadorId != utilizadorId)
            {
                return Forbid("Apenas o requisitante do pedido pode marcá-lo como concluído.");
            }

            if (pedido.Estado != EstadoPedido.EmProgresso)
            {
                return BadRequest("O pedido não está em progresso ou já foi concluído.");
            }

            pedido.Estado = EstadoPedido.Concluido;

            var admins = await _context.Utilizadores
                .Where(u => u.TipoUtilizadorId == 2)
                .ToListAsync();

            foreach (var admin in admins)
            {
                var notificacaoAdmin = new Notificacao
                {
                    Mensagem = $"O utilizador {pedido.Utilizador.NomeUtilizador} marcou o pedido #{pedido.PedidoId} como concluído. Valida esta conclusão.",
                    Lida = 0,
                    DataMensagem = DateTime.Now,
                    PedidoId = pedido.PedidoId,
                    UtilizadorId = admin.UtilizadorId,
                    ItemId = null
                };
                _context.Notificacaos.Add(notificacaoAdmin);
            }

            await _context.SaveChangesAsync();

            return Ok("O pedido foi marcado como concluído. Os administradores foram notificados para validar a conclusão.");
        }


        [HttpPost("validar-conclusao/{pedidoId}")]
        [Authorize]
        public async Task<IActionResult> ValidarConclusaoPedidoAjuda(int pedidoId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Utilizador não autenticado.");
            }

            int utilizadorId = int.Parse(userIdClaim.Value);

            var utilizador = await _context.Utilizadores.FindAsync(utilizadorId);
            if (utilizador == null || utilizador.TipoUtilizadorId != 2)
            {
                return Forbid("Apenas administradores podem validar pedidos de ajuda.");
            }

            var pedido = await _context.PedidosAjuda
                .Include(p => p.Utilizador) 
                .Include(p => p.Voluntariados)
                .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

            if (pedido == null)
            {
                return NotFound("Pedido de ajuda não encontrado.");
            }

            if (pedido.Estado != EstadoPedido.Concluido)
            {
                return BadRequest("O pedido não ainda não foi concluído.");
            }

            var recetor = pedido.Utilizador;

            if (recetor == null)
            {
                return BadRequest("Não foi possível determinar o recetor do pedido.");
            }

            int recompensa = pedido.RecompensaCares ?? 0;

            recetor.NumCares += recompensa;

            var transacao = new Transacao
            {
                DataTransacao = DateTime.UtcNow,
                Quantidade = recompensa
            };

            var transacaoAjuda = new TransacaoAjuda
            {
                RecetorTran = recetor.UtilizadorId,
                Transacao = transacao,
                PedidoAjuda = new List<PedidoAjuda> { pedido }
            };

            _context.Transacoes.Add(transacao);
            _context.TransacaoAjuda.Add(transacaoAjuda);

            var voluntariado = pedido.Voluntariados.FirstOrDefault();
            if (voluntariado != null)
            {
                var notificacaoVoluntario = new Notificacao
                {
                    Mensagem = $"A transação foi efetuada com sucesso para o pedido #{pedido.PedidoId}. Obrigado pela tua ajuda!",
                    Lida = 0,
                    DataMensagem = DateTime.Now,
                    PedidoId = pedido.PedidoId,
                    UtilizadorId = voluntariado.UtilizadorId,
                    ItemId = null
                };
                _context.Notificacaos.Add(notificacaoVoluntario);
            }

            await _context.SaveChangesAsync();

            return Ok("Pedido de ajuda concluído com sucesso. Recompensa atribuída, transação registada e notificação enviada.");
        }



        #endregion


    }
}
