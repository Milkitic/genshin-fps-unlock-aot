using System;
using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace UnlockFps.Gui;

public static class AppBuilderExtensions
{
    public static AppBuilder WithNativeFonts(this AppBuilder appBuilder, string? specificFontFamily = null)
    {
        string? familyName = null;

        if (specificFontFamily != null)
        {
            var family = SKFontManager.Default.MatchFamily(specificFontFamily);
            familyName = family?.FamilyName;
        }

        FontManagerOptions options = new();

        if (familyName == null)
        {
            familyName = SKFontManager.Default.MatchCharacter('a')?.FamilyName;
            if (familyName == null)
            {
                Console.Error.WriteLine("Cannot find default font.");
            }
        }

        if (familyName != null)
        {
            options.DefaultFamilyName = familyName;

            var fontFallbacks = new FontFallback[]
            {
                new() { FontFamily = FontFamily.Parse(familyName) },
            };
            options.FontFallbacks = fontFallbacks;
        }

        return appBuilder.With(options);
    }
}