using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
namespace KavrosClueHelper
{
    public static class ReferenceRegressionTests
    {
        public static int Run(string report)
        {
            var log=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,msg)=>{count++;if(!ok)throw new Exception(msg);};
            string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
            try
            {
                var reader=new ReferenceClueReader(assets);
                foreach(ReferenceClue c in reader.Clues.Where(c=>c.Type!="map"))foreach(string text in c.Text)
                {ReferenceClue found=reader.MatchText(text);check(found!=null&&found.Card.Id==c.Card.Id,"Text mapping "+c.Card.Id+": "+text);}
                foreach(string text in new[]{"","Beware of double agents!","Search","You are scanning on a different level.","Open a supported clue or puzzle.","This is an unrelated note on parchment.","This anagram reveals who to speak to next: NOT A REAL NPC"})check(reader.MatchText(text)==null,"False positive "+text);
                foreach(ReferenceClue c in reader.Clues.Where(c=>c.Type=="coordinates"))
                {string text=System.Text.RegularExpressions.Regex.Replace(c.Text[0],@"\d+",m=>(Int32.Parse(m.Value)+37).ToString());check(reader.MatchText(text)==null,"Wrong coordinate numbers "+c.Card.Id);}
                log.AppendLine("Exact aliases, ambiguous/negative text and coordinate-number checks passed.");
                foreach(ReferenceClue c in reader.Clues.Where(c=>c.Type=="map"))
                using(Bitmap original=new Bitmap(Path.Combine(assets,"reference-maps",c.Card.Id+".png")))
                foreach(float scale in new[]{1F,0.8F,1.25F})
                using(Bitmap image=Pixels.Crop(original,new Rectangle(Point.Empty,original.Size),new Size((int)(original.Width*scale),(int)(original.Height*scale))))
                {var timer=Stopwatch.StartNew();DetectedPuzzle p=reader.Detect(image,new Pixels(image));check(p!=null&&p.Master.Id==c.Card.Id,"Map "+c.Card.Id+" scale "+scale+" scores="+reader.MapScores+" OCR="+reader.LastText);log.AppendLine("Map "+c.Card.Id+" scale "+scale+" "+timer.ElapsedMilliseconds+"ms");}
                using(var image=new Bitmap(520,320))
                {using(Graphics g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(226,201,151));using(Font font=new Font("Segoe UI",14F,FontStyle.Regular,GraphicsUnit.Pixel))g.DrawString("This parchment does not contain a treasure trail.",font,Brushes.SaddleBrown,new RectangleF(80,120,350,80));}check(reader.Detect(image,new Pixels(image))==null,"Unrelated parchment");}
                foreach(ReferenceClue c in reader.Clues.Where(c=>c.Type!="map").GroupBy(c=>c.Card.Tier+":"+c.Type).Select(g=>g.First()))
                using(var image=new Bitmap(520,320))
                {
                    using(Graphics g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(226,201,151));using(Font font=new Font("Segoe UI",14F,FontStyle.Regular,GraphicsUnit.Pixel))using(Brush ink=new SolidBrush(Color.FromArgb(96,78,51)))using(var format=new StringFormat{Alignment=StringAlignment.Center})g.DrawString(c.Text[0],font,ink,new RectangleF(60,120,400,160),format);}
                    var timer=Stopwatch.StartNew();DetectedPuzzle p=reader.Detect(image,new Pixels(image));check(p!=null&&p.Master.Id==c.Card.Id,"Native OCR "+c.Card.Id+" text="+reader.LastText+" error="+reader.LastError);log.AppendLine("Native OCR "+c.Card.Tier+" "+c.Type+" "+timer.ElapsedMilliseconds+"ms");
                }
                log.AppendLine("PASS: "+count+" catalogue assertions; "+reader.Clues.Count+" cards enabled.");File.WriteAllText(report,log.ToString());return 0;
            }
            catch(Exception e){log.AppendLine("FAIL after "+count+": "+e);File.WriteAllText(report,log.ToString());return 1;}
        }
    }
}
