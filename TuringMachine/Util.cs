using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TuringMachine
{
    internal static class Util
    {
        public static bool match(this string inp, string reg, out Match m, RegexOptions opt = RegexOptions.IgnoreCase | RegexOptions.Compiled) => (m = Regex.Match(inp, reg, opt)).Success;
    }
}
