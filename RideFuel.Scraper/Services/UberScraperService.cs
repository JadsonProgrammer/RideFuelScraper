using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Text.Json;

namespace RideFuel.Scraper.Services;

public class UberScraperService
{
    private readonly string _email;
    private readonly string _password;

    public UberScraperService(string email, string password)
    {
        _email = email;
        _password = password;
    }

    public async Task RunAsync()
    {
        var options = new ChromeOptions();
        options.AddArgument("--disable-webrtc");
        options.AddArgument("--disable-infobars");
        options.AddArgument("--disable-notifications");
        options.AddArgument("--start-maximized");
        options.AddArgument("--log-level=3"); // Reduz logs do Chrome
        options.AddArgument("--silent");

        using var driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), options, TimeSpan.FromMinutes(3));

        try
        {
            Console.WriteLine("➡️ Iniciando automação Uber...");
            driver.Navigate().GoToUrl("https://drivers.uber.com/earnings/activities");

            // ETAPA 1: LOGIN
            Console.WriteLine("🔑 Realizando login...");

            // Preenche e-mail
            var emailField = WaitForElement(driver, By.Name("email"), 30);
            emailField.SendKeys(_email);

            // Clica em avançar
            var botaoAvancarEmail = WaitForElement(driver, By.CssSelector("button[type='submit']"), 10);
            botaoAvancarEmail.Click();

            // ETAPA 2: VERIFICAÇÃO (CAPTCHA/SMS)
            Console.WriteLine("⏳ Aguardando verificação...");
            await WaitForSmsVerification(driver);

            // ETAPA 3: SENHA
            Console.WriteLine("🔐 Inserindo senha...");
            var senhaField = WaitForElement(driver, By.Id("PASSWORD"), 30);
            senhaField.SendKeys(_password);

            var botaoAvancarSenha = WaitForElement(driver, By.CssSelector("button[data-testid='forward-button']"), 10);
            botaoAvancarSenha.Click();

            // ETAPA 4: ACESSAR HISTÓRICO
            Console.WriteLine("📊 Acessando histórico de viagens...");
            await WaitForNavigation(driver, "https://drivers.uber.com/earnings/activities");

            // ETAPA 5: CARREGAR TODAS AS VIAGENS
            Console.WriteLine("⏬ Carregando todas as viagens...");
            await LoadAllTrips(driver);

            // ETAPA 6: EXTRAIR DADOS
            Console.WriteLine("🔍 Extraindo dados das viagens...");
            var viagens = ExtractTripsData(driver);

            // ETAPA 7: SALVAR JSON
            SaveTripsToJson(viagens);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro crítico: {ex.Message}");
            TakeScreenshot(driver, "erro_critico");
            throw;
        }
        finally
        {
            driver.Quit();
            Console.WriteLine("✅ Processo concluído. Pressione qualquer tecla para sair...");
        }
    }

    private IWebElement WaitForElement(IWebDriver driver, By by, int seconds)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(seconds));
        return wait.Until(d =>
        {
            var element = d.FindElement(by);
            return (element.Displayed && element.Enabled) ? element : null;
        });
    }

    private async Task WaitForSmsVerification(IWebDriver driver)
    {
        var timeout = TimeSpan.FromMinutes(3);
        var start = DateTime.Now;

        while ((DateTime.Now - start) < timeout &&
               driver.FindElements(By.CssSelector("input[id^='PHONE_SMS_OTP-']")).Count < 4)
        {
            await Task.Delay(1000);
        }

        Console.Write("🔢 Digite o código SMS recebido: ");
        var smsCode = Console.ReadLine();

        var codeInputs = driver.FindElements(By.CssSelector("input[id^='PHONE_SMS_OTP-']"));
        for (int i = 0; i < smsCode.Length && i < codeInputs.Count; i++)
        {
            codeInputs[i].SendKeys(smsCode[i].ToString());
        }
    }

    private async Task WaitForNavigation(IWebDriver driver, string targetUrl)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
        wait.Until(d => d.Url.StartsWith(targetUrl));
        await Task.Delay(3000);
    }

    private async Task LoadAllTrips(IWebDriver driver)
    {
        int loadedTrips = 0;
        while (true)
        {
            try
            {
                var loadMoreButton = driver.FindElement(By.CssSelector("button[data-testid='loadMore']"));
                if (loadMoreButton.Displayed && loadMoreButton.Enabled)
                {
                    loadMoreButton.Click();
                    await Task.Delay(2000);
                    loadedTrips += 10;
                    Console.WriteLine($"↕️ Carregadas +10 viagens (Total: ~{loadedTrips})");
                }
                else break;
            }
            catch { break; }
        }
    }

    private List<object> ExtractTripsData(IWebDriver driver)
    {
        var viagens = new List<object>();
        var trips = driver.FindElements(By.CssSelector("div[data-testid='trip-expand-container']"));

        foreach (var trip in trips)
        {
            try
            {
                var tipo = trip.FindElement(By.CssSelector("div[class*='trip-type']"))?.Text;
                var dataHora = trip.FindElement(By.CssSelector("h3"))?.Text.Split(' ');
                var ganhos = trip.FindElement(By.CssSelector("span[data-testid='trip-fare']"))?.Text;
                var link = trip.FindElement(By.CssSelector("a"))?.GetAttribute("href");

                viagens.Add(new
                {
                    Tipo = tipo,
                    Data = dataHora.Length > 0 ? dataHora[0] : null,
                    Hora = dataHora.Length > 1 ? dataHora[1] : null,
                    Valor = ganhos,
                    LinkDetalhes = link
                });
            }
            catch { /* Ignora erros em viagens individuais */ }
        }

        return viagens;
    }

    private void SaveTripsToJson(List<object> viagens)
    {
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);

        var fileName = $"uber_viagens_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(outputDir, fileName);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(viagens, jsonOptions));
        Console.WriteLine($"\n💾 Dados salvos em: {filePath}");
        Console.WriteLine($"📊 Total de viagens extraídas: {viagens.Count}");
    }

    private void TakeScreenshot(IWebDriver driver, string name)
    {
        try
        {
            var screenshotDir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
            Directory.CreateDirectory(screenshotDir);

            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            var filePath = Path.Combine(screenshotDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            screenshot.SaveAsFile(filePath);
            Console.WriteLine($"📸 Screenshot salvo: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Falha ao capturar screenshot: {ex.Message}");
        }
    }
}