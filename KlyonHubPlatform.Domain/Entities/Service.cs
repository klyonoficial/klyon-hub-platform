namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Serviços oferecidos pela plataforma (ex: Contratar Endereço Fiscal, Reservar Sala)
    /// </summary>
    public class Service
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>URL da imagem ilustrativa do serviço</summary>
        public string? ImageUrl { get; set; }
        /// <summary>Rota/URL para a landing page do serviço</summary>
        public string Route { get; set; } = string.Empty;
        /// <summary>Ordem de exibição</summary>
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<PartnerService> PartnerServices { get; set; } = new List<PartnerService>();
    }
}

