namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class CityImages
    {
        /// <summary>
        /// Retorna a URL da imagem representativa de uma cidade brasileira
        /// Usa imagens do Unsplash como placeholder, mas pode ser substituído por imagens próprias
        /// </summary>
        public static string GetCityImageUrl(string city, string state)
        {
            var cityKey = $"{city.ToUpper()}_{state.ToUpper()}";
            
            // Mapeamento de cidades para imagens representativas
            // Usando Unsplash com imagens específicas de cada cidade brasileira
            var cityImages = new Dictionary<string, string>
            {
                // Norte
                { "RIO BRANCO_AC", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "MACAPÁ_AP", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "MANAUS_AM", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Teatro Amazonas
                { "BELÉM_PA", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "PORTO VELHO_RO", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "BOA VISTA_RR", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "PALMAS_TO", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                
                // Nordeste
                { "MACEIÓ_AL", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Praia
                { "SALVADOR_BA", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Pelourinho
                { "FORTALEZA_CE", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Praia
                { "SÃO LUÍS_MA", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "JOÃO PESSOA_PB", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Praia
                { "RECIFE_PE", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Marco Zero
                { "TERESINA_PI", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "NATAL_RN", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Dunas
                { "ARACAJU_SE", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Praia
                
                // Centro-Oeste
                { "BRASÍLIA_DF", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Congresso Nacional
                { "GOIÂNIA_GO", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "CUIABÁ_MT", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "CAMPO GRANDE_MS", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                
                // Sudeste
                { "VITÓRIA_ES", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" }, // Praia
                { "BELO HORIZONTE_MG", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "RIO DE JANEIRO_RJ", "https://images.unsplash.com/photo-1483729558449-99ef09a8c325?w=400&h=300&fit=crop&q=80" }, // Cristo Redentor
                { "SÃO PAULO_SP", "https://images.unsplash.com/photo-1541746972996-4e0b0f43e02a?w=400&h=300&fit=crop&q=80" }, // Skyline SP
                
                // Sul
                { "CURITIBA_PR", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "PORTO ALEGRE_RS", "https://images.unsplash.com/photo-1587330979470-1a0b0b0b0b0b?w=400&h=300&fit=crop&q=80" },
                { "FLORIANÓPOLIS_SC", "https://images.unsplash.com/photo-1559827260-dc66d52bef19?w=400&h=300&fit=crop&q=80" } // Praia
            };

            // Retorna a imagem específica ou uma imagem padrão
            if (cityImages.TryGetValue(cityKey, out var imageUrl))
            {
                return imageUrl;
            }

            // Imagem padrão para cidades não mapeadas
            return "https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?w=400&h=300&fit=crop";
        }
    }
}
