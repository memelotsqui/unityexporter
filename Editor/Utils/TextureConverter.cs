using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureConverter
{
    public static Texture2D CreateFromColor(Color color, int sizeX, int sizeY, bool convertToLinear = false)
    {
        Texture2D result = new Texture2D(sizeX, sizeY);
        if (convertToLinear)
            color = color.linear;
        Color[] pixels = new Color[sizeX * sizeY];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        result.SetPixels(pixels);
        result.Apply();

        return result;
    }
}
