namespace Aives.Application.Viva;

/// <summary>Picks the TTS language for a question: Vietnamese if it has Vietnamese letters, otherwise English.</summary>
public static class SpeechLanguage
{
    public const string Vietnamese = "vi-VN";
    public const string English = "en-US";

    // Letters with Vietnamese diacritics (both cases). Plain ASCII text reads as English.
    private const string VietnameseLetters =
        "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ" +
        "ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ";

    public static string Detect(string? text)
        => !string.IsNullOrEmpty(text) && text.IndexOfAny(VietnameseLetters.ToCharArray()) >= 0
            ? Vietnamese
            : English;
}
