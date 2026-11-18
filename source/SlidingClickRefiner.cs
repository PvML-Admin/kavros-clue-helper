using System;using System.Collections.Generic;using System.Diagnostics;using System.Linq;
namespace KavrosClueHelper
{
    // Meet in the middle between actual states on a valid route. Both halves
    // use real line-click actions, so shortcuts minimise clicks rather than
    // just compressing an individual-tile solution.
    public static class SlidingClickRefiner
    {
        private struct Key : IEquatable<Key>
        {public ulong A,B;public bool Equals(Key k){return A==k.A&&B==k.B;}public override bool Equals(object k){return k is Key&&Equals((Key)k);}public override int GetHashCode(){return (int)(A^(A>>32)^B^(B>>32));}}
        private sealed class Node {public int[] Board;public int Blank,Parent,Target,Depth;}
        private static Key Pack(int[] board)
        {var k=new Key();for(int i=0;i<25;i++){int bit=i*5;ulong v=(ulong)board[i];if(bit<64){k.A|=v<<bit;if(bit>59)k.B|=v>>(64-bit);}else k.B|=v<<(bit-64);}return k;}
        private static int[] Apply(int[] source,IEnumerable<int> clicks)
        {int[] b=(int[])source.Clone();int blank=Array.IndexOf(b,24);foreach(int target in clicks){int step=blank/5==target/5?Math.Sign(target-blank):5*Math.Sign(target-blank);while(blank!=target){int n=blank+step;b[blank]=b[n];b[n]=24;blank=n;}}return b;}
        private static List<int> Join(List<Node> a,int ai,List<Node> b,int bi)
        {var path=new List<int>();for(int i=ai;a[i].Parent>=0;i=a[i].Parent)path.Add(a[i].Target);path.Reverse();for(int i=bi;b[i].Parent>=0;i=b[i].Parent)path.Add(b[b[i].Parent].Blank);return path;}
        private static List<int> Shortcut(int[] start,int[] goal,int limit,Stopwatch clock,long end)
        {
            var a=new List<Node>{new Node{Board=start,Blank=Array.IndexOf(start,24),Parent=-1}};var b=new List<Node>{new Node{Board=goal,Blank=Array.IndexOf(goal,24),Parent=-1}};
            var ak=new Dictionary<Key,int>{{Pack(start),0}};var bk=new Dictionary<Key,int>{{Pack(goal),0}};var af=new List<int>{0};var bf=new List<int>{0};
            for(int depth=0;depth<Math.Min(limit,12)&&af.Count>0&&bf.Count>0;depth++)
            {
                bool forward=depth%2==0;var nodes=forward?a:b;var own=forward?ak:bk;var other=forward?bk:ak;var frontier=forward?af:bf;var nextFrontier=new List<int>();
                foreach(int id in frontier)
                {
                    if((id&127)==0){SearchCancellation.Check();if(clock.ElapsedMilliseconds>=end)return null;}
                    Node n=nodes[id];
                    foreach(int step in new[]{-1,1,-5,5})
                    {int blank=n.Blank,steps=0;for(int length=1;length<=4;length++)
                     {int p=blank+step;if(p<0||p>=25||Math.Abs(step)==1&&p/5!=blank/5)break;n.Board[blank]=n.Board[p];n.Board[p]=24;blank=p;steps++;Key key=Pack(n.Board);int old;
                      if(!own.ContainsKey(key)){int child=nodes.Count;nodes.Add(new Node{Board=(int[])n.Board.Clone(),Blank=blank,Target=blank,Parent=id,Depth=n.Depth+1});own[key]=child;nextFrontier.Add(child);if(other.TryGetValue(key,out old)){List<int> result=forward?Join(a,child,b,old):Join(a,old,b,child);while(steps-->0){int previous=blank-step;n.Board[blank]=n.Board[previous];n.Board[previous]=24;blank=previous;}return result;}}
                     }
                     while(steps-->0){int previous=blank-step;n.Board[blank]=n.Board[previous];n.Board[previous]=24;blank=previous;}
                    }
                    if(nodes.Count>25000)return null;
                }
                if(forward)af=nextFrontier;else bf=nextFrontier;
            }
            return null;
        }
        public static List<int> Refine(int[] initial,List<int> moves,int milliseconds)
        {
            var clock=Stopwatch.StartNew();var clicks=SlidingRoute.ClickRoute(initial,moves);
            for(int pass=0;pass<2&&clock.ElapsedMilliseconds<milliseconds;pass++)
            for(int index=pass*3;index+6<clicks.Count&&clock.ElapsedMilliseconds<milliseconds;index+=4)
            {
                int length=Math.Min(14,clicks.Count-index);int[] start=Apply(initial,clicks.Take(index)),goal=Apply(start,clicks.Skip(index).Take(length));
                List<int> shortcut=Shortcut(start,goal,length-1,clock,Math.Min(milliseconds,clock.ElapsedMilliseconds+30));
                if(shortcut!=null&&shortcut.Count<length){clicks.RemoveRange(index,length);clicks.InsertRange(index,shortcut);}
            }
            var expanded=new List<int>();int blank=Array.IndexOf(initial,24);foreach(int target in clicks){int step=blank/5==target/5?Math.Sign(target-blank):5*Math.Sign(target-blank);while(blank!=target){blank+=step;expanded.Add(blank);}}return expanded;
        }
    }
}
