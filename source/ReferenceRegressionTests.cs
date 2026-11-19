using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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
                string stump=Path.Combine(assets,"test_assets","reported","unread-treasure-map.png");
                check(File.Exists(stump),"wilderness stump map screenshot");
                using(Bitmap shot=new Bitmap(stump))
                {
                    DetectedPuzzle p=reader.Detect(shot,new Pixels(shot));
                    check(p!=null&&p.Master.Id==59,"stump map screenshot is clue 59, scores="+reader.MapScores);
                    string scores=reader.MapScores??"";
                    int slash=scores.LastIndexOf(" / ",StringComparison.Ordinal);
                    check(slash>0,"stump map is ranked against another map");
                    string best=scores.Substring(0,slash);
                    best=best.Substring(best.LastIndexOf(' ')+1);
                    string other=scores.Substring(slash+3).Trim();
                    check(best.StartsWith("59:",StringComparison.Ordinal),"winner is clue 59, not another map: "+scores);
                    check(other.StartsWith("59:",StringComparison.Ordinal)==false,"runner-up is a different map: "+scores);
                    double bestScore=Double.Parse(best.Substring(best.IndexOf(':')+1),CultureInfo.InvariantCulture);
                    double otherScore=Double.Parse(other.Substring(other.IndexOf(':')+1),CultureInfo.InvariantCulture);
                    check(bestScore>=0.83&&bestScore-otherScore>=0.07,"stump map stays clear of the other maps "+scores);
                    log.AppendLine("Stump map screenshot "+scores);
                }
                ReferenceClue craters=reader.Clues.First(c=>c.Card.Id==59);
                check(craters.Card.Travel.IndexOf("walk a few steps south",StringComparison.Ordinal)>=0,"wilderness course walk is a few steps south");
                check(craters.Card.Travel.IndexOf("1. Level 44",StringComparison.Ordinal)>=0,"level 44 obelisk is the other way");
                foreach(ReferenceClue c in reader.Clues.Where(c=>c.Type!="map"))foreach(string text in c.Text)
                {ReferenceClue found=reader.MatchText(text);check(found!=null&&found.Card.Id==c.Card.Id,"Text mapping "+c.Card.Id+": "+text);}
                foreach(string text in new[]{"","Beware of double agents!","Search","You are scanning on a different level.","Open a supported clue or puzzle.","This is an unrelated note on parchment.","This anagram reveals who to speak to next: NOT A REAL NPC"})check(reader.MatchText(text)==null,"False positive "+text);
                foreach(ReferenceClue c in reader.Clues.Where(c=>c.Type=="coordinates"))
                {string text=System.Text.RegularExpressions.Regex.Replace(c.Text[0],@"\d+",m=>(Int32.Parse(m.Value)+37).ToString());check(reader.MatchText(text)==null,"Wrong coordinate numbers "+c.Card.Id);}
                ReferenceClue alkharid=reader.MatchText("sf "+reader.Clues.First(c=>c.Card.Id==95).Text[0]+" zz");
                check(alkharid!=null&&alkharid.Card.Id==95,"Al Kharid tents survive border junk");
                ReferenceClue beasts=reader.MatchText(reader.Clues.First(c=>c.Card.Id==156).Text[0]+" sand rocks");
                check(beasts!=null&&beasts.Card.Id==156,"Beasts poem survives border junk");
                ReferenceClue calc=reader.MatchText("me am the calc");
                check(calc!=null&&calc.Card.Id==20,"Calc anagram line matches");
                ReferenceClue food=reader.MatchText(reader.Clues.First(c=>c.Card.Id==155).Text[0]+" sand");
                check(food!=null&&food.Card.Id==155,"Foodles clue survives border junk");
                string live="Yawn in the rogues' general store. Beware of double agents! Equip an iron square shield, blue dragonhide vambraces, and an iron pickaxe.";
                ReferenceClue rogues=reader.MatchText(live);
                check(rogues!=null&&rogues.Card.Id==235,"rogues' general store wording maps to clue 235");
                check(rogues.Card.Location.IndexOf("rogues' general store",StringComparison.OrdinalIgnoreCase)>=0,"heading names the rogues' general store");
                check(reader.MatchText("MYSTERIOUS CLUE SCROLL\n"+live)!=null&&reader.MatchText("MYSTERIOUS CLUE SCROLL\n"+live).Card.Id==235,"title junk does not hide the rogues' store clue");
                string fixture=Path.Combine(assets,"test_assets","reported","mysterious-rogues-yawn.png");
                check(File.Exists(fixture),"mysterious scroll fixture");
                using(Bitmap shot=new Bitmap(fixture))
                {
                    Pixels pixels=new Pixels(shot);
                    Rectangle? paper=reader.PrimaryPaper(pixels);
                    check(paper.HasValue,"mysterious scroll frame is found");
                    check(paper.Value.Contains(1000,450),"frame covers the clue text");
                    check(paper.Value.Width>300&&paper.Value.Height>160,"frame is the scroll, not a roof");
                    check(paper.Value.Top>300,"dark title bar is trimmed off the parchment");
                }
                foreach(string shot in new[]{"beasts.png","calc.png","foodles.png","alkharid-real.png","al1.png","al2.png","al3.png","yan.png","duel.png","slider2.png","slider-tree.png","slider3.png","mysterious-rogues-yawn.png"})
                    check(File.Exists(Path.Combine(assets,"test_assets","reported",shot)),"reported fixture "+shot);
                check(File.Exists(Path.Combine(assets,"test_assets","reported","beasts.png")),"beasts fixture");
                check(File.Exists(Path.Combine(assets,"test_assets","reported","slider-tree.png")),"tree slider fixture");
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
