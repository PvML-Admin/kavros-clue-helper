using System;
using System.Collections.Generic;
using System.Linq;

namespace KavrosClueHelper
{
    public sealed class TowerPuzzle
    {
        public int[] Top, Bottom, Left, Right;
        public int[] Cells = new int[25];
        public string Key { get { return String.Join(",", Top.Concat(Bottom).Concat(Left).Concat(Right)); } }
    }

    public static class TowerSolver
    {
        private static readonly List<int[]> Permutations = Generate(new int[0], new[] { 1, 2, 3, 4, 5 });
        private static List<int[]> Generate(int[] prefix, int[] rest)
        {
            if (rest.Length == 0) return new List<int[]> { prefix };
            List<int[]> result = new List<int[]>();
            foreach (int value in rest) result.AddRange(Generate(prefix.Concat(new[] { value }).ToArray(), rest.Where(v => v != value).ToArray()));
            return result;
        }
        public static int Visible(IEnumerable<int> line)
        {
            int height = 0, count = 0;
            foreach (int v in line) if (v > height) { height = v; count++; }
            return count;
        }
        public static List<int[]> Solve(TowerPuzzle puzzle, bool useExistingCells)
        {
            foreach (int[] side in new[] { puzzle.Top, puzzle.Bottom, puzzle.Left, puzzle.Right })
                if (side == null || side.Length != 5 || side.Any(v => v < 1 || v > 5)) throw new ArgumentException("All 20 tower clues must be readable numbers from 1 to 5.");
            List<int[]> solutions = new List<int[]>();
            int[] board = new int[25];
            List<int[]>[] candidates = Enumerable.Range(0, 5).Select(row => Permutations.Where(p => Visible(p) == puzzle.Left[row] && Visible(p.Reverse()) == puzzle.Right[row]
                && (!useExistingCells || Enumerable.Range(0, 5).All(c => puzzle.Cells[row * 5 + c] == 0 || puzzle.Cells[row * 5 + c] == p[c]))).ToList()).ToArray();
            Search(0, puzzle, board, candidates, solutions);
            return solutions;
        }
        private static void Search(int row, TowerPuzzle puzzle, int[] board, List<int[]>[] candidates, List<int[]> solutions)
        {
            if (solutions.Count >= 2) return;
            if (row == 5) { solutions.Add((int[])board.Clone()); return; }
            foreach (int[] line in candidates[row])
            {
                bool valid = true;
                for (int c = 0; c < 5 && valid; c++)
                {
                    for (int r = 0; r < row; r++) if (board[r * 5 + c] == line[c]) valid = false;
                    if (!valid) break;
                    board[row * 5 + c] = line[c];
                    int visible = Visible(Enumerable.Range(0, row + 1).Select(r => board[r * 5 + c]));
                    if (visible > puzzle.Top[c] || visible + (4 - row) < puzzle.Top[c]) valid = false;
                    if (row == 4 && (visible != puzzle.Top[c] || Visible(Enumerable.Range(0, 5).Reverse().Select(r => board[r * 5 + c])) != puzzle.Bottom[c])) valid = false;
                }
                if (valid) Search(row + 1, puzzle, board, candidates, solutions);
            }
        }
    }

