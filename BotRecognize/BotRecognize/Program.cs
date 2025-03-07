using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;
using Telegram.Bot;
using BotRecognize.DB;
using BotRecognize.Controller;
using Microsoft.EntityFrameworkCore;

namespace BotRecognize
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) => ConfigureServices(services))
                .UseConsoleLifetime()
                .Build();

            Console.WriteLine("Services launch");

            await host.RunAsync();
            Console.WriteLine("Services stop");
        }

        public static void ConfigureServices(IServiceCollection services)
        {
            // Регистрируем Telegram-бота
            services.AddSingleton<ITelegramBotClient>(provider => new TelegramBotClient("7935930217:AAF_KST07z_gL35RnoNoSdC3lA6H366ST8s"));

            // Регистрируем основной сервис бота и фоновые задачи
            services.AddHostedService<Bot>();

            // Регистрируем контекст базы данных
            //services.AddDbContext<AppDbContext>(options => options.UseNpgsql("Host=localhost;Database=recbot;Username=postgres;Password=12345Ob@"));
            services.AddScoped<TextMessageController>();
            services.AddScoped<DocumentMessageController>();
        }
    }
}
