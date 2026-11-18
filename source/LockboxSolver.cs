using System;
using System.Collections.Generic;
using System.Linq;

namespace KavrosClueHelper
{
    // Symbols follow the legacy combat triangle: melee -> ranged -> magic.
    public sealed class Solution
    {
        public int[] Clicks;
        public int Target;
        public int Total { get { return Clicks.Sum(); } }
    }

    public static class LockboxSolver
    {
        public static readonly int[] Example = {
            0, 0, 1, 0, 2,
            0, 1, 1, 0, 0,
            0, 2, 1, 0, 0,
            2, 0, 0, 1, 0,
            2, 2, 1, 0, 0
        };

        public static int Mod(int value) { return (value % 3 + 3) % 3; }

        public static void Validate(int[] board)
        {
            if (board == null || board.Length != 25 || board.Any(v => v < 0 || v > 2))
                throw new ArgumentException("A lockbox must contain 25 symbols: 0, 1 or 2.");
        }

        public static void Click(int[] board, int index, int times, int direction)
        {
            int row = index / 5, column = index % 5;
            int delta = times * direction;
            board[index] = Mod(board[index] + delta);
            if (row > 0) board[index - 5] = Mod(board[index - 5] + delta);
            if (row < 4) board[index + 5] = Mod(board[index + 5] + delta);
            if (column > 0) board[index - 1] = Mod(board[index - 1] + delta);
            if (column < 4) board[index + 1] = Mod(board[index + 1] + delta);
        }

        // Exhaust all 3^5 top-row choices for each possible final symbol. All
        // later clicks are then forced by the row above. This enumerates every
        // solution modulo 3, so the result minimises the total individual clicks.
        public static Solution Solve(int[] source, int direction)
        {
            Validate(source);
            if (direction != 1 && direction != -1) throw new ArgumentException("Invalid cycle direction.");
            Solution best = null;
            for (int target = 0; target < 3; target++)
            {
                for (int seed = 0; seed < 243; seed++)
                {
                    int[] board = (int[])source.Clone();
                    int[] clicks = new int[25];
                    int digits = seed;
                    for (int column = 0; column < 5; column++)
                    {
                        clicks[column] = digits % 3;
                        digits /= 3;
                        Click(board, column, clicks[column], direction);
                    }
                    for (int row = 1; row < 5; row++)
                        for (int column = 0; column < 5; column++)
                        {
                            int i = row * 5 + column;
                            clicks[i] = Mod((target - board[i - 5]) * direction);
                            Click(board, i, clicks[i], direction);
                        }
                    bool solved = true;
                    for (int i = 20; i < 25; i++) if (board[i] != target) solved = false;
                    if (solved)
                    {
                        Solution candidate = new Solution { Clicks = clicks, Target = target };
                        if (best == null || candidate.Total < best.Total) best = candidate;
                    }
                }
            }
            return best;
        }
    }
}
