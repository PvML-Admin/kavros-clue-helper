using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KavrosClueHelper
{
    public sealed class SlidingOptimization
    {
        public List<int> Moves;
        public int LowerBound;
        public bool Optimal;
    }
    // Bounded optimal search, several complete construction orders, then exact
    // local shortcuts. The validated fallback always remains available. A time
    // limit never changes the meaning of "optimal" in the returned result.
    public static class SlidingOptimizer
    {
        private static readonly int[][] Adjacent=Enumerable.Range(0,25).Select(p=>SlidingSolver.Neighbours(p).ToArray()).ToArray();
        private struct Key : IEquatable<Key>
        {
            public ulong A,B;
            public bool Equals(Key other){return A==other.A&&B==other.B;}
            public override bool Equals(object other){return other is Key&&Equals((Key)other);}
            public override int GetHashCode(){unchecked{return (int)(A^(A>>32)^B^(B>>32));}}
        }
        private static Key Pack(int[] board)
        {
            Key k=new Key();for(int i=0;i<25;i++){int bit=i*5;ulong v=(ulong)board[i];if(bit<64){k.A|=v<<bit;if(bit>59)k.B|=v>>(64-bit);}else k.B|=v<<(bit-64);}return k;
        }
        private sealed class Exact
        {
            public int[] Board,Goal,GoalPosition,Path=new int[600];
            public int[,] Distance=new int[25,25];
            public List<int> Found;
            public bool TimedOut,Exhausted;
            public bool ClustersEnabled=true;
            private long nodes;
            private long nodeLimit;
            private readonly bool standardGoal;
            private readonly Dictionary<Key,int> seen=new Dictionary<Key,int>();
            public Exact(int[] board,int[] goal,int milliseconds)
            {
                Board=(int[])board.Clone();Goal=goal;GoalPosition=new int[25];for(int i=0;i<25;i++)GoalPosition[goal[i]]=i;
                standardGoal=goal.SequenceEqual(Enumerable.Range(0,25));
                for(int tile=0;tile<24;tile++)for(int p=0;p<25;p++)Distance[tile,p]=Math.Abs(p/5-GoalPosition[tile]/5)+Math.Abs(p%5-GoalPosition[tile]%5);
                nodeLimit=milliseconds < 0 ? Int64.MaxValue : (long)milliseconds*ExactNodesPerMs;
            }
            public int Manhattan(){int h=0;for(int i=0;i<25;i++)if(Board[i]!=24)h+=Distance[Board[i],i];return h;}
            // Disjoint conflict pairs: no tile is charged twice, so this remains
            // a lower bound even when both row and column conflicts are present.
            private int Conflicts()
            {
                int used=0,extra=0;
                for(int line=0;line<5;line++)for(int a=0;a<5;a++)for(int b=a+1;b<5;b++)
                {
                    int x=Board[line*5+a],y=Board[line*5+b];
                    if(x!=24&&y!=24&&(used&(1<<x|1<<y))==0&&GoalPosition[x]/5==line&&GoalPosition[y]/5==line&&GoalPosition[x]%5>GoalPosition[y]%5){used|=1<<x|1<<y;extra+=2;}
                }
                for(int line=0;line<5;line++)for(int a=0;a<5;a++)for(int b=a+1;b<5;b++)
                {
                    int x=Board[a*5+line],y=Board[b*5+line];
                    if(x!=24&&y!=24&&(used&(1<<x|1<<y))==0&&GoalPosition[x]%5==line&&GoalPosition[y]%5==line&&GoalPosition[x]/5>GoalPosition[y]/5){used|=1<<x|1<<y;extra+=2;}
                }
                return extra;
            }
            public int LowerBound {get{return Math.Max(Manhattan()+Conflicts(),standardGoal?SliderPatterns.Bound(Board,ClustersEnabled):0);}}
            public bool Search(int maximum)
            {
                int h=Manhattan(),bound=LowerBound,blank=Array.IndexOf(Board,24);
                while(bound<=maximum)
                {
                    if (ExactSlider.BoundChanged != null) ExactSlider.BoundChanged(bound);
                    seen.Clear();int next=Visit(blank,-1,0,h,bound);
                    if(Found!=null)return true;if(TimedOut)return false;
                    if(next==Int32.MaxValue)break;bound=next;
                }
                Exhausted=true;return false;
            }
            private int Visit(int blank,int previous,int depth,int h,int bound)
            {
                if(++nodes>=nodeLimit){TimedOut=true;return Int32.MaxValue;}if((nodes&511)==0)SearchCancellation.Check();
                int f=depth+h;if(f>bound)return f;
                if(h==0){Found=Path.Take(depth).ToList();return -1;}
                f=Math.Max(f+Conflicts(),standardGoal?depth+SliderPatterns.Bound(Board):0);if(f>bound)return f;
                Key key=Pack(Board);int old;if(seen.TryGetValue(key,out old)&&old<=depth)return Int32.MaxValue;if(seen.Count<180000)seen[key]=depth;
                int minimum=Int32.MaxValue;
                // Try moves reducing distance first, without allocating nodes.
                for(int pass=0;pass<2;pass++)foreach(int next in Adjacent[blank])
                {
                    if(next==previous)continue;int tile=Board[next],change=Distance[tile,blank]-Distance[tile,next];if((change<0)!=(pass==0))continue;
                    Board[blank]=tile;Board[next]=24;Path[depth]=next;
                    int result=Visit(next,blank,depth+1,h+change,bound);
                    Board[next]=tile;Board[blank]=24;
                    if(Found!=null||TimedOut)return result;minimum=Math.Min(minimum,result);
                }
                return minimum;
            }
        }
        // Work units replace wall-clock limits so the same board always gets the
        // same route on every PC; "milliseconds" is calibrated on a mid-range CPU.
        internal const int ExactNodesPerMs=2000,AnytimeNodesPerMs=6000,WeightedVisitsPerMs=320;
        private static int[] Goal {get{return Enumerable.Range(0,25).ToArray();}}
        // Anytime weighted IDA*: each pass with weight w returns a route of at most
        // about w times the optimum and only accepts routes shorter than the best.
        // With w=1 and an unexhausted budget the final pass is a full proof.
        private sealed class Anytime
        {
            private readonly int[] board,position=new int[25],path=new int[400];private int manhattan;
            public List<int> Best;public int BestCount;public long Nodes,Limit;public bool Stopped,Proven;private double weight;
            public Anytime(int[] initial,List<int> incumbent,long limit)
            {
                board=(int[])initial.Clone();for(int p=0;p<25;p++)position[board[p]]=p;
                for(int p=0;p<25;p++)if(board[p]!=24)manhattan+=Math.Abs(p/5-board[p]/5)+Math.Abs(p%5-board[p]%5);
                Best=incumbent;BestCount=incumbent.Count;Limit=limit;
            }
            private int Lower(){return Math.Max(manhattan,SliderPatterns.BoundFromPositions(position));}
            public void Run(double[] weights)
            {
                int root=Lower();
                foreach(double w in weights)
                {
                    // A weight-w pass returns about w*optimum >= w*root; skip passes that
                    // cannot beat the incumbent (they degenerate into a full search).
                    if(w>1.0&&w*root>=BestCount)continue;
                    weight=w;double bound=w*root;
                    // Every ancestor of a route of length g has g'+w*h' <= w*g (h admissible), so
                    // once the bound passes w*(best-1) this weight cannot shorten the route.
                    while(!Stopped&&bound<=weight*(BestCount-1)){double next=Visit(position[24],-1,0,bound);if(next<0||next>=1e9)break;bound=next;}
                    if(Stopped)return;
                    if(w==1.0){Proven=true;return;}
                }
            }
            private double Visit(int blank,int previous,int depth,double bound)
            {
                if(++Nodes>=Limit){Stopped=true;return 1e9;}if((Nodes&4095)==0)SearchCancellation.Check();
                int lower=Lower();if(depth+lower>=BestCount)return 1e9;
                double f=depth+weight*lower;if(f>bound)return f;
                if(lower==0){BestCount=depth;Best=path.Take(depth).ToList();return -1;}
                double minimum=1e9;
                foreach(int next in Adjacent[blank])
                {
                    if(next==previous)continue;int tile=board[next];
                    int delta=Math.Abs(blank/5-tile/5)+Math.Abs(blank%5-tile%5)-Math.Abs(next/5-tile/5)-Math.Abs(next%5-tile%5);
                    board[blank]=tile;board[next]=24;position[tile]=blank;position[24]=next;manhattan+=delta;path[depth]=next;
                    double result=Visit(next,blank,depth+1,bound);
                    board[next]=tile;board[blank]=24;position[tile]=next;position[24]=blank;manhattan-=delta;
                    if(result<0||Stopped)return result;minimum=Math.Min(minimum,result);
                }
                return minimum;
            }
        }
        private sealed class Node
        {public int[] Board;public int Blank,Parent,Move,Depth,H;public Key Key;}
        private static List<int> Weighted(int[] initial,int ceiling,int milliseconds,int weight,bool clustersEnabled=true)
        {
            long visitLimit=(long)milliseconds*WeightedVisitsPerMs;var heuristic=new Exact(initial,Goal,1);var nodes=new List<Node>();var heap=new List<int>();var best=new Dictionary<Key,int>();
            heuristic.ClustersEnabled=clustersEnabled;
            Func<int,int,bool> before=(a,b)=>{Node x=nodes[a],y=nodes[b];int f=1000*x.Depth+weight*x.H,g=1000*y.Depth+weight*y.H;return f<g||f==g&&x.H<y.H;};
            Action<int> push=id=>{heap.Add(id);int p=heap.Count-1;while(p>0){int parent=(p-1)/2;if(!before(heap[p],heap[parent]))break;int swap=heap[p];heap[p]=heap[parent];heap[parent]=swap;p=parent;}};
            Node root=new Node{Board=(int[])initial.Clone(),Blank=Array.IndexOf(initial,24),Parent=-1,H=heuristic.LowerBound,Key=Pack(initial)};nodes.Add(root);best[root.Key]=0;push(0);long visits=0;
            while(heap.Count>0&&nodes.Count<400000)
            {
                if(++visits>=visitLimit)break;if((visits&255)==0)SearchCancellation.Check();
                int id=heap[0],last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);if(heap.Count>0){heap[0]=last;int p=0;while(true){int a=p*2+1;if(a>=heap.Count)break;int b=a+1,child=b<heap.Count&&before(heap[b],heap[a])?b:a;if(!before(heap[child],heap[p]))break;int swap=heap[p];heap[p]=heap[child];heap[child]=swap;p=child;}}
                Node node=nodes[id];int known;if(!best.TryGetValue(node.Key,out known)||known!=node.Depth)continue;
                if(node.H==0){var path=new List<int>();for(int p=id;nodes[p].Parent>=0;p=nodes[p].Parent)path.Add(nodes[p].Move);path.Reverse();return path;}
                if(node.Depth+node.H>=ceiling)continue;
                foreach(int next in Adjacent[node.Blank])
                {
                    if(node.Parent>=0&&nodes[node.Parent].Blank==next)continue;
                    int tile=node.Board[next];node.Board[node.Blank]=tile;node.Board[next]=24;Key key=Pack(node.Board);int depth=node.Depth+1;
                    if(!best.TryGetValue(key,out known)||known>depth)
                    {
                        heuristic.Board=node.Board;int h=heuristic.LowerBound;
                        if(depth+h<ceiling){int child=nodes.Count;nodes.Add(new Node{Board=(int[])node.Board.Clone(),Blank=next,Parent=id,Move=next,Depth=depth,H=h,Key=key});best[key]=depth;push(child);}
                    }
                    node.Board[next]=tile;node.Board[node.Blank]=24;
                }
            }
            return null;
        }
        private static int[] Apply(int[] initial,IEnumerable<int> moves)
        {
            int[] b=(int[])initial.Clone();int blank=Array.IndexOf(b,24);
            foreach(int move in moves){if(!Adjacent[blank].Contains(move))throw new InvalidOperationException("Non-adjacent slider route");b[blank]=b[move];b[move]=24;blank=move;}return b;
        }
        internal static int[] Replay(int[] initial, IEnumerable<int> moves) { return Apply(initial, moves); }

        internal static List<int> Prove(int[] initial, List<int> seed)
        {
            var exact = new Exact(initial, Goal, -1);
            exact.Path = new int[seed.Count + 1];
            return exact.Search(seed.Count - 1) ? exact.Found : seed;
        }

        private static List<int> Shortcuts(int[] initial,List<int> best,int windowBudget)
        {
            best=new List<int>(best);
            for(int start=0;start+8<best.Count;start+=8)
            {
                int length=Math.Min(24,best.Count-start);
                int[] a=Apply(initial,best.Take(start)),b=Apply(a,best.Skip(start).Take(length));var shortcut=new Exact(a,b,windowBudget);
                if(shortcut.LowerBound>=length)continue;
                if(shortcut.Search(length-2)){best.RemoveRange(start,length);best.InsertRange(start,shortcut.Found);}
            }
            return best;
        }
        private static IEnumerable<string> Orders(string prefix="",int rows=0,int columns=0)
        {
            if((5-rows)*(5-columns)<=6){yield return prefix;yield break;}
            if(rows<3)foreach(string s in Orders(prefix+"R",rows+1,columns))yield return s;
            if(columns<3)foreach(string s in Orders(prefix+"C",rows,columns+1))yield return s;
        }
        private static List<int> Construct(int[] initial,string order)
        {
            int[] b=(int[])initial.Clone();bool[] fixedCells=new bool[25];var moves=new List<int>();int row=0,column=0;
            foreach(char phase in order)
            {
                SearchCancellation.Check();
                if(phase=='R')
                {
                    for(int x=column;x<3;x++)SlidingSolver.Place(b,fixedCells,new[]{row*5+x},moves);
                    SlidingSolver.Place(b,fixedCells,new[]{row*5+3,row*5+4},moves);row++;
                }
                else
                {
                    for(int y=row;y<3;y++)SlidingSolver.Place(b,fixedCells,new[]{y*5+column},moves);
                    SlidingSolver.Place(b,fixedCells,new[]{15+column,20+column},moves);column++;
                }
            }
            int[] last=Enumerable.Range(0,24).Where(i=>!fixedCells[i]).ToArray();
            SlidingSolver.Place(b,fixedCells,last,moves);
            if(!b.SequenceEqual(Goal))throw new InvalidOperationException("Slider construction failed");return SlidingRoute.Compact(initial,moves);
        }
        public static SlidingOptimization Solve(int[] initial,int milliseconds=2000)
        {
            if(!SlidingSolver.IsSolvable(initial))throw new ArgumentException("Invalid sliding board");
            List<int> best=SlidingRoute.Compact(initial,SlidingSolver.Solve(initial));
            var exact=new Exact(initial,Goal,Math.Min(250,milliseconds/8));int lower=exact.LowerBound;
            if(best.Count==lower)return new SlidingOptimization{Moves=best,LowerBound=lower,Optimal=true};
            if(exact.Search(best.Count-1))return new SlidingOptimization{Moves=exact.Found,LowerBound=exact.Found.Count,Optimal=true};
            if(exact.Exhausted)return new SlidingOptimization{Moves=best,LowerBound=best.Count,Optimal=true};
            foreach(string order in new[]{"RCRCR","CRCRC","RRRCC","CCCRR"}.Concat(Orders()).Distinct())
            {List<int> candidate=Construct(initial,order);if(candidate.Count<best.Count)best=candidate;}
            // Stage A (about 2 s of work, as in v0.6.4): global weighted A* ladder and shortcuts.
            int stageA=Math.Min(milliseconds,2000);
            {List<int> alternative=Weighted(initial,best.Count,Math.Max(1,stageA/16),1500,false);if(alternative!=null&&alternative.Count<best.Count)best=alternative;}
            int[] weights={3000,2600,2200,1800,1500,1200,1000};
            int ladder=Math.Max(1,(int)(stageA*0.6)/weights.Length);
            for(int i=0;i<weights.Length;i++)
            {
                List<int> global=Weighted(initial,best.Count,ladder,weights[i]);
                if(global!=null&&global.Count<best.Count){best=global;if(weights[i]==1000)return new SlidingOptimization{Moves=best,LowerBound=best.Count,Optimal=true};}
            }
            best=Shortcuts(initial,SlidingRoute.Compact(initial,best),45);
            // Stage B (the rest of the budget): anytime weighted IDA* seeded with the
            // stage-A route, so it can only shorten it; then shortcuts again.
            if(milliseconds>stageA)
            {
                var anytime=new Anytime(initial,best,(long)(milliseconds-stageA)*AnytimeNodesPerMs);
                anytime.Run(new[]{3.0,2.5,2.0,1.8,1.6,1.5,1.4,1.35,1.3,1.25,1.2,1.15,1.1,1.05,1.0});
                best=anytime.Best;
                if(anytime.Proven)return new SlidingOptimization{Moves=best,LowerBound=best.Count,Optimal=true};
                best=Shortcuts(initial,best,Math.Min(200,milliseconds/40));
            }
            best=SlidingRoute.Compact(initial,best);
            if(!Apply(initial,best).SequenceEqual(Goal))throw new InvalidOperationException("Optimised slider route failed validation");
            return new SlidingOptimization{Moves=best,LowerBound=lower,Optimal=best.Count==lower};
        }
    }
}
