using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System.Text.Json;

namespace RideFuel.Scraper.Services
{
    public class UberScraperService2
    {
        private readonly string _email;
        private readonly string _password;

        public UberScraperService2(string email, string password)
        {
            _email = email;
            _password = password;
        }

        public void Run()
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");

            using var driver = new ChromeDriver(options);

            try
            {
                driver.Navigate().GoToUrl("https://drivers.uber.com/earnings/activities");
                Thread.Sleep(3000);

                // Tela de email
                var emailInput = driver.FindElement(By.Id("PHONE_NUMBER_or_EMAIL_ADDRESS"));
                emailInput.SendKeys(_email);
                emailInput.SendKeys(Keys.Enter);
                Thread.Sleep(3000);

                // Espera tela de código SMS
                Console.Write("Digite o código SMS recebido: ");
                var codigo = Console.ReadLine();

                for (int i = 0; i < codigo.Length && i < 4; i++)
                {
                    var otpInput = driver.FindElement(By.Id($"PHONE_SMS_OTP-{i}"));
                    otpInput.SendKeys(codigo[i].ToString());
                }

                driver.FindElement(By.TagName("body")).SendKeys(Keys.Enter);
                Console.WriteLine("Código SMS enviado. Aguardando a tela da senha...");

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                wait.Until(drv => drv.FindElements(By.Id("PASSWORD")).Any());

                var passwordInput = driver.FindElement(By.Id("PASSWORD"));
                passwordInput.SendKeys(_password);
                passwordInput.SendKeys(Keys.Enter);

                Console.WriteLine("Senha enviada. Aguardando redirecionamento...");
                Thread.Sleep(8000); // aguarda o dashboard carregar

                // Extrair corridas
                var rows = driver.FindElements(By.CssSelector("tbody._css-PKJb tr"));
                var viagens = new List<object>();

                foreach (var row in rows)
                {
                    var evento = row.FindElement(By.CssSelector("td:nth-child(1) p")).Text.Trim();
                    var data = row.FindElement(By.CssSelector("td:nth-child(2) p:nth-child(1)")).Text.Trim();
                    var hora = row.FindElement(By.CssSelector("td:nth-child(2) p:nth-child(2)")).Text.Trim();
                    var ganhos = row.FindElement(By.CssSelector("td:nth-child(3) p")).Text.Trim();
                    var link = row.FindElement(By.CssSelector("td:nth-child(4) a")).GetAttribute("href");

                    viagens.Add(new
                    {
                        Tipo = evento,
                        Data = data,
                        Hora = hora,
                        Valor = ganhos,
                        LinkDetalhes = link
                    });
                }

                var json = JsonSerializer.Serialize(viagens, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine("\nViagens extraídas:\n");
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("\nPressione qualquer tecla para fechar o navegador...");
                Console.ReadKey();
                driver.Quit();
            }
        }
    }
}
