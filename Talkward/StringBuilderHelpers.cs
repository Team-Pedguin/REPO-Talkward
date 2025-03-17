using System.Text;

namespace Talkward;

public static class StringBuilderHelpers
{
    public static StringBuilder TrimEnd(this StringBuilder sb, char c)
    {
        if (sb.Length == 0)
            return sb;

        while (sb.Length > 0 && sb[^1] == c)
            sb.Remove(sb.Length - 1, 1);

        return sb;
    }

    public static StringBuilder TrimEnd(this StringBuilder sb)
    {
        if (sb.Length == 0)
            return sb;

        while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
            sb.Remove(sb.Length - 1, 1);

        return sb;
    }
    
    public static StringBuilder TrimStart(this StringBuilder sb, char c)
    {
        if (sb.Length == 0)
            return sb;

        while (sb.Length > 0 && sb[0] == c)
            sb.Remove(0, 1);

        return sb;
    }
    
    public static StringBuilder TrimStart(this StringBuilder sb)
    {
        if (sb.Length == 0)
            return sb;

        while (sb.Length > 0 && char.IsWhiteSpace(sb[0]))
            sb.Remove(0, 1);

        return sb;
    }
    
    public static StringBuilder Trim(this StringBuilder sb, char c)
    {
        if (sb.Length == 0)
            return sb;

        while (sb.Length > 0 && sb[^1] == c)
            sb.Remove(sb.Length - 1, 1);

        while (sb.Length > 0 && sb[0] == c)
            sb.Remove(0, 1);

        return sb;
    }
    
    public static StringBuilder Trim(this StringBuilder sb)
    {
        if (sb.Length == 0)
            return sb;

        while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
            sb.Remove(sb.Length - 1, 1);

        while (sb.Length > 0 && char.IsWhiteSpace(sb[0]))
            sb.Remove(0, 1);

        return sb;
    }
}