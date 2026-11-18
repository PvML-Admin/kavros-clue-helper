using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace KavrosClueHelper
{
    public static class Tests
    {
        private static int assertions;
        private static void Check(bool condition, string description)
        { assertions++; if (!condition) throw new Exception("FAILED: " + description); }

        // Independent linear algebra oracle. The game is A*x = target-board
        // over GF(3). Enumerate the nullspace to verify both feasibility and
        // minimum click counts without the production solver's row chasing.
        private static int Oracle(int[] board, int direction)
        {
            int minimum = Int32.MaxValue;
            for (int target = 0; target < 3; target++)
            {
                int[,] matrix = new int[25, 26];
                for (int row = 0; row < 25; row++)
                {
                    for (int column = 0; column < 25; column++)
                        if (Math.Abs(row / 5 - column / 5) + Math.Abs(row % 5 - column % 5) <= 1)
                            matrix[row, column] = direction == 1 ? 1 : 2;
                    matrix[row, 25] = LockboxSolver.Mod(target - board[row]);
                }
                List<int> pivots = new List<int>();
                int rank = 0;
                for (int column = 0; column < 25; column++)
                {
                    int pivot = rank;
                    while (pivot < 25 && matrix[pivot, column] == 0) pivot++;
                    if (pivot == 25) continue;
                    for (int j = 0; j < 26; j++)
                    { int swap = matrix[rank, j]; matrix[rank, j] = matrix[pivot, j]; matrix[pivot, j] = swap; }
                    int inverse = matrix[rank, column];
                    for (int j = 0; j < 26; j++) matrix[rank, j] = LockboxSolver.Mod(matrix[rank, j] * inverse);
                    for (int row = 0; row < 25; row++)
                    {
                        if (row == rank) continue;
                        int factor = matrix[row, column];
                        for (int j = 0; j < 26; j++) matrix[row, j] = LockboxSolver.Mod(matrix[row, j] - factor * matrix[rank, j]);
                    }
                    pivots.Add(column); rank++;
                }
                bool possible = true;
                for (int row = rank; row < 25; row++) if (matrix[row, 25] != 0) possible = false;
                if (!possible) continue;
                int[] free = Enumerable.Range(0, 25).Where(c => !pivots.Contains(c)).ToArray();
                int possibilities = (int)Math.Pow(3, free.Length);
                for (int number = 0; number < possibilities; number++)
                {
                    int[] result = new int[25]; int digits = number;
                    foreach (int column in free) { result[column] = digits % 3; digits /= 3; }
                    for (int row = 0; row < rank; row++)
                    {
                        int value = matrix[row, 25];
                        foreach (int column in free) value -= matrix[row, column] * result[column];
                        result[pivots[row]] = LockboxSolver.Mod(value);
                    }
                    minimum = Math.Min(minimum, result.Sum());
                }
            }
            return minimum == Int32.MaxValue ? -1 : minimum;
        }

        private static void Verify(int[] board, int direction, string label)
        {
            Solution solution = LockboxSolver.Solve(board, direction);
            int optimum = Oracle(board, direction);
            Check((solution == null ? -1 : solution.Total) == optimum, label + " optimum agrees with independent GF(3) oracle");
            if (solution == null) return;
            int[] final = (int[])board.Clone();
            // Apply moves independently, by explicit Manhattan neighbourhood.
            for (int i = 0; i < 25; i++)
                for (int j = 0; j < 25; j++)
                    if (Math.Abs(i / 5 - j / 5) + Math.Abs(i % 5 - j % 5) <= 1)
                        final[j] = LockboxSolver.Mod(final[j] + solution.Clicks[i] * direction);
            Check(final.All(v => v == solution.Target), label + " every icon reaches the declared target");
            Check(solution.Clicks.All(v => v >= 0 && v <= 2), label + " click counts in range");
        }

        private static Bitmap Synthetic(int[] values, int size)
        {
            Bitmap image = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(25, 48, 56));
                for (int i = 0; i < 25; i++)
                {
                    int x = i % 5 * size / 5, y = i / 5 * size / 5, cell = size / 5;
                    using (Pen edge = new Pen(Color.FromArgb(140, 160, 165))) graphics.DrawRectangle(edge, x + 1, y + 1, cell - 3, cell - 3);
                    Color colour = values[i] == 0 ? Color.FromArgb(206, 208, 199) : values[i] == 1 ? Color.FromArgb(204, 149, 69) : Color.FromArgb(55, 89, 186);
                    using (Brush brush = new SolidBrush(colour)) graphics.FillEllipse(brush, x + cell * 0.3F, y + cell * 0.25F, cell * 0.35F, cell * 0.50F);
                }
            }
            return image;
        }

        public static int Run(string reportPath)
        {
            StringBuilder report = new StringBuilder();
            try
            {
                for (int symbol = 0; symbol < 3; symbol++) Verify(Enumerable.Repeat(symbol, 25).ToArray(), 1, "uniform " + symbol);
                int[] corner = new int[25]; LockboxSolver.Click(corner, 4, 1, 1);
                Check(corner[0] == 0 && corner[5] == 0 && corner[3] == 1 && corner[4] == 1 && corner[9] == 1 && corner.Sum() == 3, "corner has no row wrap");
                int[] single = new int[25]; LockboxSolver.Click(single, 12, 1, 1); Verify(single, 1, "centre move");
                Verify(LockboxSolver.Example, 1, "screenshot transcription");
                Verify(LockboxSolver.Example, -1, "screenshot reversed cycle");
                Random random = new Random(944);
                for (int sample = 0; sample < 100; sample++)
                {
                    int[] reachable = Enumerable.Repeat(random.Next(3), 25).ToArray();
                    int direction = sample % 2 == 0 ? 1 : -1;
                    for (int i = 0; i < 25; i++) LockboxSolver.Click(reachable, i, random.Next(3), direction);
                    Verify(reachable, direction, "reachable " + sample);
                    if (sample < 30)
                    {
                        int[] arbitrary = Enumerable.Range(0, 25).Select(i => random.Next(3)).ToArray();
                        Verify(arbitrary, direction, "arbitrary " + sample);
                    }
                }
                bool invalid = false; try { LockboxSolver.Solve(new int[24], 1); } catch (ArgumentException) { invalid = true; }
                Check(invalid, "invalid board length rejected");
                invalid = false; try { LockboxSolver.Solve(Enumerable.Repeat(-1, 25).ToArray(), 1); } catch (ArgumentException) { invalid = true; }
                Check(invalid, "unknown symbols rejected");
                invalid = false; try { LockboxSolver.Solve(new int[25], 0); } catch (ArgumentException) { invalid = true; }
                Check(invalid, "invalid cycle direction rejected");
                foreach (int size in new[] { 100, 150, 200, 250, 300 })
                    using (Bitmap image = Synthetic(LockboxSolver.Example, size))
                    {
                        Reading reading = ScreenReader.Read(image);
                        Check(reading.Uncertain == 0 && reading.Board.SequenceEqual(LockboxSolver.Example), "synthetic colour reading at " + size);
                    }
                using (Bitmap image = new Bitmap(150, 150))
                {
                    using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.Black);
                    Check(ScreenReader.Read(image).Uncertain == 25, "blank image is unknown, not a solved board");
                }
                if (TestOptions.IncludeUi) using (HelperForm form = new HelperForm())
                {
                    form.LoadExample();
                    form.ShowInTaskbar = false;
                    form.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                    form.Location = new Point(-30000, -30000);
                    form.Show();
                    System.Windows.Forms.Application.DoEvents();
                    Check(form.ClientSize.Width == 440 && form.ClientSize.Height == 756, "helper form can be created");
                    using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        HashSet<int> colours = new HashSet<int>();
                        for (int y = 310; y < 570; y += 3) for (int x = 20; x < 270; x += 3) colours.Add(bitmap.GetPixel(x, y).ToArgb());
                        Check(colours.Count > 20, "helper form renders its board and child controls");
                        form.Close();
                    }
                }
                Solution example = LockboxSolver.Solve(LockboxSolver.Example, 1);
                report.AppendLine("PASS: " + assertions + " assertions.");
                report.AppendLine("100 reachable boards + 30 arbitrary boards; both cycle directions.");
                report.AppendLine("All click totals verified against an independent GF(3) elimination oracle.");
                report.AppendLine("Synthetic icon colour classification: 100 / 150 / 200 / 250 / 300 px grids.");
                report.AppendLine("Blank images, invalid input, edge neighbourhoods. UI tests: " + TestOptions.IncludeUi);
                report.AppendLine("Screenshot was transcribed visually; live screenshot pixel recognition has NOT been validated.");
                report.AppendLine("Live Kavros capture, game click-through, and multi-monitor DPI need user validation.");
                report.AppendLine("Example: " + (example == null ? "NO SOLUTION" : example.Total + " clicks; target " + example.Target));
                if (example != null) for (int row = 0; row < 5; row++) report.AppendLine(String.Join(" ", example.Clicks.Skip(row * 5).Take(5)));
                File.WriteAllText(reportPath, report.ToString());
                return 0;
            }
            catch (Exception exception)
            { report.AppendLine(exception.ToString()); File.WriteAllText(reportPath, report.ToString()); return 1; }
        }
    }
}
