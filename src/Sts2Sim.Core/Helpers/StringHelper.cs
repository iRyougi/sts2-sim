using System.IO.Hashing;
using System.Text;
using System.Text.RegularExpressions;

namespace Sts2Sim.Core.Helpers;

public static partial class StringHelper
{
    [GeneratedRegex("([A-Za-z0-9]|\\G(?!^))([A-Z])")]
    private static partial Regex CamelCaseRegex();

    public static string SnakeCase(string txt)
    {
        return CamelCaseRegex().Replace(txt.Trim(), "$1_$2").ToLowerInvariant();
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("[^A-Z0-9_]")]
    private static partial Regex SpecialCharRegex();

    public static string Slugify(string txt)
    {
        string text = CamelCaseRegex().Replace(txt.Trim(), "$1_$2");
        string input = WhitespaceRegex().Replace(text.ToUpperInvariant(), "_");
        return SpecialCharRegex().Replace(input, "");
    }

    /// <summary>
    /// 旧版 31 位哈希，逐字移植自游戏 MegaCrit.Sts2.Core.Helpers.StringHelper（Plan 01 基线）。
    /// v0.109 起游戏本体不再用它计算种子，仅用于 <c>RunRngSet</c> 的 "old" 前缀种子兼容路径。
    /// 新代码一律用无后缀的 <see cref="GetDeterministicHashCode(string)"/>。
    /// 注意：不能用 string.GetHashCode()（.NET Core 每进程随机化）。
    /// </summary>
    public static int GetDeterministicHashCodeOld(string str)
    {
        int num = 352654597;
        int num2 = num;
        for (int i = 0; i < str.Length; i += 2)
        {
            num = ((num << 5) + num) ^ str[i];
            if (i == str.Length - 1)
            {
                break;
            }
            num2 = ((num2 << 5) + num2) ^ str[i + 1];
        }
        return num + num2 * 1566083941;
    }

    [ThreadStatic]
    private static byte[]? _stringHashCache;

    /// <summary>
    /// v0.109 哈希：XxHash64（UTF8, seed 0），逐字移植自游戏 MegaCrit.Sts2.Core.Helpers.StringHelper。
    /// </summary>
    public static ulong GetDeterministicHashCode(string str)
    {
        if (_stringHashCache == null)
        {
            _stringHashCache = new byte[1024];
        }
        int byteCount = Encoding.UTF8.GetByteCount(str);
        if (byteCount > _stringHashCache.Length)
        {
            _stringHashCache = new byte[(int)Math.Round((double)byteCount * 1.5)];
        }
        byteCount = Encoding.UTF8.GetBytes(str, _stringHashCache);
        return XxHash64.HashToUInt64(_stringHashCache.AsSpan().Slice(0, byteCount), 0L);
    }
}
