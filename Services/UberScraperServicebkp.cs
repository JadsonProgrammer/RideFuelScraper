using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;


namespace RideFuel.Scraper;

public class UberScraperService
{
    private readonly string _email;
    private readonly string _password;

    public UberScraperService(string email, string password)
    {
        this._email = email;
        _password = password;
    }

    public async Task RunAsync()
    {
        var options = new ChromeOptions();
        options.AddArgument("--disable-webrtc");
        options.AddArgument("--disable-infobars");
        options.AddArgument("--disable-notifications");
        options.AddArgument("--start-maximized");

        using var driver = new ChromeDriver(options);

        try
        {
            Console.WriteLine("➡️ Acessando página de login da Uber...");
            driver.Navigate().GoToUrl("https://auth.uber.com/login/");

            var emailField = driver.FindElement(By.Name("email"));
            emailField.SendKeys(_email);

            var botaoAvancar = driver.FindElement(By.CssSelector("button[type='submit']"));
            botaoAvancar.Click();

            Console.WriteLine("🧠 Resolva o CAPTCHA manualmente se houver. Você tem até 3 minutos para concluir.");
            var captchaTimeout = TimeSpan.FromMinutes(3);
            var captchaStart = DateTime.Now;
            while ((DateTime.Now - captchaStart) < captchaTimeout && driver.FindElements(By.CssSelector("input[id^='PHONE_SMS_OTP-']")).Count < 4)
            {
                await Task.Delay(1000);
            }

            Console.Write("🔐 Digite o código SMS recebido e pressione Enter: ");
            var smsCode = Console.ReadLine();

            var codeInputs = driver.FindElements(By.CssSelector("input[id^='PHONE_SMS_OTP-']"));
            for (int i = 0; i < smsCode.Length && i < codeInputs.Count; i++)
            {
                codeInputs[i].Clear();
                codeInputs[i].SendKeys(smsCode[i].ToString());
            }

            Console.WriteLine("⏳ Aguardando campo de senha aparecer...");
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            wait.Until(d => d.FindElements(By.Id("PASSWORD")).Count > 0);

            Console.WriteLine("🔑 Campo de senha encontrado. Efetuando login...");
            var senhaField = driver.FindElement(By.Id("PASSWORD"));
            senhaField.SendKeys(_password);
            senhaField.Submit();

            await Task.Delay(10000);
            Console.WriteLine("🌐 Acessando página de viagens...");
            driver.Navigate().GoToUrl("https://drivers.uber.com/earnings/activities");
            await Task.Delay(5000);

            Console.WriteLine("🔁 Carregando todas as corridas...");
            while (true)
            {
                try
                {
                    var botaoCarregarMais = driver.FindElement(By.CssSelector("button[data-testid='loadMore']"));
                    if (botaoCarregarMais.Displayed && botaoCarregarMais.Enabled)
                    {
                        Console.WriteLine("📥 Clicando em 'Carregar mais'...");
                        botaoCarregarMais.Click();
                        await Task.Delay(3000);
                    }
                    else break;
                }
                catch (NoSuchElementException)
                {
                    break;
                }
            }

            Console.WriteLine("📦 Extraindo informações das viagens...");
            var trips = new List<object>();
            var elementos = driver.FindElements(By.CssSelector("div[data-testid='trip-expand-container']"));
            foreach (var el in elementos)
            {
                try
                {
                    var data = el.FindElement(By.CssSelector("h3"))?.Text;
                    var destino = el.FindElement(By.CssSelector("div[data-testid='trip-location-dropoff']"))?.Text;
                    var preco = el.FindElement(By.CssSelector("span[data-testid='trip-fare']"))?.Text;
                    var id = el.GetAttribute("data-trip-id") ?? Guid.NewGuid().ToString();

                    trips.Add(new
                    {
                        Id = id,
                        Data = data,
                        Destino = destino,
                        Valor = preco
                    });
                }
                catch
                {
                    continue;
                }
            }

            var pastaSaida = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            Directory.CreateDirectory(pastaSaida);
            var nomeArquivo = $"Trips_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var caminhoCompleto = Path.Combine(pastaSaida, nomeArquivo);

            await File.WriteAllTextAsync(caminhoCompleto, JsonSerializer.Serialize(trips, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"\n✅ Arquivo salvo em: {caminhoCompleto}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }

        Console.WriteLine("\n🧹 Pressione qualquer tecla para fechar o navegador...");
        Console.ReadKey();
        driver.Quit();
    }
}
