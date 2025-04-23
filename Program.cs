using Microsoft.Extensions.Configuration;
using RideFuel.Scraper.Services;

namespace RideFuel.Scraper;

class Program
{
    static void Main(string[] args)
    {
        // 📚 Carregar configurações
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var email = config["UberCredentials:Email"];
        var password = config["UberCredentials:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Erro: Email ou senha não foram definidos no appsettings.json.");
            return;
        }

        // 🚀 Executar scraping
        var scraper = new UberScraperService2(email, password);
        scraper.Run();
    }
}
