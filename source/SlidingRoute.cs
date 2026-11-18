using System;
using System.Collections.Generic;
using System.Linq;
namespace KavrosClueHelper
{
    public static class SlidingRoute
    {
        public static List<int> ClickRoute(int[] initial,IList<int> route)
        {
            var clicks=new List<int>();int blank=Array.IndexOf(initial,24),i=0;
            while(i<route.Count)
            {
                int direction=route[i]-blank,target=blank;
                do{int next=route[i];if(!SlidingSolver.Neighbours(target).Contains(next))throw new ArgumentException("Invalid slider route");target=next;i++;}while(i<route.Count&&route[i]-target==direction);
                clicks.Add(target);blank=target;
            }
            return clicks;
        }
        public static int CompletedClicks(int[] initial,IList<int> route,int consumed)
        {
            int blank=Array.IndexOf(initial,24),i=0,count=0;
            while(i<route.Count)
            {int direction=route[i]-blank;do{blank=route[i++];}while(i<route.Count&&route[i]-blank==direction);if(i>consumed)break;count++;}
            return count;
        }
        public static bool Advance(int[] previous,IList<int> route,int[] current,out List<int> remaining)
        {
            remaining=null;if(!SlidingSolver.IsValid(previous)||!SlidingSolver.IsValid(current)||route==null)return false;
            int[] board=(int[])previous.Clone();
            for(int i=0;i<=route.Count;i++)
            {
                if(board.SequenceEqual(current)){remaining=route.Skip(i).ToList();return true;}
                if(i==route.Count)break;
                int blank=Array.IndexOf(board,24),next=route[i];
                if(!SlidingSolver.Neighbours(blank).Contains(next))return false;
                board[blank]=board[next];board[next]=24;
            }
            return false;
        }
        public static List<int> Compact(int[] initial,IList<int> route)
        {
            int[] board=(int[])initial.Clone();var result=new List<int>();var states=new List<string>{String.Join(",",board)};
            var seen=new Dictionary<string,int>{{states[0],0}};
            foreach(int move in route)
            {
                int blank=Array.IndexOf(board,24);
                if(!SlidingSolver.Neighbours(blank).Contains(move))throw new ArgumentException("Sliding route contains a non-adjacent move");
                board[blank]=board[move];board[move]=24;result.Add(move);string key=String.Join(",",board);int previous;
                if(seen.TryGetValue(key,out previous))
                {
                    result.RemoveRange(previous,result.Count-previous);
                    foreach(string state in states.Skip(previous+1))seen.Remove(state);
                    states.RemoveRange(previous+1,states.Count-previous-1);
                }
                else{seen[key]=result.Count;states.Add(key);}
            }
            return result;
        }
    }
}
