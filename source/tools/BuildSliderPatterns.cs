using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
class BuildSliderPatterns
{
    static readonly int[] Powers={1,25,625,15625};
    static readonly int[][] Clusters={new[]{0,1,5,6},new[]{2,3,7,8},new[]{4,9,14,19},new[]{10,11,15,16},new[]{12,13,17,18},new[]{20,21,22,23}};
    static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(args[0]);int count=args.Length>1?Int32.Parse(args[1]):12;
            Parallel.For(0,count,new ParallelOptions{MaxDegreeOfParallelism=3},pattern=>Build(args[0],pattern));return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
    static void WriteHash(string file)
    {using(SHA256 sha=SHA256.Create())using(FileStream input=File.OpenRead(file))File.WriteAllText(Path.ChangeExtension(file,".sha256"),BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant());}
    static void Build(string dir,int pattern)
    {
        Thread.CurrentThread.Priority=ThreadPriority.BelowNormal;string file=Path.Combine(dir,pattern+".bin");if(File.Exists(file)&&new FileInfo(file).Length==9765625){WriteHash(file);Console.WriteLine(pattern+" cached");return;}
        var watch=Stopwatch.StartNew();byte[] distance=new byte[9765625];for(int i=0;i<distance.Length;i++)distance[i]=255;
        int goal=24*390625;for(int i=0;i<4;i++)goal+=(pattern<6?pattern*4+i:Clusters[pattern-6][i])*Powers[i];distance[goal]=0;
        var buckets=new Queue<int>[100];for(int i=0;i<buckets.Length;i++)buckets[i]=new Queue<int>();buckets[0].Enqueue(goal);long expanded=0;
        int[][] neighbours=new int[25][];for(int i=0;i<25;i++){var list=new List<int>();if(i>=5)list.Add(i-5);if(i<20)list.Add(i+5);if(i%5>0)list.Add(i-1);if(i%5<4)list.Add(i+1);neighbours[i]=list.ToArray();}
        for(int d=0;d<buckets.Length;d++)
        {
            Queue<int> queue=buckets[d];while(queue.Count>0)
            {
                int key=queue.Dequeue();if(distance[key]!=d)continue;expanded++;
                int v=key,p0=v%25;v/=25;int p1=v%25;v/=25;int p2=v%25;v/=25;int p3=v%25,blank=v/25;
                foreach(int next in neighbours[blank])
                {
                    int multiplier=next==p0?1:next==p1?25:next==p2?625:next==p3?15625:0;
                    int successor=key+(next-blank)*390625+(blank-next)*multiplier,nextDistance=d+(multiplier==0?0:1);
                    if(nextDistance<distance[successor]){distance[successor]=(byte)nextDistance;buckets[nextDistance].Enqueue(successor);}
                }
            }
        }
        File.WriteAllBytes(file,distance);WriteHash(file);Console.WriteLine(pattern+": "+expanded+" states; "+watch.ElapsedMilliseconds+"ms");
    }
}
