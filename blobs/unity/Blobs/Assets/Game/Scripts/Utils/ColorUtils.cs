using System;
using UnityEngine;

namespace Game.Scripts.Utils
{
  public class ColorUtils
  {
    public static ulong RGBToUlong(int red, int green, int blue, int alpha)
      {
        // Ensure input values are within the 0 - 65535 range
        red = Math.Clamp(red, 0, 65535);
        green = Math.Clamp(green, 0, 65535);
        blue = Math.Clamp(blue, 0, 65535);
        alpha = Math.Clamp(alpha, 0, 65535);

        // Calculate the shift amounts by multiplying with 2 to the power of respective shifts
        // and combining the shifted values and alpha into a single ulong value
        ulong colorValue = ((ulong)red << 48) | ((ulong)green << 32) | ((ulong)blue << 16) | (ulong)alpha;

        return colorValue;
      }

    public static Color UlongToColor(ulong colorValue)
    {
      byte r = (byte)((colorValue >> 56) & 0xFF); // Shift right by 56 bits then mask the lowest 8 bits
      byte g = (byte)((colorValue >> 40) & 0xFF); // Shift right by 40 bits then mask the lowest 8 bits
      byte b = (byte)((colorValue >> 24) & 0xFF); // Shift right by 24 bits then mask the lowest 8 bits
      byte a = (byte)((colorValue >> 8) & 0xFF);  // Shift right by 8 bits then mask the lowest 8 bits

      return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
  }
}
