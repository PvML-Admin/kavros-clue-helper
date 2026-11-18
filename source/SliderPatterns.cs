// Independently generated additive pattern databases. Each operator is charged
// only in the one disjoint group containing the moved tile. See the original
// method: https://web.cs.ucla.edu/~korf/publications.html (Korf / Felner).
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace KavrosClueHelper
{
    public static class SliderPatterns
    {
        private static readonly byte[][] tables=Load();
        private static readonly int[] transpose=Enumerable.Range(0,25).Select(p=>p%5*5+p/5).ToArray();
        private static readonly int[][] clusters={new[]{0,1,5,6},new[]{2,3,7,8},new[]{4,9,14,19},new[]{10,11,15,16},new[]{12,13,17,18},new[]{20,21,22,23}};
        public static int Count {get{return tables.Length;}}
        private static byte[][] Load()
        {
            string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","data","slider-patterns");
            var result=new byte[12][];
            try
            {
                for(int i=0;i<12;i++)
                {
                    result[i]=File.ReadAllBytes(Path.Combine(dir,i+".bin"));if(result[i].Length!=9765625)return new byte[0][];
                    string expected=File.ReadAllText(Path.Combine(dir,i+".sha256")).Trim();
                    using(SHA256 sha=SHA256.Create()){string actual=BitConverter.ToString(sha.ComputeHash(result[i])).Replace("-","").ToLowerInvariant();if(actual!=expected)return new byte[0][];}
                }
                return result;
            }
            catch(IOException){return new byte[0][];}catch(UnauthorizedAccessException){return new byte[0][];}
        }
        public static int Bound(int[] board,bool clustersEnabled=true)
        {
            if(tables.Length==0)return 0;
            int[] positions=new int[25];for(int p=0;p<25;p++)positions[board[p]]=p;
            int maximum=0,blank=positions[24];
            for(int partition=0;partition<(clustersEnabled?2:1);partition++)
            {
                int normal=0,reflected=0;
                for(int group=0;group<6;group++)
                {
                    int first=group*4,t0=partition==0?first:clusters[group][0],t1=partition==0?first+1:clusters[group][1],t2=partition==0?first+2:clusters[group][2],t3=partition==0?first+3:clusters[group][3];
                    int a=positions[t0]+positions[t1]*25+positions[t2]*625+positions[t3]*15625+blank*390625;
                    int b=transpose[positions[transpose[t0]]]+transpose[positions[transpose[t1]]]*25+transpose[positions[transpose[t2]]]*625+transpose[positions[transpose[t3]]]*15625+transpose[blank]*390625;
                    normal+=tables[partition*6+group][a];reflected+=tables[partition*6+group][b];
                }
                maximum=Math.Max(maximum,Math.Max(normal,reflected));
            }
            return maximum;
        }
    }
}
