using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Net.Http;
using Telegram.Bot.Polling;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using System.Globalization;

namespace WeatherBot
{
    class Program
    {
        static bool isWeatherNotificationEnabled = false;
        static ITelegramBotClient bot = new TelegramBotClient("6385661086:AAGCcw1a4BjAKYUJjeDPrXLsp3QX0mIMhyM");
        static string weatherApiKey = "4f7a03a80d8a47a912af3f9275460b43";
        static string cityName = "Киев";
        static Message message { get; set; }
        static Thread weatherNotificationThread;
        static string time { get; set; }
        static bool StartMessege = true;
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                message = update.Message;
                if (StartMessege)
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Чтобы вызвать меню, введите команду: /weather");
                    StartMessege = false;
                }
                if (message.Text.ToLower() == "/start")
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Привет! Я погодный бот Weather. Я буду отправлять тебе погоду.");
                    return;
                }
                else if (message.Text.ToLower() == "/weather")
                {
                    await SendWeatherMenu(botClient, message.Chat);
                    return;
                }
                else if (message.Text.StartsWith("/settime"))
                {
                    string[] parts = message.Text.Split(' ');
                    if (parts.Length < 2)
                    {
                        await botClient.SendTextMessageAsync(message.Chat, "Пожалуйста, введите время в формате /settime ЧЧ:ММ");
                        return;
                    }

                    time = parts[1];
                    if (DateTime.TryParseExact(time, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime notificationTime))
                    {
                        isWeatherNotificationEnabled = true;
                        await botClient.SendTextMessageAsync(message.Chat, $"Уведомления о погоде установлены на {time}");
                        return;

                    }

                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat, "Неверный формат времени. Пожалуйста, введите время в формате /settime ЧЧ:ММ");
                        return;


                    }
                }
                else if (message.Text == "Указать город")
                {
                    await botClient.SendTextMessageAsync(
                        message.Chat,
                        "Введите название города:"
                    );
                    return;
                }
                else if (message.Text == "Включить уведомления")
                {
                    isWeatherNotificationEnabled = true;
                    await botClient.SendTextMessageAsync(message.Chat, "Уведомления о погоде включены. Введите время уведомления в формате ЧЧ:ММ");
                    return;
                }
                else if (message.Text == "Выключить уведомления")
                {
                    isWeatherNotificationEnabled = false;
                    await botClient.SendTextMessageAsync(message.Chat, $"Уведомления о погоде о {time} выключены.");
                    return;
                }
                else if (isWeatherNotificationEnabled)
                {
                    time = message.Text;
                    await SetWeatherNotificationTimeAsync(botClient, message.Chat, time);
                    return;
                }

                else if (message.Text == "Погода на завтра")
                {
                    await SendWeatherTimeMenu(botClient, message.Chat);
                    return;
                }
                else if (message.Text == "Утро" || message.Text == "Обед" || message.Text == "Вечер" || message.Text == "Полночь" || message.Text == "Назад")
                {
                    if (message.Text != "Назад")
                        await GetWeatherForecastAsync(botClient, message.Chat, 1, message.Text);
                    else
                        await SendWeatherMenu(botClient, message.Chat);
                    return;
                }
                else if (message.Text == "Погода на 3 дня")
                {
                    await GetWeatherForecastAsync(botClient, message.Chat, 3, null);
                    return;
                }

                if (!string.IsNullOrEmpty(message.Text))
                {
                    cityName = message.Text;

                    string apiUrl = $"http://api.openweathermap.org/data/2.5/weather?q={cityName}&appid={weatherApiKey}&units=metric&lang=ru";

                    using (HttpClient client = new HttpClient())
                    {
                        try
                        {
                            HttpResponseMessage r = await client.GetAsync(apiUrl);
                            if (r.IsSuccessStatusCode)
                            {
                                string response = await r.Content.ReadAsStringAsync();
                                var json = JObject.Parse(response);
                                var temp = json["main"]["temp"].ToString();
                                var temp_feels_like = json["main"]["feels_like"].ToString();
                                var temp_min = json["main"]["temp_min"].ToString();
                                var temp_max = json["main"]["temp_max"].ToString();
                                var humidity = json["main"]["humidity"].ToString();
                                var wind_speed = json["wind"]["speed"].ToString();
                                var wind_deg = json["wind"]["deg"].ToString();
                                var weatherArray = json["weather"] as JArray;
                                var weather_description = weatherArray[0]["description"].ToString();

                                await botClient.SendTextMessageAsync(message.Chat, $"Погода в {cityName}:\n 🌡Температура сейчас: {temp}°\n 🤒Ощущается как {temp_feels_like}°\n 📉 Минимальная температура: {temp_min}°\n 📈Максимальная температура: {temp_max}°\n {GetEmoji(weather_description)}Облачность: {weather_description}\n 💧Влажность воздуха: {humidity}%\n 🌬Скорость ветра: {wind_speed} м/с\n 🕔Текущее время: {DateTime.Now}");
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(message.Chat, "Извините, я не понимаю эту команду.");
                                Console.WriteLine("error");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            return;
                        }
                    }
                }


            }
        }
        public static bool CheckTimeOfDay(JToken forecastItem, string timeOfDay)
        {
            // Получаем время из даты прогноза
            string dt_txt = forecastItem["dt_txt"].ToString();
            DateTime dateTime = DateTime.ParseExact(dt_txt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            switch (timeOfDay)
            {
                case "Утро":
                    return dateTime.Hour >= 6 && dateTime.Hour < 12;
                case "Обед":
                    return dateTime.Hour >= 12 && dateTime.Hour < 18;
                case "Вечер":
                    return dateTime.Hour >= 18 && dateTime.Hour < 24;
                case "Полночь":
                    return dateTime.Hour >= 0 && dateTime.Hour < 6;
                case "Назад":
                    return false;
                default:
                    return false;
            }
        }
        public static async Task SendWeatherTimeMenu(ITelegramBotClient botClient, Chat chat)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton("Утро") },
                new KeyboardButton[] { new KeyboardButton("Обед") },
                new KeyboardButton[] { new KeyboardButton("Вечер") },
                new KeyboardButton[] { new KeyboardButton("Полночь") },
                new KeyboardButton[] { new KeyboardButton("Назад") },
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendTextMessageAsync(
                chat,
                "Выберите время погоды на завтра:",
                replyMarkup: replyKeyboardMarkup
            );
        }
        static TimeSpan notificationTime;

        public static async Task WeatherNotificationThreadAsync()
        {
            while (true)
            {
                if (isWeatherNotificationEnabled)
                {
                    var currentTime = DateTime.Now.TimeOfDay;
                    if (currentTime > notificationTime && currentTime < notificationTime.Add(TimeSpan.FromMinutes(1)))
                    {
                        string apiUrl = $"http://api.openweathermap.org/data/2.5/weather?q={cityName}&appid={weatherApiKey}&units=metric&lang=ru";

                        using (HttpClient client = new HttpClient())
                        {

                            HttpResponseMessage r = await client.GetAsync(apiUrl);
                            if (r.IsSuccessStatusCode)
                            {
                                string response = await r.Content.ReadAsStringAsync();
                                var json = JObject.Parse(response);
                                var temp = json["main"]["temp"].ToString();
                                var temp_feels_like = json["main"]["feels_like"].ToString();
                                var temp_min = json["main"]["temp_min"].ToString();
                                var temp_max = json["main"]["temp_max"].ToString();
                                var humidity = json["main"]["humidity"].ToString();
                                var wind_speed = json["wind"]["speed"].ToString();
                                var wind_deg = json["wind"]["deg"].ToString();
                                var wind_gust = json["wind"]["gust"].ToString();
                                var weatherArray = json["weather"] as JArray;
                                var weather_description = weatherArray[0]["description"].ToString();

                                await SendWeatherNotification(bot, message.Chat, $"Погода в {cityName}:\n 🌡Температура сейчас: {temp}°\n 🤒Ощущается как {temp_feels_like}°\n 📉 Минимальная температура: {temp_min}°\n 📈Максимальная температура: {temp_max}°\n {GetEmoji(weather_description)}Облачность: {weather_description}\n 💧Влажность воздуха: {humidity}%\n 🌬Скорость ветра: {wind_speed} м/с\n 🕔Текущее время: {DateTime.Now}");
                            }
                        }
                        await Task.Delay(TimeSpan.FromHours(24));
                    }
                    else
                    {
                        // Засыпаем поток на 1 минуту
                        await Task.Delay(TimeSpan.FromMinutes(1));
                    }
                }
                else
                {
                    // Засыпаем поток на 1 минуту
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }
        public static async Task SetWeatherNotificationTimeAsync(ITelegramBotClient botClient, Chat chat, string time)
        {
            if (DateTime.TryParseExact(time, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime notificationTime))
            {
                isWeatherNotificationEnabled = true;
                Program.notificationTime = notificationTime.TimeOfDay;
                await botClient.SendTextMessageAsync(chat, $"Уведомления о погоде установлены на {time}");
            }
            else
            {
                await botClient.SendTextMessageAsync(chat, "Неверный формат времени. Пожалуйста, введите время в формате /settime ЧЧ:ММ");
                return;

            }
        }
        public static async Task SendWeatherNotification(ITelegramBotClient botClient, Chat chat, string weatherData)
        {
            await botClient.SendTextMessageAsync(chat, weatherData);
        }
        public static async Task SendWeatherMenu(ITelegramBotClient botClient, Chat chat)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton("Указать город") },
                new KeyboardButton[] { new KeyboardButton("Включить уведомления") },
                new KeyboardButton[] { new KeyboardButton("Выключить уведомления") },
                new KeyboardButton[] { new KeyboardButton("Погода на завтра") },
                new KeyboardButton[] { new KeyboardButton("Погода на 3 дня") }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };

            await botClient.SendTextMessageAsync(
                chat,
                "Выберите опцию:",
                replyMarkup: replyKeyboardMarkup
            );
        }
        static string GetEmoji(string weather_description)
        {
            string emoji = string.Empty;
            if (weather_description.Contains("несколько облаков") || weather_description.Contains("переменная облачность"))
            {
                emoji = "🌤";
            }
            else if (weather_description.Contains("ясно"))
            {
                emoji = "☀️";
            }
            else if (weather_description.Contains("облачно"))
            {
                emoji = "🌥";
            }
            else if (weather_description.Contains("дождь"))
            {
                emoji = "🌦";
            }
            return emoji;
        }
        public static async Task GetWeatherForecastAsync(ITelegramBotClient botClient, Chat chat, int days, string timeOfDay)
        {
            // URL API-сервиса погоды
            string apiUrl = $"http://api.openweathermap.org/data/2.5/forecast?q={cityName}&appid={weatherApiKey}&units=metric&lang=ru";

            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetStringAsync(apiUrl);
                var json = JObject.Parse(response);
                var forecastArray = json["list"] as JArray;
                string forecastMessage = string.Empty;

                // Получить прогноз погоды на указанное количество дней
                int forecastDays = Math.Min(days, forecastArray.Count);
                int index = (days == 1) ? 1 : 0;
                int to_index = (days == 1) ? 1 : 0;
                string c_deys = (days == 1) ? "день" : "дня";

                string forecastTitle = (forecastDays == 1) ? "на завтра" : $"на {forecastDays} {c_deys}";
                // Получить прогноз погоды на указанное количество дней
                for (int i = index; i < forecastDays + to_index; i++)
                {
                    var forecastItem = forecastArray[i * 8]; // Получить прогноз на каждые 3 часа
                    if (CheckTimeOfDay(forecastItem, timeOfDay) && timeOfDay != null)
                    {
                        var dt_txt = forecastItem["dt_txt"].ToString();
                        var weatherArray = forecastItem["weather"] as JArray;
                        var weather_description = weatherArray[0]["description"].ToString();
                        var temp = forecastItem["main"]["temp"].ToString();
                        var temp_min = forecastItem["main"]["temp_min"].ToString();
                        var temp_max = forecastItem["main"]["temp_max"].ToString();
                        var humidity = forecastItem["main"]["humidity"].ToString();
                        var wind_speed = forecastItem["wind"]["speed"].ToString();

                        forecastMessage += $" 📅 {dt_txt}\n 🌡Температура: {temp}°\n 📉 Минимальная температура: {temp_min}°\n 📈Максимальная температура: {temp_max}°\n {GetEmoji(weather_description)}Облачность: {weather_description}\n 💧Влажность воздуха: {humidity}%\n 🌬Скорость ветра: {wind_speed} м/с\n\n";
                        await botClient.SendTextMessageAsync(chat, $"Прогноз погоды {forecastTitle} в {cityName} {timeOfDay}:\n\n{forecastMessage}");
                        return;
                    }
                    else
                    {
                        var dt_txt = forecastItem["dt_txt"].ToString();
                        var weatherArray = forecastItem["weather"] as JArray;
                        var weather_description = weatherArray[0]["description"].ToString();
                        var temp = forecastItem["main"]["temp"].ToString();
                        var temp_min = forecastItem["main"]["temp_min"].ToString();
                        var temp_max = forecastItem["main"]["temp_max"].ToString();
                        var humidity = forecastItem["main"]["humidity"].ToString();
                        var wind_speed = forecastItem["wind"]["speed"].ToString();

                        forecastMessage += $" 📅 {dt_txt}\n 🌡Температура: {temp}°\n 📉 Минимальная температура: {temp_min}°\n 📈Максимальная температура: {temp_max}°\n {GetEmoji(weather_description)}Облачность: {weather_description}\n 💧Влажность воздуха: {humidity}%\n 🌬Скорость ветра: {wind_speed} м/с\n\n";
                    }
                }
                if (timeOfDay == null)
                {
                    await botClient.SendTextMessageAsync(chat, $"Прогноз погоды {forecastTitle} в {cityName}:\n\n{forecastMessage}");
                    return;
                }
                await botClient.SendTextMessageAsync(chat, $"К сожалению, прогноз погоды на {timeOfDay.ToLower()} в {cityName} отсутствует.");
            }
        }

        public static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(exception));
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Запущен бот " + bot.GetMeAsync().Result.FirstName);

            weatherNotificationThread = new Thread(async () => await WeatherNotificationThreadAsync());
            weatherNotificationThread.Start();

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // receive all update types
            };

            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );

            Console.ReadLine();
        }
    }
}
