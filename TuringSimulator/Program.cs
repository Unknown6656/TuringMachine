using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TuringMachine
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var conf = StringTuringMachineConfiguration.FromReadableConfiguration(File.ReadAllText(args[0]));
                var tm = conf.CreateDTM(false);

                Console.WriteLine($"Config loaded from '{args[0]}'. Press any key to start ...");
                Console.ReadKey(true);
                Console.Clear();
                Console.CursorVisible = false;

                while (!tm.HasHalted)
                {
                    DisplayTM(tm, 0);

                    tm.NextStep();
                }

                DisplayTM(tm, 0);
                DisplayDebug(conf, tm);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured: {ex.Message}\n{ex.StackTrace}");
            }

            Console.CursorVisible = true;
            Console.WriteLine("\nPress any key to exit ...");
            Console.ReadKey(true);
        }

        private static void DisplayDebug(StringTuringMachineConfiguration conf, DeterministicTuringMachine<char> tm)
        {
            Console.WriteLine($"\nThe turing machine halted and {(tm.State == TuringState.HaltedAccept ? "ACCEPTED" : "REJECTED")} the input '{conf.InitialMemoryLayout}'.");
            Console.WriteLine($"-------------------------------------------- DEBUG DATA --------------------------------------------");
            Console.WriteLine($"Configuration (bytes):");

            var bytes = conf.ToBytes().ToList();
            
            while (bytes.Count > 0)
            {
                int cnt = Math.Min(32, bytes.Count);

                Console.WriteLine($"    {string.Join(" ", from b in bytes.Take(cnt) select b.ToString("x2"))}");

                bytes.RemoveRange(0, cnt);
            }

            Console.WriteLine($"\nConfiguration (readable):");
            Console.WriteLine(string.Join("\n", from l in conf.GenerateReadableConfiguration().Split('\n') select "    " + l));
            Console.WriteLine($"History:\n");

            
            Console.WriteLine($"----------------------------------------------------------------------------------------------------");
        }

        private static void DisplayTM(DeterministicTuringMachine<char> tm, int yoffs)
        {
            const int SCNT = 20;
            const int CNT = SCNT * 2 + 1;

            void pline()
            {
                Console.CursorLeft = 0;
                Console.Write("    = = = ===@");

                for (int i = 0; i < CNT; ++i)
                    Console.Write("===@");

                Console.WriteLine("=== = = =");
            }

            Console.CursorTop = yoffs;

            pline();

            Console.CursorLeft += 4;
            Console.Write("- - - -  | ");
            
            for (int i = -SCNT; i <= SCNT; ++i)
            {
                Console.Write(tm.Memory[i + tm.CurrentAddress]);
                Console.Write(" | ");
            }

            Console.WriteLine(" - - - -");

            pline();

            int left = 15 + SCNT * 4;

            Console.CursorLeft = left;
            Console.WriteLine("^");
            Console.CursorLeft = left;
            Console.WriteLine(tm.CurrentState);

            var trans = tm.CurrentState.Transitions;

            for (int i = 0; i < 10; ++i)
                if (i < trans.Length)
                {
                    var t = trans[i];

                    Console.CursorLeft = left;
                    Console.WriteLine($"[{tm.CurrentState.ID}] {t}");
                }
                else
                    Console.WriteLine(new string(' ', Console.BufferWidth - 5));
        }
    }
}
