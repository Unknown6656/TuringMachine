using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TuringMachine
{
    public delegate void TuringMachineEvent<T>(DeterministicTuringMachine<T> tm) where T : struct;

    public delegate void TuringMachineEvent<T, U>(DeterministicTuringMachine<T> tm, U data) where T : struct;

    [Serializable]
    public class DeterministicTuringMachine<T>
        where T : struct
    {
        public TuringMachineConfiguration<ulong, T> Configuration { get; }
        public TuringMachineMemory<T> Memory { get; }
        public bool AllowUndefinedRunning { get; }
        public TuringState<ulong, T> CurrentState { get; private set; }
        public long CurrentAddress { get; private set; }
        public TuringState State { get; private set; }
        public bool HasHalted => AllowUndefinedRunning ? State == TuringState.Active : State != TuringState.Active;


        public event TuringMachineEvent<T, TuringState> OnHalted;
        public event TuringMachineEvent<T, (TuringState<ulong, T> From, TuringTransition<ulong, T> Transition)> OnTransition;


        public void NextStep()
        {
            if (State == TuringState.Active)
                try
                {
                    T input = Memory[CurrentAddress];
                    TuringTransition<ulong, T> trans = Configuration.GetTransition(CurrentState.ID, input);

                    Memory[CurrentAddress] = trans.OutputSymbol;

                    if (trans.Action == TuringAction.MoveLeft)
                        --CurrentAddress;
                    else if (trans.Action == TuringAction.MoveRight)
                        ++CurrentAddress;

                    OnTransition?.Invoke(this, (CurrentState, trans));
                    CurrentState = Configuration[trans.TargetID];

                    if (CurrentState.Accpetance == TuringAcceptance.Accept)
                        State = TuringState.HaltedAccept;
                    else if (CurrentState.Accpetance == TuringAcceptance.Reject)
                        State = TuringState.HaltedReject;
                }
                catch (Exception)
                {
                    if (!AllowUndefinedRunning)
                        State = TuringState.HaltedReject;
                }

            if (State != TuringState.Active)
                OnHalted?.Invoke(this, State);
        }

        public void Initialize(T[] data)
        {
            for (int i = 0; i < data.Length; ++i)
                Memory[i] = data[i];

            CurrentAddress = 0;
            State = TuringState.Active;
        }

        public DeterministicTuringMachine(TuringMachineConfiguration<ulong, T> conf, T def, bool allowundefined)
        {
            Memory = new TuringMachineMemory<T>(def);
            CurrentState = conf[conf.StartStateIndex];
            Configuration = conf;
            CurrentAddress = 0;
        }
    }

    [Serializable]
    public class TuringMachineMemory<T>
        where T : struct
    {
        private readonly Dictionary<long, T> data = new Dictionary<long, T>();
        private T @default;


        public event Action<(long addr, T data)> OnDataRead;
        public event Action<(long addr, T old, T @new)> OnDataWritten;


        public T this[long addr]
        {
            set
            {
                T old = data.ContainsKey(addr) ? data[addr] : @default;

                if (value.Equals(@default))
                    data.Remove(addr);
                else
                    data[addr] = value;

                OnDataWritten?.Invoke((addr, old, value));
            }
            get
            {
                T value = data.ContainsKey(addr) ? data[addr] : @default;

                OnDataRead?.Invoke((addr, value));

                return value;
            }
        }

        public int UsedSize => data.Count;

        public T[] AsArray
        {
            get
            {
                var skeys = from kvp in data
                            where !kvp.Value.Equals(@default)
                            select kvp.Key;

                long min_ndx = skeys.Min();
                long max_ndx = skeys.Max();

                T[] arr = new T[max_ndx - min_ndx + 1];

                for (int i = 0; i < arr.Length; ++i)
                    arr[i] = this[i + min_ndx];

                return arr;
            }
        }


        public TuringMachineMemory(T def) => @default = def;

        public void Clear() => data.Clear();
    }

    [Serializable]
    public class TuringMachineConfiguration<I, T>
        where I : struct
        where T : struct
    {
        private readonly Dictionary<I, TuringState<I, T>> states = new Dictionary<I, TuringState<I, T>>();
        
        
        public TuringState<I, T> this[I key]
        {
            set => states[key] = value.ID.Equals(key) ? value : throw new ArgumentException($"The field '{nameof(TuringState<I, T>.ID)}' must be equal to the given key.", nameof(value));
            get => states[key];
        }

        public TuringState<I, T>[] States => states.Values.ToArray();

        public I StartStateIndex { set; get; }


        internal TuringMachineConfiguration()
        {
        }

        public TuringState<I, T> GetState(I id) => this[id];

        public void AddState(TuringState<I, T> state) => this[state.ID] = state;

        public void AddStates(params TuringState<I, T>[] states)
        {
            foreach (TuringState<I, T> s in states)
                AddState(s);
        }

        public TuringState<I, T> RemoveState(I id) => RemoveState(GetState(id));

        public TuringState<I, T> RemoveState(TuringState<I, T> state)
        {
            states.Remove(state.ID);

            return state;
        }

        public void AddTransition(I from, I to, TuringAction act, T output, T symbol) => AddTransition(from, to, act, output, new T[] { symbol });

        public void AddTransition(I from, I to, TuringAction act, T output, params T[] symbols) => AddTransition(from, to, act, output, symbols as IEnumerable<T>);

        public void AddTransition(I from, I to, TuringAction act, T output, IEnumerable<T> symbols) => GetState(from).AddTransition((to, act, output, symbols));

        public TuringTransition<I, T> GetTransition(I from, T symbol) => GetState(from).GetTransition(symbol);

        public void ClearTransitions(I from) => this[from].Clear();

        public void ClearTransitions()
        {
            foreach (I from in states.Keys)
                ClearTransitions(from);
        }

        public void Clear() => states.Clear();

        public byte[] ToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(states.Count);

                foreach (I id in states.Keys)
                {
                    TuringState<I, T> state = states[id];
                    TuringTransition<I, T>[] trans = state.Transitions;
                    
                    put(wr, id);
                    wr.Write((byte)state.Accpetance);
                    wr.Write(trans.Length);

                    for (int i = 0; i < trans.Length; ++i)
                    {
                        TuringTransition<I, T> t = trans[i];

                        wr.Write((byte)t.Action);
                        wr.Write(t.InputSymbols.Length);

                        foreach (T s in t.InputSymbols)
                            put(wr, s);

                        put(wr, t.OutputSymbol);
                        put(wr, t.TargetID);
                    }
                }

                return ms.ToArray();
            }
        }

        public string ToBase64() => Convert.ToBase64String(ToBytes());

        public void ToFile(FileInfo nfo)
        {
            if (!nfo.Exists)
                nfo.Create().Close();

            byte[] dat = ToBytes();

            using (FileStream fs = nfo.OpenWrite())
                fs.Write(dat, 0, dat.Length);
        }


        public static TuringMachineConfiguration<I, T> FromBytes(byte[] arr)
        {
            TuringMachineConfiguration<I, T> conf = EmptyConfiguration;

            using (MemoryStream ms = new MemoryStream(arr))
            using (BinaryReader rd = new BinaryReader(ms))
            {
                for (int si = 0, sc = rd.ReadInt32(); si < sc; ++si)
                {
                    I id = get<I>(rd);
                    TuringAcceptance acc = (TuringAcceptance)rd.ReadByte();
                    List<TuringTransition<I, T>> trans = new List<TuringTransition<I, T>>();

                    for (int ti = 0, tc = rd.ReadInt32(); ti < tc; ++ti)
                    {
                        TuringAction act = (TuringAction)rd.ReadByte();
                        T[] input = new T[rd.ReadInt32()];

                        for (int ii = 0; ii < input.Length; ++ii)
                            input[ii] = get<T>(rd);

                        T output = get<T>(rd);
                        I tid = get<I>(rd);

                        trans.Add((tid, act, output, input));
                    }

                    conf.AddState((id, acc, trans));
                }

                return conf;
            }
        }

        public static TuringMachineConfiguration<I, T> FromBase64(string b64) => FromBytes(Convert.FromBase64String(b64));

        public static TuringMachineConfiguration<I, T> FromFile(FileInfo nfo)
        {
            byte[] bytes = new byte[nfo.Length];

            using (FileStream fs = nfo.OpenRead())
                fs.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        public static TuringMachineConfiguration<I, T> EmptyConfiguration => new TuringMachineConfiguration<I, T>();

        private static void put<U>(BinaryWriter wr, U s)
            where U : struct
        {
            int size = Marshal.SizeOf(s);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr<U>(s, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            wr.Write(size);
            wr.Write(arr);
        }

        private static U get<U>(BinaryReader rd)
            where U : struct
        {
            U s = new U();
            int size = rd.ReadInt32();
            byte[] arr = rd.ReadBytes(size);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(arr, 0, ptr, size);

            s = Marshal.PtrToStructure<U>(ptr);

            Marshal.FreeHGlobal(ptr);

            return s;
        }
    }

    [Serializable]
    public struct TuringTransition<I, T>
        where I : struct
        where T : struct
    {
        private readonly List<T> isymbols;

        public I TargetID { get; }
        public T OutputSymbol { get; }
        public TuringAction Action { get; }
        public T[] InputSymbols => isymbols.ToArray();


        public TuringTransition(I target, TuringAction act, T output, params T[] input)
            : this(target, act, output, input as IEnumerable<T>)
        {
        }

        public TuringTransition(I target, TuringAction act, T output, IEnumerable<T> input)
        {
            Action = act;
            TargetID = target;
            OutputSymbol = output;
            isymbols = input?.ToList() ?? new List<T>();
        }

        public void AddSymbols(params T[] symbols) => AddSymbols(symbols as IEnumerable<T>);

        public void AddSymbols(IEnumerable<T> symbols)
        {
            foreach (T s in symbols)
                AddSymbol(s);
        }

        public void AddSymbol(T symbol)
        {
            if (!isymbols.Contains(symbol))
                isymbols.Add(symbol);
        }

        public void Clear() => isymbols.Clear();

        public void RemoveSymbol(T symbol)
        {
            if (isymbols.Contains(symbol))
                isymbols.Remove(symbol);
        }

        public override string ToString() => $"'{string.Join("', '", InputSymbols)}'  -->  ({Action}, '{OutputSymbol}', {TargetID})";


        public static implicit operator (I target, TuringAction act, T output, List<T> input)(TuringTransition<I, T> t) => (t.TargetID, t.Action, t.OutputSymbol, t.isymbols);

        public static implicit operator TuringTransition<I, T>((I target, TuringAction act, T output, IEnumerable<T> input) t) => new TuringTransition<I, T>(t.target, t.act, t.output, t.input);
    }

    [Serializable]
    public struct TuringState<I, T>
        where I : struct
        where T : struct
    {
        private readonly Dictionary<I, TuringTransition<I, T>> transitions;


        public I ID { get; }

        public TuringAcceptance Accpetance { get; }

        public TuringTransition<I, T>[] Transitions => transitions.Values.ToArray();


        public TuringTransition<I, T> this[I target]
        {
            set => transitions[target] = value.TargetID.Equals(target) ? value : throw new ArgumentException($"The field '{nameof(TuringTransition<I, T>.TargetID)}' must be equal to the given key.", nameof(value));
            get => transitions[target];
        }

        public TuringState(I id, TuringAcceptance acc, params TuringTransition<I, T>[] transitions)
            : this(id, acc, transitions as IEnumerable<TuringTransition<I, T>>)
        {
        }

        public TuringState(I id, TuringAcceptance acc, IEnumerable<TuringTransition<I, T>> transitions)
        {
            ID = id;
            Accpetance = acc;
            this.transitions = new Dictionary<I, TuringTransition<I, T>>();

            foreach (TuringTransition<I, T> t in transitions?.ToList() ?? new List<TuringTransition<I, T>>())
                AddTransition(t);
        }

        public void AddTransition(TuringTransition<I, T> trans)
        {
            if (transitions.ContainsKey(trans.TargetID))
                transitions[trans.TargetID].AddSymbols(trans.InputSymbols);
            else
                transitions[trans.TargetID] = trans;
        }

        public TuringTransition<I, T> GetTransition(T symbol) => transitions.Values.First(t => t.InputSymbols.Contains(symbol));

        public TuringTransition<I, T> GetTransition(I target) => this[target];

        public TuringTransition<I, T> RemoveTransition(I target) => RemoveTransition(GetTransition(target));

        public TuringTransition<I, T> RemoveTransition(TuringTransition<I, T> trans)
        {
            transitions.Remove(trans.TargetID);

            return trans;
        }

        public void Clear() => transitions.Clear();

        public override string ToString() => $"{(Accpetance == TuringAcceptance.Accept ? "[ACC.] " : Accpetance == TuringAcceptance.Reject ? "[REJ.]" : "")}{ID} ({transitions.Count} Transition[s])";


        public static implicit operator (I id, TuringAcceptance acc, TuringTransition<I, T>[] trans) (TuringState<I, T> t) => (t.ID, t.Accpetance, t.transitions.Values.ToArray());

        public static implicit operator TuringState<I, T>((I id, TuringAcceptance acc, IEnumerable<TuringTransition<I, T>> trans) t) => new TuringState<I, T>(t.id, t.acc, t.trans);
    }

    [Serializable]
    public enum TuringAcceptance
        : byte
    {
        None,
        Accept,
        Reject,
    }

    [Serializable]
    public enum TuringAction
        : byte
    {
        None = 0,
        MoveLeft,
        MoveRight,
    }

    [Serializable]
    public enum TuringState
        : byte
    {
        Active,
        HaltedAccept,
        HaltedReject,
    }
}
