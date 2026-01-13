using UnityEngine;

public enum Language
{
    French,
    English
}

public class DialogueLanguage : MonoBehaviour
{
    public static Language CurrentLanguage = Language.French;

    public static string LanguageToCSVColumn(Language lang)
    {
        switch (lang)
        {
            case Language.French: return "fr";
            case Language.English: return "en";
            default: return "fr"; 
        }
    }
}