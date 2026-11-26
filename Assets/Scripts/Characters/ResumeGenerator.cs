using UnityEngine;

[System.Serializable]
public class ResumeBio
{
    public int education;
    public int hobby;
    public int quirk;

    public string Localize() => UI.GameText.Get("hiring.bio",
        UI.GameText.Get($"hiring.bio.educations.{education}"),
        UI.GameText.Get($"hiring.bio.hobbies.{hobby}"),
        UI.GameText.Get($"hiring.bio.quirks.{quirk}"));
}

public static class ResumeGenerator
{
    public static ResumeBio Generate() => new ResumeBio
    {
        education = Random.Range(0, 5),
        hobby = Random.Range(0, 5),
        quirk = Random.Range(0, 5)
    };

    public static string GenerateBio() => Generate().Localize();
}
