using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System;
using System.IO;

namespace TuringMachine
{
    public sealed class StringTuringMachineConfiguration
        : TuringMachineConfiguration<ulong, char>
    {
        private readonly List<char> charset = new List<char>();
        private string initmem;
        private char @default;


        public object InitialMemoryLayout => initmem;

        public StringTuringMachineConfiguration(params char[] charset)
            : this(charset as IEnumerable<char>)
        {
        }

        public StringTuringMachineConfiguration(IEnumerable<char> charset) => this.charset.AddRange(charset ?? new char[0]);

        public DeterministicTuringMachine<char> CreateDTM(bool allowundefined)
        {
            DeterministicTuringMachine<char> dtm = new DeterministicTuringMachine<char>(this, @default, allowundefined);

            if (initmem != null)
                dtm.Initialize(initmem.ToCharArray());

            return dtm;
        }

        public string GenerateReadableConfiguration()
        {
            char astr(TuringTransition<ulong, char> t) => t.Action == TuringAction.MoveLeft ? 'L' : t.Action == TuringAction.MoveRight ? 'R' : '-';
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"%charset {string.Join(" ", charset)}");
            sb.AppendLine($"%memory {initmem}");
            sb.AppendLine($"%blank {@default}");

            foreach (TuringState<ulong, char> s in States)
                sb.AppendLine($"{(s.ID == StartStateIndex ? "> " : "")}{s.ID}{(s.Accpetance == TuringAcceptance.Accept ? " A" : s.Accpetance == TuringAcceptance.Reject ? " R" : "")}");

            foreach (var t in from s in States
                              from t in s.Transitions
                              select new { From = s.ID, Transition = t })
                sb.AppendLine($"{t.From} {new string(t.Transition.InputSymbols)} -> {t.Transition.OutputSymbol} {astr(t.Transition)} {t.Transition.TargetID}");

            return sb.ToString();
        }
        
        public new byte[] ToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                byte[] b = base.ToBytes();

                wr.Write(b.Length);
                wr.Write(b);
                wr.Write(@default);
                wr.Write(initmem ?? "");
                wr.Write(new string(charset.ToArray()));

                return ms.ToArray();
            }
        }


        public static new StringTuringMachineConfiguration FromBytes(byte[] arr)
        {
            using (MemoryStream ms = new MemoryStream(arr))
            using (BinaryReader rd = new BinaryReader(ms))
            {
                StringTuringMachineConfiguration conf = new StringTuringMachineConfiguration();
                byte[] b = rd.ReadBytes(rd.ReadInt32());

                conf.@default = rd.ReadChar();
                conf.initmem = rd.ReadString();
                conf.charset.AddRange(rd.ReadString());

                return conf;
            }
        }

        public static StringTuringMachineConfiguration FromReadableConfiguration(string str)
        {
            StringTuringMachineConfiguration conf = new StringTuringMachineConfiguration { initmem = null };
            int lnr = 0;
            Match m;

            string g(string n) => m.Groups[n].ToString();
            string err(string msg) => throw new Exception(msg);


            foreach (string __l in str.Replace('\r', ' ').Split('\n'))
            {
                string line = __l.Trim();

                ++lnr;

                if (line.match(@"^(\;|\/\/|\-\-).*$", out _))
                    continue;
                else if (line.match(@"^\%charset(?<set>(\s+[^\s])+)$", out m))
                    foreach (string chr in g("set").Split(' '))
                    {
                        if (chr.Length == 0)
                            continue;

                        char c = chr.Trim()[0];

                        if (!conf.charset.Contains(c))
                            conf.charset.Add(c);
                    }
                else if (line.match(@"^\%blank\s+(?<def>[^\s])$", out m))
                {
                    if (!conf.charset.Contains(conf.@default = g("def").Trim()[0]))
                        err($"The character '{conf.@default}' must be in the charset before being set as blank symbol.");
                }
                else if (line.match(@"^\%memory\s+(?<mem>.+)$", out m))
                {
                    string s = g("mem");
                    var nex = s.Distinct().Except(conf.charset);

                    if (nex.Any())
                        err($"The character(s) '{string.Join("', '", nex)}' must be in the charset before being added.");
                    else
                        conf.initmem = s;
                }
                else if (line.match(@"^(?<init>\>\s*)?(?<id>[0-9]+)\s*(?<acc>[AR])?$", out m))
                {
                    bool init = g("init").Trim().Any();
                    char acc = (g("acc").Trim().ToLower() + '-')[0];
                    ulong id;

                    conf.AddState(new TuringState<ulong, char>(id = ulong.Parse(m.Groups["id"].ToString()), acc == 'a' ? TuringAcceptance.Accept : acc == 'r' ? TuringAcceptance.Reject : TuringAcceptance.None));

                    if (init)
                        conf.StartStateIndex = id;
                }
                else if (line.match(@"^(?<fid>[0-9]+)\s+(?<inp>[^\s]+)\s*\->\s*(?<outp>[^\s])\s+(?<act>[rl-])\s+(?<tid>[0-9]+)$", out m))
                {
                    ulong fid = ulong.Parse(g("fid"));
                    ulong tid = ulong.Parse(g("tid"));
                    char[] inp = g("inp").ToCharArray();
                    char outp = g("outp")[0];
                    char act = g("act").ToLower()[0];

                    var unknown = inp.Union(new[] { outp }).Except(conf.charset);

                    if (unknown.Any())
                        err($"The character(s) '{string.Join("', '", unknown)}' must be in the charset before being added.");

                    conf.AddTransition(fid, tid, act == 'r' ? TuringAction.MoveRight : act == 'l' ? TuringAction.MoveLeft : TuringAction.None, outp, inp);
                }
                else if (line.Any())
                    err($"Unable to parse line no. {lnr}:  {line}");
            }

            return conf;
        }
    }
}
