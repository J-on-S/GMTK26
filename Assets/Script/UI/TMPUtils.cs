using TMPro;
using UnityEngine;

public static class TMPUtils {
    // Wraps a string with a color tag
    public static string ColorString(string text, Color color) {
        // Convert Color to hex
        string hex = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{hex}>{text}</color>";
    }
    public static string MakeBold(string text) {
        return $"<b>{text}</b>";
    }

    // Makes a string italic
    public static string MakeItalic(string text) {
        return $"<i>{text}</i>";
    }
}