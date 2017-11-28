using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System;

namespace TuringMachine
{
    public static class Program
    {
        private const int SCNT = 20;
        private const int CNT = SCNT * 2 + 1;
        private static readonly List<(string, long, TuringState<ulong, char>)> history = new List<(string, long, TuringState<ulong, char>)>();


        public static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;

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
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"\nThe turing machine halted and ");

            if (tm.State == TuringState.HaltedAccept)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("ACCEPTED");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("REJECTED");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($" the input '{conf.InitialMemoryLayout}'.");
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
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(string.Join("\n", from l in conf.GenerateReadableConfiguration().Split('\n') select "    " + l));
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"History:\n");

            int i = 0;

            foreach (var frame in history)
            {
                Console.Write($"[{i:x8}] {frame.Item3.ID,3}:   .....{frame.Item1.Substring(0, SCNT)}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(frame.Item1[SCNT]);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"{frame.Item1.Substring(SCNT + 1, SCNT)}.....");

                ++i;
            }

            Console.WriteLine($"----------------------------------------------------------------------------------------------------");
        }

        private static void DisplayTM(DeterministicTuringMachine<char> tm, int yoffs)
        {
            void pline()
            {
                Console.CursorLeft = 0;
                Console.Write("    = = = ===@");

                for (int i = 0; i < CNT; ++i)
                    if (i == SCNT - 1)
                    {
                        Console.Write("===");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write('@');
                    }
                    else
                    {
                        if (i == SCNT + 1)
                            Console.ForegroundColor = ConsoleColor.White;

                        Console.Write("===@");
                    }

                Console.WriteLine("=== = = =");
            }

            Console.CursorTop = yoffs;

            pline();

            Console.CursorLeft += 4;
            Console.Write("- - - -  | ");

            StringBuilder sb = new StringBuilder();

            for (int i = -SCNT; i <= SCNT; ++i)
            {
                char c = tm.Memory[i + tm.CurrentAddress];

                sb.Append(c);

                if (i == 1)
                    Console.ForegroundColor = ConsoleColor.White;

                Console.Write(c);

                if (i == -1)
                    Console.ForegroundColor = ConsoleColor.Cyan;

                Console.Write(" | ");
            }

            history.Add((sb.ToString(), tm.CurrentAddress, tm.CurrentState));

            Console.WriteLine(" - - - -");

            pline();

            int left = 15 + SCNT * 4;

            Console.CursorLeft = left;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("^");
            Console.ForegroundColor = ConsoleColor.White;
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
