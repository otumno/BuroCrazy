using UnityEngine;

public static class DocumentTitleGenerator
{
    private static string[] prefixes = { "Приказ №", "Распоряжение №", "Циркуляр №", "Директива №", "Служебная записка №" };
    private static string[] subjects = { "о немедленном", "касательно внесения изменений в", "об упразднении", "о тотальном запрете", "касательно регламентации" };
    private static string[] objects = { "департамента по учёту скрепок", "отдела по борьбе с хорошим настроением", "процедуры чаепития", "использования красных чернил", "комитета по надзору за кактусами" };
    private static string[] reasons = { "в связи с ретроградным Меркурием", "согласно параграфу 7-БИС", "во избежание временных парадоксов", "по причине неустановленного происхождения", "в рамках оптимизации всего" };

    public static string GenerateTitle()
    {
        string prefix = prefixes[Random.Range(0, prefixes.Length)] + Random.Range(100, 999);
        string subject = subjects[Random.Range(0, subjects.Length)];
        string obj = objects[Random.Range(0, objects.Length)];
        string reason = reasons[Random.Range(0, reasons.Length)];

        return $"{prefix}\n{subject} {obj}\n{reason}";
    }
}