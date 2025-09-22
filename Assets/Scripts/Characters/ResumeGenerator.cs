// Файл: ResumeGenerator.cs
using UnityEngine;
using System.Text;

public static class ResumeGenerator
{
    private static string[] educations = {
        "Высшая Школа Канцелярских Наук",
        "Институт Бюрократических Искусств",
        "Университет Перекладывания Бумаг им. Архивариуса",
        "Академия Управления Степплерами",
        "Колледж Прикладной Печати и Подписи"
    };

    private static string[] hobbies = {
        "разведение кактусов на подоконнике",
        "скоростное сшивание документов",
        "коллекционирование редких скрепок",
        "медитация под гул принтера",
        "составление диаграмм в Excel о смысле жизни"
    };

    private static string[] quirks = {
        "Считает, что принтер - это портал в другое измерение.",
        "Убежден, что комнатный фикус - его прямой начальник.",
        "Разговаривает с кулером для воды, когда думает, что его никто не видит.",
        "Верит, что тройная копия документа обладает магической силой.",
        "Подозревает, что в подвале живет ручной архиватор."
    };

    public static string GenerateBio()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b>Образование:</b> {educations[Random.Range(0, educations.Length)]}.");
        sb.AppendLine($"<b>Хобби:</b> {hobbies[Random.Range(0, hobbies.Length)]}.");
        sb.Append($"<b>О себе:</b> {quirks[Random.Range(0, quirks.Length)]}");

        return sb.ToString();
    }
}