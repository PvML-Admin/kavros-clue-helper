using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace KavrosClueHelper
{
    public static class AutomationRegressionTests
    {
        private static int assertions;
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("FAILED: "+message);}
        public static Bitmap Minimap(string assets,ScanRegion region,PointF position,double scale,double angle,bool icons)
        {
            var result=new Bitmap(240,240);
            using(var map=new Bitmap(Path.Combine(assets,"test_assets","minimap",region.Id+".png")))
            {
                double cos=Math.Cos(angle)/scale,sin=Math.Sin(angle)/scale;
                for(int y=0;y<240;y++)for(int x=0;x<240;x++)
                {
                    double wx=position.X+(x-120)*cos-(y-120)*sin,wy=position.Y-(x-120)*sin-(y-120)*cos;
                    int mx=(int)Math.Round((wx-region.MapBounds.Left)/region.MapBounds.Width*map.Width),my=(int)Math.Round((region.MapBounds.Bottom-wy)/region.MapBounds.Height*map.Height);
                    result.SetPixel(x,y,mx>=0&&my>=0&&mx<map.Width&&my<map.Height?map.GetPixel(mx,my):Color.FromArgb(25,33,40));
                }
            }
            using(Graphics g=Graphics.FromImage(result))
            {
                if(icons){foreach(Point p in new[]{new Point(65,90),new Point(165,70),new Point(90,172)}){g.FillEllipse(Brushes.DodgerBlue,p.X-5,p.Y-5,10,10);g.DrawEllipse(Pens.Gold,p.X-5,p.Y-5,10,10);}for(int i=0;i<12;i++)g.FillRectangle(Brushes.Yellow,30+i*13,40+(i*37)%150,2,2);}
                g.FillRectangle(Brushes.White,119,119,3,3);
            }
            return result;
        }
        public static int Run(string report)
        {
            try
            {
                string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");var regions=new ScanVision(assets).Regions;
                var reader=new MinimapPositionReader(assets);
                foreach(var test in new[]{new{Id=368,P=new PointF(2208,3360),S=4.0,A=0.0},new{Id=368,P=new PointF(2240,3390),S=3.0,A=Math.PI/6},new{Id=371,P=new PointF(3190,6970),S=4.0,A=0.0},new{Id=368,P=new PointF(2180,3400),S=2.7,A=Math.PI/10},new{Id=371,P=new PointF(3220,7000),S=5.2,A=-73*Math.PI/180}})
                {
                    ScanRegion region=regions.Single(r=>r.Id==test.Id);reader.Reset();
                    Check(region.MapBounds.Contains(test.P),"test point is within the region");
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    using(Bitmap mini=Minimap(assets,region,test.P,test.S,test.A,true))
                    {
                        MinimapMatch match=reader.Find(mini,region,Point.Empty,CancellationToken.None);
                        File.AppendAllText(report,"Initial minimap "+test.Id+" "+test.P+": "+(match==null?"NONE":match.Position+" error "+match.Error.ToString("0.00"))+"; "+clock.ElapsedMilliseconds+" ms. "+reader.Diagnostics+"\r\n");
                        Check(match!=null&&CompassMath.Distance(match.Position,test.P)<=2,"map localization with icons and rotation: "+test.Id);
                    }
                    for(int step=1;step<=3;step++)
                    using(Bitmap mini=Minimap(assets,region,new PointF(test.P.X+step*3,test.P.Y-step*2),test.S,test.A,true))
                    {
                        clock.Restart();MinimapMatch match=reader.Find(mini,region,Point.Empty,CancellationToken.None);
                        File.AppendAllText(report,"Movement "+step+": "+(match==null?"NONE":match.Position.ToString())+"; "+clock.ElapsedMilliseconds+" ms. "+reader.Diagnostics+"\r\n");
                        Check(match!=null&&CompassMath.Distance(match.Position,new PointF(test.P.X+step*3,test.P.Y-step*2))<=2,"movement follows the minimap");
                    }
                    using(Bitmap blank=new Bitmap(240,240))
                    {using(Graphics g=Graphics.FromImage(blank)){g.Clear(Color.OliveDrab);g.FillRectangle(Brushes.White,119,119,3,3);}Check(reader.Find(blank,region,Point.Empty,CancellationToken.None)==null,"untextured false position is rejected");}
                }
                foreach(ScanRegion region in regions)
                using(Bitmap scene=new Bitmap(Path.Combine(assets,"test_assets","reported","compass-edgeville.png")))
                {
                    reader.Reset();var clock=System.Diagnostics.Stopwatch.StartNew();
                    Check(reader.Find(scene,region,Point.Empty,CancellationToken.None)==null,"real unrelated minimap/scenery cannot become a scan position for "+region.Name);
                    File.AppendAllText(report,"Unrelated full game / "+region.Name+": "+clock.ElapsedMilliseconds+" ms. "+reader.Diagnostics+"\r\n");
                }
                var scan=new ScanSession();scan.Observe(new ScanReading{Region=regions[0]});DateTime now=DateTime.UtcNow;
                MinimapMatch first=new MinimapMatch{Position=new PointF(2208,3360)},second=new MinimapMatch{Position=new PointF(2210,3361)};
                scan.ApplyMatch(first,now);Check(!scan.Position.HasValue,"single match cannot place the live range box");
                scan.ApplyMatch(second,now.AddMilliseconds(250));Check(scan.PositionTracked&&scan.Position.Value==second.Position,"consistent next frame enables automatic position");
                scan.ApplyMatch(null,now.AddMilliseconds(500));Check(scan.PositionTracked,"a missing frame keeps the live marker");scan.ApplyMatch(null,now.AddMilliseconds(1200));Check(scan.Position.HasValue,"a short gap does not drop the player");
                scan.ApplyMatch(first,now.AddMilliseconds(750));scan.ApplyMatch(second,now.AddMilliseconds(1000));scan.Expire(now.AddSeconds(3));Check(scan.Position.HasValue,"recent tracking survives a short silence");
                scan.SetManual(new PointF(2200,3350));scan.ApplyMatch(second,now.AddSeconds(4));Check(!scan.AutoTracking&&scan.Position.Value==new PointF(2200,3350),"manual fallback disables automatic overwrites");
                scan.SetAutomatic(true);Check(!scan.Position.HasValue,"enabling automatic tracking removes the old manual position");
                using(Bitmap scene=new Bitmap(1600,900))using(Bitmap mini=Minimap(assets,regions[0],new PointF(2208,3360),4,0,true))
                {
                    using(Graphics g=Graphics.FromImage(scene)){g.Clear(Color.FromArgb(30,40,50));g.DrawImageUnscaled(mini,1310,50);}
                    reader.Reset();MinimapMatch match=reader.Find(scene,regions[0],Point.Empty,CancellationToken.None);
                    Check(match!=null&&CompassMath.Distance(match.Position,new PointF(2208,3360))<=2,"automatic player-dot discovery inside the complete game window");
                    Check(match.PlayerPixel==new Point(1430,170),"minimap capture preserves the client coordinate origin");
                    using(Bitmap crop=scene.Clone(new Rectangle(1312,52,236,236),System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                    {match=reader.Find(crop,regions[0],new Point(1312,52),CancellationToken.None);Check(match!=null&&match.PlayerPixel==new Point(1430,170),"cropped follow-up keeps full-game coordinates: "+reader.Diagnostics);}
                }
                var compass=new CompassSession(assets);Check(compass.Positions.Count==28,"all 28 baseline lodestones are imported");
                Check(compass.Positions.Any(p=>p.Name=="Taverley lodestone")&&compass.Positions.Any(p=>p.Name=="Lunar Isle lodestone"),"quoted upstream destinations are restored");
                Check(compass.Positions.All(p=>p.Name.All(c=>c<128)),"no corrupt punctuation in built-in labels");
                string prefs=Path.Combine(Path.GetDirectoryName(report),"automation-prefs.txt");
                File.WriteAllText(prefs,"spot\t"+Uri.EscapeDataString("Seers\u00c2\u00b4 Village lodestone (Kavros)")+"\t2688\t3481\t2\r\n");
                var loaded=new OverlayPreferences(prefs);Check(loaded.SavedPositions.Single().Name=="Seers' Village lodestone (Kavros)"&&loaded.SavedPositions[0].X==2688&&loaded.SavedPositions[0].Radius==2,"saved labels repair without moving custom coordinates");File.Delete(prefs);
                using(var map=new CompassMap(assets))
                {
                    map.Size=new Size(380,260);map.SetSession(compass,false);var origin=compass.Positions.Single(p=>p.Name=="Edgeville lodestone");PointF iconPoint=map.ToScreen(origin.Point);
                    Check(map.HitLodestone(Point.Round(iconPoint))==origin,"map icon selects the corresponding destination");
                    typeof(CompassMap).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,(int)iconPoint.X,(int)iconPoint.Y,0)});
                    CompassPosition selected=null;map.LodestonePicked+=v=>selected=v;
                    typeof(CompassMap).GetMethod("OnMouseUp",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,(int)iconPoint.X,(int)iconPoint.Y,0)});
                    Check(selected==origin,"clicking the picture dispatches the selection");
                }
                using(var panel=new FloatingCluePanel(assets,compass,new OverlayPreferences(Path.Combine(Path.GetDirectoryName(report),"unused-automation-prefs.txt"))))
                {
                    DateTime tick=DateTime.UtcNow.AddMilliseconds(-300);for(int i=0;i<3;i++)compass.Observe(new CompassReading{Angle=0.4,Error=0.01},tick.AddMilliseconds(i*125));
                    panel.SetCompass(false);
                    var map=panel.Controls.OfType<CompassMap>().Single();var origin=compass.Positions.Single(p=>p.Name=="Edgeville lodestone");PointF pixel=map.ToScreen(origin.Point);
                    typeof(CompassMap).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,(int)pixel.X,(int)pixel.Y,0)});
                    typeof(CompassMap).GetMethod("OnMouseUp",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,(int)pixel.X,(int)pixel.Y,0)});
                    Panel buttons=panel.Controls.OfType<Panel>().Single(v=>v.Controls.OfType<Button>().Any(b=>b.Text=="Record direction"));
                    Button record=buttons.Controls.OfType<Button>().Single(b=>b.Text=="Record direction");Check(record.Enabled,"clicking a lodestone enables stable recording without a checkbox");
                    typeof(Button).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(record,new object[]{EventArgs.Empty});
                    Check(compass.Bearings.Count==1&&compass.Bearings[0].Position.Name==origin.Name,"two-click flow records the correct selected origin");
                    Check(!record.Enabled,"recording requires a new explicit origin selection");
                    using(Bitmap preview=new Bitmap(map.Width,map.Height))using(Graphics previewGraphics=Graphics.FromImage(preview)){typeof(CompassMap).GetMethod("OnPaint",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(map,new object[]{new PaintEventArgs(previewGraphics,new Rectangle(Point.Empty,map.Size))});preview.Save(Path.Combine(Path.GetDirectoryName(report),"v051-compass-map.png"));}
                    panel.SetPuzzle(new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=new ScanReading{Region=regions[0]}},"");panel.RefreshCompass(false);
                    Check(panel.Controls.OfType<Label>().Any(v=>v.Text.StartsWith("Prifddinas |")),"compass refresh cannot overwrite the scan heading");
                    foreach(MasterClue clue in MasterClues.All)
                    {
                        panel.SetCompass(false);panel.SetPuzzle(new DetectedPuzzle{Kind=PuzzleKind.EmoteClue,Master=clue},"");
                        Panel footer=panel.Controls.OfType<Panel>().Single(v=>v.Controls.OfType<Button>().Any(b=>b.Text=="Clear"));
                        var master=panel.Controls.OfType<MasterClueView>().Single();
                        Check(panel.ClientRectangle.Contains(footer.Bounds)&&master.Bottom<footer.Top,"new master clue leaves the complete footer below the content: "+clue.Id);
                        Check(footer.Controls.OfType<Button>().All(b=>footer.ClientRectangle.Contains(b.Bounds)),"no footer button is clipped after a clue transition");
                        panel.SetPuzzle(new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=new ScanReading{Region=regions[0]}},"");
                        Check(panel.ClientRectangle.Contains(footer.Bounds),"scan-to-master resizing keeps footer inside the panel");
                    }
                    panel.ApplyTracking(first,regions[0].Id,now);panel.ApplyTracking(second,regions[0].Id,now.AddMilliseconds(250));
                    using(Bitmap preview=new Bitmap(380,260))using(Graphics previewGraphics=Graphics.FromImage(preview)){var scanMap=panel.Controls.OfType<ScanMap>().Single();typeof(ScanMap).GetMethod("OnPaint",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(scanMap,new object[]{new PaintEventArgs(previewGraphics,new Rectangle(Point.Empty,scanMap.Size))});preview.Save(Path.Combine(Path.GetDirectoryName(report),"v051-scan-map.png"));}
                }
                var searchPreferences=new OverlayPreferences(Path.Combine(Path.GetDirectoryName(report),"unused-search-prefs.txt"));
                searchPreferences.SavedPositions.Add(new CompassPosition{Name="My saved spot",X=3000,Y=3300,Radius=2});
                using(var panel=new FloatingCluePanel(assets,compass,searchPreferences))
                {
                    panel.SetCompass(false);DateTime stamp=DateTime.UtcNow.AddMilliseconds(-300);
                    for(int i=0;i<3;i++)compass.Observe(new CompassReading{Angle=0.4,Error=0.01},stamp.AddMilliseconds(i*125));
                    var controls=panel.Controls.OfType<Panel>().Single(p=>p.Controls.OfType<ComboBox>().Any(c=>c.DropDownStyle!=ComboBoxStyle.DropDownList));
                    ComboBox picker=controls.Controls.OfType<ComboBox>().Single();
                    MethodInfo keyDown=typeof(ComboBox).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance);
                    picker.Text="eD";keyDown.Invoke(picker,new object[]{new KeyEventArgs(Keys.Enter)});
                    Check(((CompassPosition)picker.SelectedItem).Name=="Edgeville lodestone","short mixed-case prefix commits Edgeville");
                    Check(controls.Controls.OfType<NumericUpDown>().Single(n=>n.Maximum==6400).Value==3067&&controls.Controls.OfType<NumericUpDown>().Single(n=>n.Maximum==12800).Value==3505,"prefix selection sets the correct coordinates");
                    Check(controls.Controls.OfType<Button>().Single(b=>b.Text=="Record direction").Enabled,"search selection enables recording for a stable compass");
                    picker.Text="my";keyDown.Invoke(picker,new object[]{new KeyEventArgs(Keys.Enter)});
                    Check(((CompassPosition)picker.SelectedItem).Name=="My saved spot"&&controls.Controls.OfType<NumericUpDown>().Single(n=>n.Maximum==6400).Value==3000,"saved Kavros positions are searchable");
                    picker.Text="zzzz";keyDown.Invoke(picker,new object[]{new KeyEventArgs(Keys.Enter)});
                    Check(!controls.Controls.OfType<Button>().Single(b=>b.Text=="Record direction").Enabled,"unknown search text cannot record stale coordinates");
                }
                File.AppendAllText(report,"V0.5.3 AUTOMATION PASS: "+assertions+" assertions. Screenshot-generated minimap movement/rotation/icon and no-match tests; map clicks, full catalog and saved-coordinate compatibility. Live Kavros scan minimaps still require validation.\r\n");return 0;
            }
            catch(Exception e){File.AppendAllText(report,e+"\r\n");return 1;}
        }
    }
}
