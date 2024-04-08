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
      // Extract the components. Assuming each component is 16 bits,
      // and the color is stored in ARGB order.
      byte a = (byte)((colorValue >> 48) & 0xFFFF); // Extract the alpha component and downscale it
      byte r = (byte)((colorValue >> 32) & 0xFFFF); // Extract the red component and downscale it
      byte g = (byte)((colorValue >> 16) & 0xFFFF); // Extract the green component and downscale it
      byte b = (byte)(colorValue & 0xFFFF);         // Extract the blue component and downscale it

      // Assuming the color components were in 16-bit and you need to downscale to 8-bit
      // You can achieve this by taking the high byte of each component directly,
      // assuming the color information is mostly in the higher bits.
      // This simple method loses some precision but is a common approach.
      // If your components are already 8 bits, you can directly use them without bit shifting.

      return new Color(a, r, g, b);
    }
  }
}
