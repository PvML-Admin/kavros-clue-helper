using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Threading;
using KavrosClueHelper;

class SolverChecks
{
    static int checks;
    static readonly int[] Goal = Enumerable.Range(0,25).ToArray();
    static void Assert(bool ok,string msg){ if(!ok)throw new Exception(msg); checks++; }
    static string Key(int[] b){return new string(b.Select(v=>(char)(v+65)).ToArray());}
    static int[] Move(int[] b,int to)
    {
        int[] copy=(int[])b.Clone(); int p=Array.IndexOf(copy,24);
        int step=(p/5==to/5?1:5)*Math.Sign(to-p);
        while(p!=to){copy[p]=copy[p+step];p+=step;} copy[to]=24;return copy;
    }
    static IEnumerable<int> Neighbours(int p,bool lines)
    {
        for(int i=0;i<25;i++){
            int dx=Math.Abs(i%5-p%5),dy=Math.Abs(i/5-p/5);
            if(i!=p && (dx==0||dy==0) && (lines||dx+dy==1))yield return i;
        }
    }
    class State {public int[] b; public List<int> undo;public int depth;}
    static int Bfs(bool lines,int max)
    {
        var q=new Queue<State>(); q.Enqueue(new State{b=Goal,undo=new List<int>()});
        var visited=new HashSet<string>{Key(Goal)};int n=0;
        while(q.Count>0){
            State s=q.Dequeue();n++;
            int blank=Array.IndexOf(s.b,24);
            var seed=new List<int>(s.undo);
            int neighbour=Neighbours(blank,false).First();
            seed.Insert(0,blank);seed.Insert(0,neighbour); // valid but deliberately longer
            var optimal=ExactSlider.Solve(s.b,seed,lines,CancellationToken.None);
            var clicks=lines?SlidingRoute.ClickRoute(s.b,optimal):optimal;
            Assert(clicks.Count==s.depth,"BFS length "+s.depth+" found "+clicks.Count);
            var end=s.b;
            foreach(int c in clicks){Assert(Neighbours(Array.IndexOf(end,24),lines).Contains(c),"legal");end=Move(end,c);}
            Assert(end.SequenceEqual(Goal),"goal");
            if(s.depth==max)continue;
            foreach(int t in Neighbours(blank,lines)){
                var b=Move(s.b,t); if(!visited.Add(Key(b)))continue;
                var back=new List<int>();int p=t,step=Math.Sign(blank-t)*(p/5==blank/5?1:5);
                while(p!=blank){p+=step;back.Add(p);}back.AddRange(s.undo);
                q.Enqueue(new State{b=b,undo=back,depth=s.depth+1});
            }
        }
        return n;
    }
    static DateTime stamp=DateTime.UtcNow;
    static void Observe(CompassSession s, CompassPosition loc, PointF dest)
    {
        for(int i=0;i<3;i++){stamp=stamp.AddMilliseconds(150);s.Observe(new CompassReading{Angle=CompassMath.Direction(loc.Point,dest),Error=0.002},stamp);}
    }
    static void Record(CompassSession s,CompassPosition loc,PointF dest)
    {Observe(s,loc,dest);Assert(s.Record(loc,stamp),"record");}
    static void Hidden(CompassSession s,int ms)
    {stamp=stamp.AddMilliseconds(20);s.Observe(null,stamp);stamp=stamp.AddMilliseconds(ms);}
    static void Compass()
    {
        var s=new CompassSession("assets");
        var a=new CompassPosition{X=2400,Y=3300,Radius=0};
        var b=new CompassPosition{X=3400,Y=3100,Radius=0};
        PointF dest=new PointF(0,0);
        foreach(PointF p in s.AllSpots){
            s.NewClue();Record(s,a,p);Record(s,b,p);
            if(s.Candidates.Count==1 && s.Intersection.HasValue && CompassMath.Distance(a.Point,p)>100 && CompassMath.Distance(b.Point,p)>100){dest=p;break;}
        }
        Assert(s.Candidates.Count==1 && dest.X>0,"unique target fixture");
        int serial=s.Generation;
        Hidden(s,800);Observe(s,a,dest);
        Assert(s.HasPrevious && s.Bearings.Count==0 && !s.Intersection.HasValue,"hide old X");
        Assert(s.Record(a,stamp) && s.HasPrevious && s.Bearings.Count==1 && !s.Intersection.HasValue,"one coincident ray cannot restore stale X");
        Hidden(s,820);Record(s,b,dest);
        Assert(!s.HasPrevious && s.Bearings.Count==2 && s.Generation==serial,"same clue after independent confirmation");
        PointF next=s.AllSpots.First(p=>Math.Abs(CompassMath.Difference(CompassMath.Direction(a.Point,dest),CompassMath.Direction(a.Point,p)))>0.7 && CompassMath.Distance(a.Point,p)>100);
        Hidden(s,900);Record(s,a,next);
        Assert(s.Generation==serial+1 && !s.HasPrevious && s.Bearings.Count==1 && s.Candidates.Contains(next) && !s.Candidates.Contains(dest),"missed X");
        Hidden(s,10);Observe(s,b,next);Assert(!s.HasPrevious && s.Bearings.Count==1,"brief gap");Assert(s.Record(b,stamp),"second fresh");
        Hidden(s,850);Observe(s,a,next);Assert(s.HasPrevious,"park");s.ResumePrevious();Assert(!s.HasPrevious&&s.Bearings.Count==2,"continue");
        s.NewClue();Record(s,a,dest);Record(s,b,dest);serial=s.Generation;Record(s,a,next);
        Assert(s.Generation==serial+1 && s.Bearings.Count==1,"new heading without missing frame");
        s.Observe(new CompassReading{Solved=true},stamp.AddMilliseconds(50));Hidden(s,900);Observe(s,a,dest);
        Assert(s.Bearings.Count==0&&!s.HasPrevious && s.Generation==serial+2,"observed X");
        s.NewClue();Record(s,a,dest);Hidden(s,900);Record(s,b,dest);
        Assert(!s.HasPrevious&&s.Bearings.Count==2&&s.Candidates.Contains(dest),"first to second lodestone");
        s.NewClue();Record(s,a,dest);Hidden(s,900);Observe(s,b,next);s.NewClue();
        Assert(!s.HasHistory && s.Generation>serial,"clear pending history");
    }
    static void Worker()
    {
        var s=new SliderProofSession();int[] board=Move(Goal,20);
        var seed=new List<int>{21,22,23,24};
        s.Start(board,"A",true,seed);
        List<int> r;string err;
        int i=0;while(!s.Read(board,"A",true,out r,out err)&&i++<1000)Thread.Sleep(2);
        Assert(r!=null && SlidingRoute.ClickRoute(board,r).Count==1,"line proof worker");
        Assert(!s.Read(Move(board,22),"A",true,out r,out err),"mid-line is separate");
        Assert(s.Read(Goal,"A",true,out r,out err)&&r.Count==0,"complete line");
        Assert(!s.Read(board,"B",true,out r,out err)&&!s.Read(board,"A",false,out r,out err),"theme and model");
        s.Cancel(); Assert(!s.Read(board,"A",true,out r,out err),"reset");
        var stop=new CancellationTokenSource();stop.Cancel();
        try{ExactSlider.Solve(board,seed,false,stop.Token);throw new Exception("ignored cancellation");}catch(OperationCanceledException){checks++;}
        try{ExactSlider.Solve(board,new List<int>(),false,CancellationToken.None);throw new Exception("accepted bad seed");}catch(ArgumentException){checks++;}
        int[] hard=Goal;var rng=new Random(7531);for(int j=0;j<270;j++){var choices=Neighbours(Array.IndexOf(hard,24),false).ToArray();hard=Move(hard,choices[rng.Next(choices.Length)]);}
        s.Start(hard,"old",false,null);s.Start(board,"B",false,seed);
        i=0;while(!s.Read(board,"B",false,out r,out err)&&i++<1500)Thread.Sleep(2);
        Assert(r!=null&&r.Count==4 && !s.Read(hard,"old",false,out r,out err),"replacement ignores old completion");
        s.Cancel();
    }
    public static int Main()
    {
        try{Assert(SliderPatterns.Count==12,"production pattern assets");int a=Bfs(false,7),b=Bfs(true,4);Worker();Compass();
            Console.WriteLine("PASS: "+a+" adjacent boards and "+b+" whole-line boards matched independent breadth-first search. Compass and cancellation scenarios passed. "+checks+" assertions.");return 0;}
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
