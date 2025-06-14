using CommuniCare.DTOs;

public class PedidoPendenteDTO
{
    public int PedidoId { get; set; }
    public string Titulo { get; set; }
    public string Descricao { get; set; }
    public DateTime? DataCriacao { get; set; }
    public int NumeroVoluntarios { get; set; }
    public string? FotografiaPA { get; set; } 
    public int NHoras { get; set; }            
    public int NPessoas { get; set; }          
    public TransacaoDTO? Transacao { get; set; }
    public int? RecompensaCares { get; set; }
}