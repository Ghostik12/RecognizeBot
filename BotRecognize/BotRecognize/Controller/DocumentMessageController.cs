using Telegram.Bot.Types;
using Telegram.Bot;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using System.Text.RegularExpressions;
using System.Globalization;
using NPOI.SS.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotRecognize.Controller
{
    public class DocumentMessageController
    {
        private ITelegramBotClient _client;
        public DocumentMessageController(ITelegramBotClient client)
        {
            _client = client;
        }

        internal async Task Handle(Update update, CancellationToken cancellationToken)
        {
            var user = update.Message.From.Id; // Ответственный за оплату счёта

            if (update.Message.Document != null)
            {
                var file = await _client.GetFileAsync(update.Message.Document.FileId);
                var filePath = $"{update.Message.Document.FileName}";

                // Скачиваем файл
                using (var saveStream = File.Open(filePath, FileMode.Create))
                {
                    await _client.DownloadFile(file.FilePath, saveStream);
                }

                string text = "";

                // Обработка PDF
                if (update.Message.Document.MimeType == "application/pdf")
                {
                    text = ExtractTextFromPdf(filePath);
                }
                // Обработка Excel
                else if (update.Message.Document.MimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                         update.Message.Document.MimeType == "application/vnd.ms-excel")
                {
                    text = ExtractTextFromExcel(filePath);
                }

                Console.WriteLine(text);

                // Распознаем данные
                var data = ExtractDataFromText(text);

                // Формируем ответ
                var response = $"Номер счета: {data.InvoiceNumber}\n" +
                               $"Дата: {data.InvoiceDate.ToShortDateString()}\n" +
                               $"Сумма: {data.Amount}\n" +
                               $"Получатель: {data.Recipient}\n" +
                               $"Назначение платежа: {data.Purpose}";

                // Отправляем ответ пользователю
                await _client.SendTextMessageAsync(update.Message.Chat.Id, response);

                // Удаляем временный файл
                File.Delete(filePath);
            }
        }

        private static async Task HandleEditRequest(ITelegramBotClient client, long chatId, string response, (string InvoiceNumber, DateTime InvoiceDate, decimal Amount, string Recipient, string Purpose) data)
        {
            // Отправляем распознанные данные
            await client.SendTextMessageAsync(chatId, response);

            // Предлагаем редактирование
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("Да"),
                new KeyboardButton("Нет")
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await client.SendTextMessageAsync(chatId, "Хотите отредактировать данные?", replyMarkup: keyboard);

            // Ожидаем ответа пользователя
            var editResponse = await WaitForUserResponse(client, chatId);

            if (editResponse == "Да")
            {
                // Предлагаем выбрать поле для редактирования
                var fieldsKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton("Номер счета"),
                    new KeyboardButton("Дата"),
                    new KeyboardButton("Сумма"),
                    new KeyboardButton("Получатель"),
                    new KeyboardButton("Назначение платежа")
                })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                };

                await client.SendTextMessageAsync(chatId, "Что вы хотите отредактировать?", replyMarkup: fieldsKeyboard);

                // Ожидаем выбора поля
                var fieldToEdit = await WaitForUserResponse(client, chatId);

                // Запрашиваем новое значение
                await client.SendTextMessageAsync(chatId, $"Введите новое значение для {fieldToEdit}:", replyMarkup: new ReplyKeyboardRemove());

                var newValue = await WaitForUserResponse(client, chatId);

                // Обновляем данные
                switch (fieldToEdit)
                {
                    case "Номер счета":
                        data.InvoiceNumber = newValue;
                        break;
                    case "Дата":
                        if (DateTime.TryParse(newValue, out var newDate))
                        {
                            data.InvoiceDate = newDate;
                        }
                        break;
                    case "Сумма":
                        if (decimal.TryParse(newValue, out var newAmount))
                        {
                            data.Amount = newAmount;
                        }
                        break;
                    case "Получатель":
                        data.Recipient = newValue;
                        break;
                    case "Назначение платежа":
                        data.Purpose = newValue;
                        break;
                }

                // Отправляем обновленные данные
                var updatedResponse = $"Номер счета: {data.InvoiceNumber}\n" +
                                      $"Дата: {data.InvoiceDate.ToShortDateString()}\n" +
                                      $"Сумма: {data.Amount}\n" +
                                      $"Получатель: {data.Recipient}\n" +
                                      $"Назначение платежа: {data.Purpose}";

                await client.SendTextMessageAsync(chatId, updatedResponse);

                // Снова предлагаем редактирование
                await HandleEditRequest(client, chatId, updatedResponse, data);
            }
            else
            {
                // Переходим к выбору проекта
                await HandleProjectSelection(client, chatId, data);
            }
        }

        private static async Task<string> WaitForUserResponse(ITelegramBotClient client, long chatId)
        {
            var tcs = new TaskCompletionSource<string>();
            EventHandler<Update> handler = null;

            handler = async (s, e) =>
            {
                if (e.Message.Chat.Id == chatId)
                {
                    tcs.SetResult(e.Message.Text);
                    client.OnUpdate -= handler;
                }
            };

            client.OnUpdate += handler;
            return await tcs.Task;
        }

        private static string ExtractTextFromExcel(string filePath)
        {
            var text = new System.Text.StringBuilder();
            IWorkbook workbook;

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Определяем формат файла (XLS или XLSX)
                if (Path.GetExtension(filePath).Equals(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook(stream); // Для XLS
                }
                else
                {
                    workbook = new XSSFWorkbook(stream); // Для XLSX
                }

                // Чтение данных из всех листов
                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    var sheet = workbook.GetSheetAt(i);
                    for (int row = 0; row <= sheet.LastRowNum; row++)
                    {
                        var currentRow = sheet.GetRow(row);
                        if (currentRow != null)
                        {
                            for (int col = 0; col < currentRow.LastCellNum; col++)
                            {
                                var cell = currentRow.GetCell(col);
                                if (cell != null)
                                {
                                    text.Append(cell.ToString()).Append(" ");
                                }
                            }
                            text.AppendLine();
                        }
                    }
                }
            }

            return text.ToString();
        }

        private static string ExtractTextFromPdf(string filePath)
        {
            var text = new System.Text.StringBuilder();
            using (var reader = new PdfReader(filePath))
            {
                var pdfDocument = new PdfDocument(reader);
                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    text.Append(PdfTextExtractor.GetTextFromPage(page));
                }
            }
            return text.ToString();
        }

        private static (string InvoiceNumber, DateTime InvoiceDate, decimal Amount, string Recipient, string Purpose) ExtractDataFromText(string text)
        {
            var (invoiceNumber, invoiceDate) = ExtractInvoiceNumberAndDate(text);
            var amount = ExtractAmount(text);
            var recipient = ExtractRecipient(text);
            var purpose = ExtractPurpose(text, invoiceNumber, invoiceDate);

            return (invoiceNumber, invoiceDate, amount, recipient, purpose);
        }

        private static (string InvoiceNumber, DateTime InvoiceDate) ExtractInvoiceNumberAndDate(string text)
        {
            // Поиск номера счета и даты в формате "Счет №25-02441157151 от 01.01.2025"
            var match = Regex.Match(text, @"Счет\s*(?:на оплату)?\s*№\s*([\d-]+)\s*от\s*(\d{2}\.\d{2}\.\d{4})");
            if (match.Success && DateTime.TryParse(match.Groups[2].Value, out var date))
            {
                return (match.Groups[1].Value, date);
            }

            // Поиск номера счета и даты в формате "Счет №12345 от 11 февраля 2025"
            match = Regex.Match(text, @"Счет\s*(?:на оплату)?\s*№\s*([\d-]+)\s*от\s*(\d{1,2}\s+[а-я]+\s+\d{4})");
            if (match.Success && DateTime.TryParse(match.Groups[2].Value, out date))
            {
                return (match.Groups[1].Value, date);
            }

            // Поиск номера счета и даты в формате "№25-02441157151 от 01.01.2025"
            match = Regex.Match(text, @"№\s*([\d-]+)\s*от\s*(\d{2}\.\d{2}\.\d{4})");
            if (match.Success && DateTime.TryParse(match.Groups[2].Value, out date))
            {
                return (match.Groups[1].Value, date);
            }

            // Поиск номера счета и даты в формате "№12345 от 11 февраля 2025"
            match = Regex.Match(text, @"№\s*([\d-]+)\s*от\s*(\d{1,2}\s+[а-я]+\s+\d{4})");
            if (match.Success && DateTime.TryParse(match.Groups[2].Value, out date))
            {
                return (match.Groups[1].Value, date);
            }

            return (null, DateTime.MinValue); // Если данные не найдены
        }

        private static decimal ExtractAmount(string text)
        {
            // Поиск суммы в числовом формате с ключевым словом "на сумму"
            var match = Regex.Match(text, @"на сумму:\s+(\d{1,3}(?:\s?\d{3})*(?:[.,]\d{2})?)\s+руб\.");
            if (!match.Success)
            {
                // Поиск суммы в числовом формате с ключевым словом "на сумма" (без двоеточия)
                match = Regex.Match(text, @"на сумму\s+(\d{1,3}(?:\s?\d{3})*(?:[.,]\d{2})?)\s+руб\.");
            }
            if (!match.Success)
            {
                // Поиск суммы в числовом формате с ключевым словом "Всего оказано услуг на сумму:"
                match = Regex.Match(text, @"Всего оказано услуг на сумму:\s+(\d{1,3}(?:\s?\d{3})*(?:[.,]\d{2})?)");
            }

            if (match.Success)
            {
                var amountStr = match.Groups[1].Value;

                // Очищаем строку от пробелов и заменяем запятую на точку
                amountStr = amountStr.Replace(" ", "").Replace(",", ".");

                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    return amount;
                }
            }

            // Поиск суммы в письменном формате с ключевым словом "на сумма"
            match = Regex.Match(text, @"на сумму:\s+([а-я]+\s+[а-я]+\s+\d+\s+копеек)");
            if (!match.Success)
            {
                // Поиск суммы в письменном формате с ключевым словом "Всего оказано услуг на сумму:"
                match = Regex.Match(text, @"Всего оказано услуг на сумму:\s+([а-я]+\s+[а-я]+\s+\d+\s+копеек,)");
            }
            if (!match.Success)
            {
                // Поиск суммы в сокращенном письменном формате (например, "на сумма: Девятьсот")
                match = Regex.Match(text, @"на сумму:\s+([а-я]+)");
            }

            if (match.Success)
            {
                var amountStr = match.Groups[1].Value;
                var amountInNumbers = ConvertTextAmountToNumber(amountStr); // Конвертируем текст в число
                return amountInNumbers;
            }

            // Поиск суммы в числовом формате с ключевым словом "Итого:" (в крайнем случае)
            match = Regex.Match(text, @"Итого:\s+(\d{1,3}(?:\s?\d{3})*(?:[.,]\d{2})?)");
            if (match.Success)
            {
                var amountStr = match.Groups[1].Value;

                // Очищаем строку от пробелов и заменяем запятую на точку
                amountStr = amountStr.Replace(" ", "").Replace(",", ".");

                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    return amount;
                }
            }

            return 0; // Если сумма не найдена
        }

        private static decimal ConvertTextAmountToNumber(string textAmount)
        {
            var numberWords = new Dictionary<string, int>
    {
        {"ноль", 0}, {"один", 1}, {"одна", 1}, {"два", 2}, {"две", 2}, {"три", 3}, {"четыре", 4},
        {"пять", 5}, {"шесть", 6}, {"семь", 7}, {"восемь", 8}, {"девять", 9},
        {"десять", 10}, {"одиннадцать", 11}, {"двенадцать", 12}, {"тринадцать", 13},
        {"четырнадцать", 14}, {"пятнадцать", 15}, {"шестнадцать", 16}, {"семнадцать", 17},
        {"восемнадцать", 18}, {"девятнадцать", 19}, {"двадцать", 20}, {"тридцать", 30},
        {"сорок", 40}, {"пятьдесят", 50}, {"шестьдесят", 60}, {"семьдесят", 70},
        {"восемьдесят", 80}, {"девяносто", 90}, {"сто", 100}, {"двести", 200},
        {"триста", 300}, {"четыреста", 400}, {"пятьсот", 500}, {"шестьсот", 600},
        {"семьсот", 700}, {"восемьсот", 800}, {"девятьсот", 900}, {"тысяча", 1000},
        {"тысячи", 1000}, {"тысяч", 1000}, {"миллион", 1000000}, {"миллиона", 1000000},
        {"миллионов", 1000000}
    };

            var words = textAmount.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            decimal total = 0;
            decimal current = 0;

            foreach (var word in words)
            {
                if (numberWords.ContainsKey(word))
                {
                    current += numberWords[word];
                }
                else if (word == "рублей" || word == "рубля" || word == "рубль")
                {
                    total += current;
                    current = 0;
                }
                else if (word == "копеек" || word == "копейки" || word == "копейка")
                {
                    total += current / 100;
                    current = 0;
                }
            }

            return total;
        }

        private static string ExtractRecipient(string text)
        {
            var match = Regex.Match(text, @"(ООО|ОАО|ЗАО|ИП)\s*[«""]?([^»""]+)");
            return match.Success ? $"{match.Groups[1].Value} {match.Groups[2].Value}" : null;
        }

        private static string ExtractPurpose(string text, string invoiceNumber, DateTime invoiceDate)
        {
            // Паттерны для поиска назначения платежа
            var patterns = new[]
            {
                @"ВАЖНО! В ЦЕЛЯХ ИСКЛЮЧЕНИЯ ОШИБОК ПРИ ПРОВЕДЕНИИ ОПЛАТ ПРОСИМ В ПЛАТЕЖНЫХ ПОРУЧЕНИЯХ В НАЗНАЧЕНИИ ПЛАТЕЖА УКАЗЫВАТЬ:\s*(.+)",
                @"В НАЗНАЧЕНИИ ПЛАТЕЖА УКАЗЫВАТЬ:\s*(.+)",
                @"Назначение платежа:\s*(.+)",
                @"Основание платежа:\s*(.+)",
                @"Комментарий:\s*(.+)",
                @"Описание:\s*(.+)",
                @"Примечание:\s*(.+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    // Возвращаем найденное назначение платежа
                    return match.Groups[1].Value.Trim();
                }
            }

            // Если назначение платежа не найдено, возвращаем текст по умолчанию
            return $"Оплата счета №{invoiceNumber} от {invoiceDate.ToShortDateString()}";
        }
    }
}
