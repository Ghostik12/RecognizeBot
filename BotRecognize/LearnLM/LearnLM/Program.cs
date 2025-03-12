using Microsoft.ML;
using Microsoft.ML.Data;

namespace LearnLM
{
    class Program
    {
        static void Main(string[] args)
        {
            // Шаг 1: Создаем контекст ML.NET
            var mlContext = new MLContext();

            // Шаг 2: Указываем путь к файлу
            var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "data.csv");

            // Выводим текущую рабочую директорию
            Console.WriteLine("Текущая рабочая директория: " + Directory.GetCurrentDirectory());

            // Проверяем, существует ли файл
            if (!File.Exists(dataPath))
            {
                Console.WriteLine($"Файл не найден: {dataPath}");
                return;
            }

            // Шаг 3: Загружаем данные
            var data = mlContext.Data.LoadFromTextFile<DocumentData>(
                path: dataPath,
                separatorChar: ',',
                hasHeader: true
            );

            // Шаг 4: Разделяем данные на обучающую и тестовую выборки
            var splitData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            // Шаг 5: Создаем pipeline (цепочку преобразований)
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(mlContext.Transforms.Text.FeaturizeText("Features", nameof(DocumentData.Text)))
                .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // Шаг 6: Обучаем модель
            var model = pipeline.Fit(splitData.TrainSet);

            // Шаг 7: Оцениваем модель на тестовой выборке
            var predictions = model.Transform(splitData.TestSet);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions);

            Console.WriteLine($"Точность модели: {metrics.MacroAccuracy:P2}");

            // Шаг 8: Сохраняем модель
            mlContext.Model.Save(model, splitData.TrainSet.Schema, "model.zip");

            Console.WriteLine("Модель обучена и сохранена в model.zip");
        }
    }

    // Класс для хранения данных
    public class DocumentData
    {
        [LoadColumn(0)]
        public string Text { get; set; }

        [LoadColumn(1)]
        public string Label { get; set; }
    }
}