    public static class SlidingSolver
    {
        // Tile IDs 0..23, blank=24; goals are in reading order.
        public static bool IsValid(int[] board)
        {
            return board != null && board.Length == 25 && board.OrderBy(v => v).SequenceEqual(Enumerable.Range(0, 25));
        }
        public static bool IsSolvable(int[] board)
        {
            if (!IsValid(board)) return false;
            int inversions = 0;
            for (int i = 0; i < 25; i++) for (int j = i + 1; j < 25; j++) if (board[i] != 24 && board[j] != 24 && board[i] > board[j]) inversions++;
            return inversions % 2 == 0;
        }
        public static IEnumerable<int> Neighbours(int blank)
        {
            if (blank >= 5) yield return blank - 5;
            if (blank < 20) yield return blank + 5;
            if (blank % 5 > 0) yield return blank - 1;
            if (blank % 5 < 4) yield return blank + 1;
        }
        public static List<int> Solve(int[] source)
        {
            if (!IsSolvable(source)) throw new ArgumentException("The recognised sliding tiles are invalid or have impossible parity. Check the tile matching.");
            int[] board = (int[])source.Clone();
            bool[] fixedCells = new bool[25];
            List<int> moves = new List<int>();
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++) Place(board, fixedCells, new[] { row * 5 + column }, moves);
                Place(board, fixedCells, new[] { row * 5 + 3, row * 5 + 4 }, moves);
            }
            for (int column = 0; column < 3; column++) Place(board, fixedCells, new[] { 15 + column, 20 + column }, moves);
            Place(board, fixedCells, new[] { 18, 19, 23 }, moves);
            if (!board.SequenceEqual(Enumerable.Range(0, 25))) throw new InvalidOperationException("Sliding route did not reach its goal.");
            return moves;
        }
        private struct Trace { public int Parent, Move; public Trace(int parent, int move) { Parent = parent; Move = move; } }
        private static int Encode(int[] state) { int result = 0; for (int i = state.Length - 1; i >= 0; i--) result = result * 25 + state[i]; return result; }
        private static int[] Decode(int key, int count) { int[] state = new int[count]; for (int i = 0; i < count; i++) { state[i] = key % 25; key /= 25; } return state; }
        internal static void Place(int[] board, bool[] fixedCells, int[] targets, List<int> moves)
        {
            if (targets.All(t => board[t] == t)) { foreach (int t in targets) fixedCells[t] = true; return; }
            int[] initial = targets.Select(t => Array.IndexOf(board, t)).Concat(new[] { Array.IndexOf(board, 24) }).ToArray();
            int start = Encode(initial), final = -1;
            Queue<int> queue = new Queue<int>(); queue.Enqueue(start);
            Dictionary<int, Trace> visited = new Dictionary<int, Trace>(); visited[start] = new Trace(-1, -1);
            while (queue.Count > 0)
            {
                int key = queue.Dequeue(); int[] state = Decode(key, targets.Length + 1);
                if (Enumerable.Range(0, targets.Length).All(i => state[i] == targets[i])) { final = key; break; }
                int blank = state[targets.Length];
                foreach (int next in Neighbours(blank))
                {
                    if (fixedCells[next]) continue;
                    int[] successor = (int[])state.Clone(); successor[targets.Length] = next;
                    for (int i = 0; i < targets.Length; i++) if (successor[i] == next) successor[i] = blank;
                    int code = Encode(successor);
                    if (!visited.ContainsKey(code)) { visited[code] = new Trace(key, next); queue.Enqueue(code); }
                }
            }
            if (final < 0) throw new InvalidOperationException("No legal sliding route was found.");
            List<int> phase = new List<int>();
            while (visited[final].Parent != -1) { Trace trace = visited[final]; phase.Add(trace.Move); final = trace.Parent; }
            phase.Reverse();
            foreach (int next in phase) { int blank = Array.IndexOf(board, 24); board[blank] = board[next]; board[next] = 24; moves.Add(next); }
            foreach (int target in targets) fixedCells[target] = true;
        }
    }

    public sealed class RuneValue
    {
        public int Id;
        public bool IsNot;
        public RuneValue(int id, bool isNot) { Id = id; IsNot = isNot; }
    }
    public sealed class KnotLock { public int A, IndexA, B, IndexB; }
    public sealed class KnotPuzzle
    {
        public RuneValue[][] Lanes;
        public KnotLock[] Locks;
        public int[] Colours;
        public System.Drawing.Point[][] Buttons;
        // True when every rune, track and match marker was read well clear of its threshold.
        public bool Reliable;
        public double MinRune, MinTrack, MinMargin;
    }
    public sealed class KnotSolution { public int[] Offsets; public bool Certain; public int Cost { get { return Offsets.Sum(v => Math.Abs(v)); } } }
    public static class KnotSolver
    {
        public static int Mod(int i,int length){return (i%length+length)%length;}
        public static bool Equal(RuneValue a,RuneValue b){return a.IsNot?(b.IsNot||a.Id!=b.Id):(b.IsNot?a.Id!=b.Id:a.Id==b.Id);}
        private sealed class SearchState
        {
            public KnotPuzzle Puzzle;public bool[][] Pairs;public int[][] Domains;public int[] Offsets;public bool[] Assigned;
            public KnotSolution Best;public int Tentatives;public bool Certain;
            public bool Compatible(int lane,int offset)
            {
                for(int other=0;other<Assigned.Length;other++)if(Assigned[other])
                {bool[] pair=Pairs[lane*Assigned.Length+other];if(pair!=null&&!pair[offset*Puzzle.Lanes[other].Length+Offsets[other]])return false;}
                return true;
            }
            public void Search(int placed,int cost)
            {
                SearchCancellation.Check();
                if(!Certain&&Tentatives>=2||Certain&&Best!=null&&cost>=Best.Cost)return;
                if(placed==Assigned.Length)
                {
                    var found=new KnotSolution{Offsets=Offsets.Select((v,i)=>v>Puzzle.Lanes[i].Length/2?v-Puzzle.Lanes[i].Length:v).ToArray(),Certain=Certain};
                    if(Certain){if(Best==null||found.Cost<Best.Cost)Best=found;}else{Tentatives++;if(Tentatives==1)Best=found;}return;
                }
                int lane=-1,lower=cost;int[] options=null;
                for(int i=0;i<Assigned.Length;i++)if(!Assigned[i])
                {
                    int current=i;int[] viable=Domains[i].Where(v=>Compatible(current,v)).ToArray();if(viable.Length==0)return;
                    lower+=Math.Min(viable[0],Puzzle.Lanes[i].Length-viable[0]);
                    if(options==null||viable.Length<options.Length){lane=i;options=viable;}
                }
                if(Certain&&Best!=null&&lower>=Best.Cost)return;
                Assigned[lane]=true;
                foreach(int offset in options)
                {Offsets[lane]=offset;Search(placed+1,cost+Math.Min(offset,Puzzle.Lanes[lane].Length-offset));if(!Certain&&Tentatives>=2)break;}
                Assigned[lane]=false;
            }
        }
        public static KnotSolution Solve(KnotPuzzle puzzle)
        {
            if(puzzle.Lanes==null||puzzle.Lanes.Length<2||puzzle.Lanes.Length>5||puzzle.Lanes.Any(l=>l==null||l.Length<2||l.Length>40)||puzzle.Locks==null||puzzle.Locks.Length==0)throw new ArgumentException("Invalid Celtic knot lanes or intersections.");
            int n=puzzle.Lanes.Length;var state=new SearchState{Puzzle=puzzle,Pairs=new bool[n*n][],Offsets=new int[n],Assigned=new bool[n],Domains=new int[n][],Certain=true};
            for(int a=0;a<n;a++)for(int b=0;b<n;b++)if(a!=b)
            {
                KnotLock[] links=puzzle.Locks.Where(l=>l.A==a&&l.B==b||l.A==b&&l.B==a).ToArray();if(links.Length==0)continue;
                bool[] pair=new bool[puzzle.Lanes[a].Length*puzzle.Lanes[b].Length];
                for(int oa=0;oa<puzzle.Lanes[a].Length;oa++)for(int ob=0;ob<puzzle.Lanes[b].Length;ob++)
                    pair[oa*puzzle.Lanes[b].Length+ob]=links.All(l=>Equal(puzzle.Lanes[a][Mod((l.A==a?l.IndexA:l.IndexB)-oa,puzzle.Lanes[a].Length)],puzzle.Lanes[b][Mod((l.A==b?l.IndexA:l.IndexB)-ob,puzzle.Lanes[b].Length)]));
                state.Pairs[a*n+b]=pair;
            }
            for(int lane=0;lane<n;lane++)
            {
                int current=lane;int[] positions=puzzle.Locks.Where(l=>l.A==current||l.B==current).Select(l=>l.A==current?l.IndexA:l.IndexB).ToArray();
                state.Domains[lane]=Enumerable.Range(0,puzzle.Lanes[lane].Length).Where(o=>positions.All(i=>!puzzle.Lanes[current][Mod(i-o,puzzle.Lanes[current].Length)].IsNot)).OrderBy(o=>Math.Min(o,puzzle.Lanes[current].Length-o)).ToArray();
            }
            state.Search(0,0);if(state.Best!=null)return state.Best;
            state.Certain=false;for(int lane=0;lane<n;lane++){int current=lane;state.Domains[lane]=Enumerable.Range(0,puzzle.Lanes[lane].Length).OrderBy(o=>Math.Min(o,puzzle.Lanes[current].Length-o)).ToArray();}
            state.Search(0,0);return state.Tentatives==1?state.Best:null;
        }
    }

}
