using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    private static readonly string telegramToken = "8070018745:AAGkrsTogKk3opYLu7Swe840YmhH6NQt-TE";
    private static readonly string openWeatherApiKey = "d23bbdcad85a7dfc462db3b469f64672";

    private static readonly HttpClient http = new HttpClient();

    private static ITelegramBotClient botClient;

    static async Task Main()
    {
        botClient = new TelegramBotClient(telegramToken);

        using var cts = new CancellationTokenSource();

        //Настройки приёма (можно принимать только сообщения)
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        //Запускаем приём обновлений
        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Запущен бот @{me.Username}");

        Console.WriteLine("Нажмите Enter для остановки...");
        Console.ReadLine();

        //Остановить приём
        cts.Cancel();
    }

    //Обработчик обновлений
    static async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        try
        {
            if (update.Type != UpdateType.Message) return;
            var msg = update.Message;
            if (msg?.Text == null) return;
            string city = msg.Text.Trim();

            if (city.StartsWith("/start"))
            {
                await client.SendTextMessageAsync(
                    msg.Chat.Id,
                    $"Привет, {msg.From?.FirstName}! \n" +
                    "Напиши название города, и я покажу тебе погоду 🌍",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
            }
            else
            {
                await SendWeatherAsync(client, msg.Chat.Id, city, ct);
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine("HandleUpdateAsync error: " + ex);
        }
    }

    //Обработчик ошибок polling
    static Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken ct)
    {
        Console.WriteLine($"Polling error: {exception.Message}");
        return Task.CompletedTask;
    }

    // Получаем погоду и отправляем (иконку + подпись)
    static async Task SendWeatherAsync(ITelegramBotClient client, long chatId, string city, CancellationToken ct)
    {
        try
        {
                // Запрос сразу по названию города
                string url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={openWeatherApiKey}&units=metric&lang=ru";

                var response = await http.GetAsync(url, ct);
                var jsonText = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    await client.SendTextMessageAsync(chatId, $"Неправильное название города!");
                    return;
                }

                var json = JObject.Parse(jsonText);
            

            string name = json["name"]?.ToString() ?? city;
            string description = json["weather"]?[0]?["description"]?.ToString() ?? "—";
            string temp = json["main"]?["temp"]?.ToString() ?? "—";
            string humidity = json["main"]?["humidity"]?.ToString() ?? "—";
            string main = json["weather"]?[0]?["main"]?.ToString() ?? "";

            string feelsLike = json["main"]?["feels_like"]?.ToString() ?? "—";

            string emoji = GetWeatherEmoji(main);

            string weatherText =
                $"Погода в {name}:\n" +
                $"🌡Температура: {temp}°C (ощущается как {feelsLike}°C)\n" +
                $"📖Описание: {description} {emoji}\n" +
                $"💧Влажность: {humidity}%";

            await client.SendTextMessageAsync(chatId, weatherText, cancellationToken: ct);

            static string GetWeatherEmoji(string main)
            {
                return main switch
                {
                    "Clear" => "☀️",
                    "Clouds" => "☁️",
                    "Rain" => "🌧",
                    "Drizzle" => "🌦",
                    "Thunderstorm" => "⛈",
                    "Snow" => "❄️",
                    "Mist" or "Fog" or "Haze" => "🌫",
                    _ => "🌍"
                };
            }

        }


        catch (HttpRequestException ex)
        {
            Console.WriteLine("HTTP error: " + ex);
            await client.SendTextMessageAsync(chatId, "Ошибка при запросе API погоды.", cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine("General error: " + ex);
            await client.SendTextMessageAsync(chatId, "Не удалось получить погоду. Проверьте название города.", cancellationToken: ct);
        }
    }
}
